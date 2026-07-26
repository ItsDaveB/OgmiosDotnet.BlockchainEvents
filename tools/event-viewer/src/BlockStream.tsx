import { useMemo, useRef, useEffect, useState } from 'react';
import type { BlockchainEvent } from './types';

export interface BlockSummary {
  height: number;
  hash: string;
  slot: number;
  era: string;
  eventCount: number;
  rules: Record<string, number>;
  latestTime: string;
}

const MAX_VISIBLE_BLOCKS = 24;
const FRESH_MS = 2800;

function getRuleClass(ruleName: string): string {
  const lower = ruleName.toLowerCase();
  if (lower.includes('address')) return 'address-match';
  if (lower.includes('metadata') || lower.includes('key')) return 'metadata';
  if (lower.includes('governance') || lower.includes('treasury')) return 'governance';
  if (lower.includes('all transaction')) return 'all-transactions';
  return 'default';
}

function dominantRule(rules: Record<string, number>): string {
  let best = '';
  let max = 0;
  for (const [name, count] of Object.entries(rules)) {
    if (count > max) {
      max = count;
      best = name;
    }
  }
  return best;
}

function shortHash(hash: string): string {
  if (!hash || hash.length < 12) return hash || '—';
  return `${hash.slice(0, 6)}··${hash.slice(-4)}`;
}

export function deriveBlocks(events: BlockchainEvent[]): BlockSummary[] {
  const map = new Map<number, BlockSummary>();

  for (const event of events) {
    const height = event.cardanoBlockHeight;
    if (!height) continue;

    const existing = map.get(height);
    const ruleName = event.data?.ruleName ?? 'Unknown';

    if (existing) {
      existing.eventCount += 1;
      existing.rules[ruleName] = (existing.rules[ruleName] ?? 0) + 1;
      if (event.time > existing.latestTime) existing.latestTime = event.time;
      if (!existing.hash && event.cardanoBlock) existing.hash = event.cardanoBlock;
      if (!existing.slot && event.cardanoSlot) existing.slot = event.cardanoSlot;
      if (!existing.era && event.cardanoEra) existing.era = event.cardanoEra;
    } else {
      map.set(height, {
        height,
        hash: event.cardanoBlock ?? '',
        slot: event.cardanoSlot ?? 0,
        era: event.cardanoEra ?? '',
        eventCount: 1,
        rules: { [ruleName]: 1 },
        latestTime: event.time,
      });
    }
  }

  return [...map.values()]
    .sort((a, b) => b.height - a.height)
    .slice(0, MAX_VISIBLE_BLOCKS);
}

interface Props {
  events: BlockchainEvent[];
  selectedHeight: number | null;
  onSelectBlock: (height: number | null) => void;
  live: boolean;
}

export function BlockStream({ events, selectedHeight, onSelectBlock, live }: Props) {
  const blocks = useMemo(() => deriveBlocks(events), [events]);
  const tip = blocks[0] ?? null;

  const [freshHeights, setFreshHeights] = useState<Set<number>>(new Set());
  const seenHeights = useRef<Set<number>>(new Set());
  const primed = useRef(false);
  const railRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (blocks.length === 0) {
      seenHeights.current.clear();
      primed.current = false;
      return;
    }

    // First paint: seed without animating the whole backlog
    if (!primed.current) {
      blocks.forEach(b => seenHeights.current.add(b.height));
      primed.current = true;
      return;
    }

    const newcomers = blocks.filter(b => !seenHeights.current.has(b.height));
    if (newcomers.length === 0) return;

    newcomers.forEach(b => seenHeights.current.add(b.height));
    const heights = newcomers.map(b => b.height);

    setFreshHeights(prev => {
      const next = new Set(prev);
      heights.forEach(h => next.add(h));
      return next;
    });

    const timer = window.setTimeout(() => {
      setFreshHeights(prev => {
        const next = new Set(prev);
        heights.forEach(h => next.delete(h));
        return next;
      });
    }, FRESH_MS);

    return () => window.clearTimeout(timer);
  }, [blocks]);

  // Keep newest block in view when the tip advances
  useEffect(() => {
    if (!tip || !railRef.current) return;
    railRef.current.scrollTo({ left: 0, behavior: 'smooth' });
  }, [tip?.height]);

  const tipDelta = useMemo(() => {
    if (blocks.length < 2) return null;
    return blocks[0].height - blocks[1].height;
  }, [blocks]);

  return (
    <section className="block-stream" aria-label="Live block stream">
      <div className="block-stream-header">
        <div className="block-stream-title">
          <span className={`block-stream-pulse ${live ? 'live' : ''}`} aria-hidden="true" />
          <div>
            <div className="block-stream-heading">Block stream</div>
            <div className="block-stream-sub">
              Matched transactions grouped by chain tip
            </div>
          </div>
        </div>

        <div className="chain-tip" data-fresh={tip && freshHeights.has(tip.height) ? 'true' : 'false'}>
          <div className="chain-tip-item">
            <span className="chain-tip-label">Tip height</span>
            <span className="chain-tip-value tip-height">
              {tip ? tip.height.toLocaleString() : '—'}
            </span>
          </div>
          <div className="chain-tip-divider" />
          <div className="chain-tip-item">
            <span className="chain-tip-label">Slot</span>
            <span className="chain-tip-value tip-slot">
              {tip ? tip.slot.toLocaleString() : '—'}
            </span>
          </div>
          <div className="chain-tip-divider" />
          <div className="chain-tip-item">
            <span className="chain-tip-label">Era</span>
            <span className="chain-tip-value tip-era">{tip?.era || '—'}</span>
          </div>
          {tipDelta !== null && tipDelta > 0 && (
            <>
              <div className="chain-tip-divider" />
              <div className="chain-tip-item">
                <span className="chain-tip-label">Δ height</span>
                <span className="chain-tip-value tip-delta">+{tipDelta}</span>
              </div>
            </>
          )}
        </div>
      </div>

      {blocks.length === 0 ? (
        <div className="block-stream-empty">
          <div className="block-stream-empty-track" aria-hidden="true">
            {Array.from({ length: 8 }).map((_, i) => (
              <div key={i} className="block-ghost" style={{ animationDelay: `${i * 0.12}s` }} />
            ))}
          </div>
          <p>Waiting for the next matched block…</p>
        </div>
      ) : (
        <div className="block-rail-wrap">
          <div className="block-rail" ref={railRef}>
            {blocks.map((block, index) => {
              const rule = dominantRule(block.rules);
              const ruleClass = getRuleClass(rule);
              const isSelected = selectedHeight === block.height;
              const isFresh = freshHeights.has(block.height);
              const isTip = index === 0;

              return (
                <button
                  key={block.height}
                  type="button"
                  className={[
                    'block-card',
                    ruleClass,
                    isTip ? 'is-tip' : '',
                    isFresh ? 'is-fresh' : '',
                    isSelected ? 'is-selected' : '',
                  ].filter(Boolean).join(' ')}
                  onClick={() => onSelectBlock(isSelected ? null : block.height)}
                  title={`Block ${block.height} — ${block.eventCount} matched event(s). Click to filter.`}
                >
                  <div className="block-card-top">
                    <span className="block-card-badge">{isTip ? 'TIP' : `#${block.height.toLocaleString()}`}</span>
                    {isFresh && <span className="block-card-new">NEW</span>}
                  </div>
                  <div className="block-card-height">
                    {block.height.toLocaleString()}
                  </div>
                  <div className="block-card-meta">
                    <span>{block.eventCount} tx</span>
                    <span className="block-card-dot" />
                    <span>slot {block.slot.toLocaleString()}</span>
                  </div>
                  <div className="block-card-rules">
                    {Object.entries(block.rules)
                      .sort((a, b) => b[1] - a[1])
                      .slice(0, 3)
                      .map(([name, count]) => (
                        <span key={name} className={`block-rule-pip ${getRuleClass(name)}`}>
                          {count}
                        </span>
                      ))}
                  </div>
                  <div className="block-card-hash" title={block.hash}>{shortHash(block.hash)}</div>
                </button>
              );
            })}
          </div>
          <div className="block-rail-fade" aria-hidden="true" />
        </div>
      )}

      {selectedHeight !== null && (
        <div className="block-filter-bar">
          <span>
            Filtering table to block <strong>{selectedHeight.toLocaleString()}</strong>
          </span>
          <button type="button" className="block-filter-clear" onClick={() => onSelectBlock(null)}>
            Clear filter
          </button>
        </div>
      )}
    </section>
  );
}
