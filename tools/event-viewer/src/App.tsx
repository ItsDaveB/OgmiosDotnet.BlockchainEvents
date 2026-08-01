import { useState, useMemo, useCallback, useRef, useEffect } from 'react';
import {
  useReactTable,
  getCoreRowModel,
  getSortedRowModel,
  getFilteredRowModel,
  flexRender,
  type ColumnDef,
  type SortingState,
} from '@tanstack/react-table';
import { useEventStream } from './useEventStream';
import type { BlockchainEvent } from './types';
import { EventDetailDrawer } from './EventDetailDrawer';
import { BlockStream } from './BlockStream';
import { extractMinswapSwap, MinswapSwapFeed, type BlockTruckData } from './MinswapSwapCard';
import { DEMO_RULE_CONFIGS, formatMetadataSummary } from './ruleConfigs';
import './App.css';

const SSE_BASE_URL = (window as unknown as Record<string, unknown>).__SSE_URL__ as string
  || import.meta.env.VITE_SSE_URL
  || 'http://localhost:4000/events/stream';

const DEFAULT_RULE_FILTER = (window as unknown as Record<string, unknown>).__RULE_FILTER__ as string
  || import.meta.env.VITE_RULE_FILTER
  || null;

const INSTANCE_ID = (window as unknown as Record<string, unknown>).__INSTANCE_ID__ as string
  || import.meta.env.VITE_INSTANCE_ID
  || `viewer-${Math.random().toString(36).slice(2, 6)}`;

/* ─── SVG Icons ──────────────────────────────── */
const CardanoLogo = () => (
  <svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
    <circle cx="12" cy="12" r="2.8" fill="white" />
    <circle cx="12" cy="4.5" r="1.4" fill="white" opacity="0.85" />
    <circle cx="12" cy="19.5" r="1.4" fill="white" opacity="0.85" />
    <circle cx="5.5" cy="8.2" r="1.4" fill="white" opacity="0.85" />
    <circle cx="18.5" cy="8.2" r="1.4" fill="white" opacity="0.85" />
    <circle cx="5.5" cy="15.8" r="1.4" fill="white" opacity="0.85" />
    <circle cx="18.5" cy="15.8" r="1.4" fill="white" opacity="0.85" />
    <circle cx="8.5" cy="3" r="0.9" fill="white" opacity="0.45" />
    <circle cx="15.5" cy="3" r="0.9" fill="white" opacity="0.45" />
    <circle cx="3" cy="12" r="0.9" fill="white" opacity="0.45" />
    <circle cx="21" cy="12" r="0.9" fill="white" opacity="0.45" />
    <circle cx="8.5" cy="21" r="0.9" fill="white" opacity="0.45" />
    <circle cx="15.5" cy="21" r="0.9" fill="white" opacity="0.45" />
  </svg>
);

const SearchIcon = () => (
  <svg className="search-icon" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round">
    <circle cx="7" cy="7" r="4.5" />
    <path d="M10.5 10.5L14 14" />
  </svg>
);

/* ─── Sparkline Component ────────────────────── */
function Sparkline({ data, color = 'var(--accent-cyan)' }: { data: number[]; color?: string }) {
  if (data.length < 2) return null;
  const max = Math.max(...data, 0.1);
  const w = 120, h = 32;
  const points = data.map((v, i) =>
    `${(i / (data.length - 1)) * w},${h - (v / max) * (h - 2) - 1}`
  ).join(' ');
  const areaPoints = `0,${h} ${points} ${w},${h}`;
  const id = `spark-${Math.random().toString(36).slice(2, 8)}`;
  return (
    <div className="stat-sparkline">
      <svg viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none">
        <defs>
          <linearGradient id={id} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={color} stopOpacity="0.25" />
            <stop offset="100%" stopColor={color} stopOpacity="0" />
          </linearGradient>
        </defs>
        <polyline fill={`url(#${id})`} stroke="none" points={areaPoints} />
        <polyline fill="none" stroke={color} strokeWidth="1.5" strokeLinejoin="round" points={points} />
      </svg>
    </div>
  );
}

/* ─── Helpers ────────────────────────────────── */
function getRuleClass(ruleName: string | undefined): string {
  if (!ruleName) return 'default';
  const lower = ruleName.toLowerCase();
  if (lower.includes('address')) return 'address-match';
  if (lower.includes('metadata') || lower.includes('key')) return 'metadata';
  if (lower.includes('governance') || lower.includes('treasury')) return 'governance';
  if (lower.includes('all transaction')) return 'all-transactions';
  return 'default';
}

function formatFee(lovelace: number | undefined): string {
  if (!lovelace) return '—';
  return (lovelace / 1_000_000).toFixed(3);
}

function formatUptime(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  if (h > 0) return `${h}h ${m}m ${s}s`;
  if (m > 0) return `${m}m ${s}s`;
  return `${s}s`;
}

function formatTime(iso: string): string {
  try {
    const d = new Date(iso);
    return d.toLocaleTimeString('en-GB', { hour12: false, hour: '2-digit', minute: '2-digit', second: '2-digit' })
      + '.' + String(d.getMilliseconds()).padStart(3, '0');
  } catch {
    return iso;
  }
}

/* ─── Columns ────────────────────────────────── */
const columns: ColumnDef<BlockchainEvent>[] = [
  {
    id: 'time',
    header: 'Time',
    accessorFn: (row) => row.time,
    cell: ({ getValue }) => <span className="cell-time">{formatTime(getValue() as string)}</span>,
    size: 110,
  },
  {
    id: 'rule',
    header: 'Rule',
    accessorFn: (row) => row.data?.ruleName ?? '',
    cell: ({ getValue }) => {
      const name = getValue() as string;
      return <span className={`cell-rule ${getRuleClass(name)}`}>{name || '—'}</span>;
    },
    size: 180,
  },
  {
    id: 'txId',
    header: 'Transaction',
    accessorFn: (row) => row.data?.transactionId ?? '',
    cell: ({ getValue }) => {
      const id = getValue() as string;
      return <span className="cell-tx-id" title={id}>{id ? id.slice(0, 8) + '···' + id.slice(-6) : '—'}</span>;
    },
    size: 170,
  },
  {
    id: 'metadata',
    header: 'Swap / Metadata',
    accessorFn: (row) => formatMetadataSummary(row.data?.matchedCriteria),
    cell: ({ row: r, getValue }) => {
      const swap = extractMinswapSwap(r.original);
      if (swap) {
        return (
          <span className={`cell-swap dir-${(swap.direction ?? 'swap').toLowerCase()}`} title={getValue() as string}>
            <span className="cell-swap-dir">{swap.direction}</span>
            {swap.amountIn} {swap.swapInTicker}
            <span className="cell-swap-arrow">→</span>
            {swap.minReceive} {swap.swapOutTicker}
          </span>
        );
      }
      const summary = getValue() as string;
      return <span className="cell-metadata" title={summary}>{summary}</span>;
    },
    size: 260,
  },
  {
    id: 'slot',
    header: 'Slot',
    accessorFn: (row) => row.cardanoSlot,
    cell: ({ getValue }) => <span className="cell-slot">{(getValue() as number)?.toLocaleString()}</span>,
    size: 120,
  },
  {
    id: 'block',
    header: 'Block',
    accessorFn: (row) => row.cardanoBlockHeight,
    cell: ({ getValue }) => <span className="cell-mono">{(getValue() as number)?.toLocaleString()}</span>,
    size: 110,
  },
  {
    id: 'fee',
    header: 'Fee (₳)',
    accessorFn: (row) => row.data?.transaction?.fee ?? 0,
    cell: ({ getValue }) => {
      const fee = getValue() as number;
      if (!fee) return <span className="cell-fee">—</span>;
      return <span className="cell-fee"><span className="ada-symbol">₳</span>{formatFee(fee)}</span>;
    },
    size: 110,
  },
  {
    id: 'outputs',
    header: 'Outputs',
    accessorFn: (row) => row.data?.transaction?.outputAddresses?.length ?? 0,
    cell: ({ row: r }) => {
      const addrs = r.original.data?.transaction?.outputAddresses;
      if (!addrs?.length) return <span className="cell-addresses">—</span>;
      const first = addrs[0];
      return (
        <span className="cell-addresses" title={addrs.join('\n')}>
          {first.slice(0, 16)}… {addrs.length > 1 && <span style={{ opacity: 0.5 }}>+{addrs.length - 1}</span>}
        </span>
      );
    },
    size: 200,
  },
];

type DemoTab = 'overview' | 'minswap' | 'feed' | 'consumers';

const DEMO_TABS: Array<{ id: DemoTab; label: string; hint: string }> = [
  { id: 'overview', label: 'Overview', hint: 'Live tip, stats, and block stream' },
  { id: 'minswap', label: 'Minswap Demo', hint: 'DEX haul visualisation for address-match filters' },
  { id: 'feed', label: 'Live Feed', hint: 'Sortable CloudEvents table for consumers' },
  { id: 'consumers', label: 'Consumers', hint: 'SSE, gRPC, and HTTP integration surfaces' },
];

function tabFromHash(): DemoTab {
  const hash = window.location.hash.replace(/^#/, '');
  if (hash === 'minswap' || hash === 'feed' || hash === 'consumers' || hash === 'overview') return hash;
  return 'overview';
}

/* ─── App Component ──────────────────────────── */
export default function App() {
  const urlDemo = new URLSearchParams(window.location.search).has('demo');
  const initialConfig = urlDemo
    ? (DEMO_RULE_CONFIGS.find(c => c.id === 'address') ?? DEMO_RULE_CONFIGS[0])
    : (DEMO_RULE_CONFIGS.find(c => c.ruleFilter === DEFAULT_RULE_FILTER) ?? DEMO_RULE_CONFIGS[0]);
  const [selectedConfigId, setSelectedConfigId] = useState(initialConfig.id);
  const selectedConfig = DEMO_RULE_CONFIGS.find(c => c.id === selectedConfigId) ?? DEMO_RULE_CONFIGS[0];

  const { events, stats, status, paused, connectionLogs, togglePause, clearEvents } =
    useEventStream(SSE_BASE_URL, selectedConfig.ruleFilter);
  const [sorting, setSorting] = useState<SortingState>([]);
  const [selectedEvent, setSelectedEvent] = useState<BlockchainEvent | null>(null);
  const [selectedBlockHeight, setSelectedBlockHeight] = useState<number | null>(null);
  const [globalFilter, setGlobalFilter] = useState('');
  const [logsExpanded, setLogsExpanded] = useState(false);
  const [demoTab, setDemoTab] = useState<DemoTab>(tabFromHash);
  const [convoyDemo, setConvoyDemo] = useState(urlDemo);

  const network = events[0]?.cardanoNetwork ?? 'mainnet';
  const tipHeight = events[0]?.cardanoBlockHeight;
  const tipSlot = events[0]?.cardanoSlot;

  const filteredEvents = useMemo(() => {
    if (selectedBlockHeight == null) return events;
    return events.filter(e => e.cardanoBlockHeight === selectedBlockHeight);
  }, [events, selectedBlockHeight]);

  const swapTrucks = useMemo(() => {
    type Cargo = { eventId: string; swap: NonNullable<ReturnType<typeof extractMinswapSwap>> };
    const byBlock = new Map<number, {
      blockHeight: number;
      blockHash?: string;
      slot?: number;
      swaps: Cargo[];
    }>();

    // Convoy always shows recent blocks from the full stream (not table filter).
    for (const event of events) {
      const swap = extractMinswapSwap(event);
      if (!swap || event.cardanoBlockHeight == null) continue;
      const height = event.cardanoBlockHeight;
      let bucket = byBlock.get(height);
      if (!bucket) {
        bucket = {
          blockHeight: height,
          blockHash: event.cardanoBlock,
          slot: event.cardanoSlot,
          swaps: [],
        };
        byBlock.set(height, bucket);
      }
      bucket.swaps.push({ eventId: event.id, swap });
    }

    return [...byBlock.values()]
      .sort((a, b) => b.blockHeight - a.blockHeight)
      .slice(0, 8)
      .map((truck, index) => ({
        ...truck,
        fresh: index === 0 && !paused,
      }));
  }, [events, paused]);

  // Synthetic haul convoy for close-out demos when live DEX flow is quiet.
  const [demoTrucks, setDemoTrucks] = useState<BlockTruckData[]>([]);

  useEffect(() => {
    if (!convoyDemo) {
      setDemoTrucks([]);
      return;
    }
    let height = 12_345_678;
    const tokens = ['MIN', 'SNEK', 'HOSKY', 'IAG', 'WMTX', 'NIGHT'];
    const makeSwap = (buy: boolean, height: number, i: number) => {
      const token = tokens[Math.floor(Math.random() * tokens.length)];
      return {
        eventId: `demo-${height}-${i}`,
        swap: {
          dex: 'Minswap V2',
          direction: buy ? 'BUY' : 'SELL',
          orderType: 'SwapExactIn',
          swapInTicker: buy ? 'ADA' : token,
          swapOutTicker: buy ? token : 'ADA',
          amountIn: (Math.random() * 900 + 50).toFixed(1),
          minReceive: (Math.random() * 90000 + 100).toFixed(0),
        },
      };
    };
    const makeTruck = () => {
      // Emit a buy haul and a sell haul so both lanes stay active in demos.
      height += 1;
      const buyHeight = height;
      height += 1;
      const sellHeight = height;
      const buySwaps = Array.from({ length: 1 + Math.floor(Math.random() * 3) }, (_, i) => makeSwap(true, buyHeight, i));
      const sellSwaps = Array.from({ length: 1 + Math.floor(Math.random() * 3) }, (_, i) => makeSwap(false, sellHeight, i));
      setDemoTrucks(prev => [
        { blockHeight: buyHeight, slot: buyHeight * 20, swaps: buySwaps, fresh: true },
        { blockHeight: sellHeight, slot: sellHeight * 20, swaps: sellSwaps, fresh: true },
        ...prev,
      ].slice(0, 8));
    };
    makeTruck();
    const timer = window.setInterval(makeTruck, 9000);
    return () => window.clearInterval(timer);
  }, [convoyDemo]);

  useEffect(() => {
    const onHash = () => setDemoTab(tabFromHash());
    window.addEventListener('hashchange', onHash);
    return () => window.removeEventListener('hashchange', onHash);
  }, []);

  const switchTab = useCallback((tab: DemoTab) => {
    setDemoTab(tab);
    window.history.replaceState(null, '', `#${tab}`);
  }, []);

  // Track EPS history for sparkline
  const epsHistory = useRef<number[]>([]);
  const lastUptime = useRef(0);

  useEffect(() => {
    if (stats.connectionUptime !== lastUptime.current) {
      lastUptime.current = stats.connectionUptime;
      epsHistory.current = [...epsHistory.current.slice(-59), stats.eventsPerSecond];
    }
  }, [stats.connectionUptime, stats.eventsPerSecond]);

  useEffect(() => {
    setSelectedBlockHeight(null);
  }, [selectedConfigId]);

  const table = useReactTable({
    data: filteredEvents,
    columns,
    state: { sorting, globalFilter },
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getRowId: (row) => row.id,
  });

  const sortedRules = useMemo(() => {
    return Object.entries(stats.ruleBreakdown)
      .sort((a, b) => b[1] - a[1]);
  }, [stats.ruleBreakdown]);

  const handleRuleConfigChange = useCallback((configId: string) => {
    setSelectedConfigId(configId);
    setSelectedEvent(null);
    setSelectedBlockHeight(null);
  }, []);

  const handleRowClick = useCallback((event: BlockchainEvent) => {
    setSelectedEvent(event);
  }, []);

  const handleSelectBlock = useCallback((height: number | null) => {
    setSelectedBlockHeight(height);
  }, []);

  const openMinswapDemo = useCallback(() => {
    handleRuleConfigChange('address');
    setConvoyDemo(true);
    switchTab('minswap');
  }, [handleRuleConfigChange, switchTab]);

  const rowCount = table.getRowModel().rows.length;
  const haulTrucks = convoyDemo ? demoTrucks : swapTrucks;
  const buyCount = useMemo(
    () => haulTrucks.reduce((n, t) => n + t.swaps.filter(s => s.swap.direction === 'BUY').length, 0),
    [haulTrucks],
  );
  const sellCount = useMemo(
    () => haulTrucks.reduce((n, t) => n + t.swaps.filter(s => s.swap.direction === 'SELL').length, 0),
    [haulTrucks],
  );

  return (
    <>
      {/* Animated mesh background */}
      <div className="mesh-bg" aria-hidden="true">
        <div className="orb orb-1" />
        <div className="orb orb-2" />
        <div className="orb orb-3" />
      </div>

      <div className={`app demo-shell tab-${demoTab}`}>
        {/* ─── Header ──────────────────────────── */}
        <div className="header">
          <div className="header-left">
            <div className="logo">
              <div className="logo-icon">
                <CardanoLogo />
              </div>
              <div className="logo-text">
                <span className="logo-title">Event Viewer</span>
                <span className="logo-subtitle">Cardano · Consumer Demo</span>
              </div>
            </div>
            <div className="header-separator" />
            <div className="instance-badge" data-instance={INSTANCE_ID.replace(/\D/g, '') || '0'}>
              <span className="instance-dot" />
              {INSTANCE_ID}
            </div>
            <div className="network-badge">
              <span className="network-dot" />
              {network.charAt(0).toUpperCase() + network.slice(1)}
            </div>
          </div>
          <div className="header-right">
            <div className="rule-filter-selector">
              {DEMO_RULE_CONFIGS.map(config => (
                <button
                  key={config.id}
                  className={`rule-filter-btn ${getRuleClass(config.label)} ${selectedConfigId === config.id ? 'active' : ''}`}
                  onClick={() => handleRuleConfigChange(config.id)}
                  title={config.description}
                >
                  {config.label}
                </button>
              ))}
            </div>
            {(demoTab === 'feed' || demoTab === 'overview') && (
              <div className="search-container">
                <input
                  type="text"
                  placeholder="Filter events…"
                  value={globalFilter}
                  onChange={e => setGlobalFilter(e.target.value)}
                  className="search-input"
                />
                <SearchIcon />
                <div className="search-kbd">
                  <kbd>⌘</kbd><kbd>K</kbd>
                </div>
              </div>
            )}
            <button
              className={`btn btn-pause ${paused ? 'paused' : ''}`}
              onClick={togglePause}
            >
              {paused ? '▶ Resume' : '⏸ Pause'}
            </button>
            <button className="btn" onClick={clearEvents}>Clear</button>
            <div className={`status-indicator ${status}`}>
              <div className={`status-dot ${status}`} />
              {status === 'connected' ? 'Live' : status === 'connecting' ? 'Connecting…' : 'Disconnected'}
            </div>
          </div>
        </div>

        {/* ─── Consumer demo tabs ──────────────── */}
        <nav className="demo-tabs" aria-label="Consumer demo views">
          <div className="demo-tabs-track">
            {DEMO_TABS.map(tab => (
              <button
                key={tab.id}
                type="button"
                className={`demo-tab ${demoTab === tab.id ? 'active' : ''}`}
                onClick={() => switchTab(tab.id)}
                title={tab.hint}
              >
                <span className="demo-tab-label">{tab.label}</span>
                <span className="demo-tab-hint">{tab.hint}</span>
              </button>
            ))}
          </div>
          <button type="button" className="demo-launch-minswap" onClick={openMinswapDemo}>
            Launch Minswap Demo
          </button>
        </nav>

        {/* ─── Connection Log ──────────────────── */}
        {(demoTab === 'overview' || demoTab === 'consumers') && (
          <div className={`connection-log-panel ${logsExpanded ? 'expanded' : 'collapsed'}`}>
            <button
              className="connection-log-toggle"
              onClick={() => setLogsExpanded(e => !e)}
              type="button"
            >
              <span>Connection Log</span>
              <span className="log-count">{connectionLogs.length}</span>
              <span className="log-chevron">{logsExpanded ? '▾' : '▸'}</span>
            </button>
            {logsExpanded && (
              <div className="connection-log-list">
                {connectionLogs.length === 0 ? (
                  <div className="connection-log-empty">No connection activity yet</div>
                ) : (
                  connectionLogs.map((log, i) => (
                    <div key={`${log.time}-${i}`} className={`connection-log-entry ${log.level}`}>
                      <span className="log-time">{formatTime(log.time)}</span>
                      <span className="log-message">{log.message}</span>
                    </div>
                  ))
                )}
              </div>
            )}
          </div>
        )}

        {/* ─── Overview / shared stats ─────────── */}
        {(demoTab === 'overview' || demoTab === 'feed') && (
          <div className="stats-bar">
            <div className="stat-card blue">
              <div className="stat-label">Events Received</div>
              <div className="stat-value blue">{stats.totalReceived.toLocaleString()}</div>
            </div>
            <div className="stat-card cyan has-sparkline">
              <div className="stat-label">Events / sec</div>
              <div className="stat-value cyan">{stats.eventsPerSecond.toFixed(1)}</div>
              <Sparkline data={epsHistory.current} color="rgba(34, 211, 238, 0.7)" />
            </div>
            <div className="stat-card amber tip-stat">
              <div className="stat-label">Chain Tip</div>
              <div className="stat-value amber">
                {tipHeight ? tipHeight.toLocaleString() : '—'}
              </div>
              <div className="stat-footnote">
                {tipSlot ? `slot ${tipSlot.toLocaleString()}` : 'awaiting tip'}
              </div>
            </div>
            <div className="stat-card green">
              <div className="stat-label">Uptime</div>
              <div className="stat-value green">{formatUptime(stats.connectionUptime)}</div>
            </div>
            <div className="stat-card rules">
              <div className="stat-label">Rule Breakdown</div>
              <div className="rule-chips">
                {sortedRules.map(([name, count]) => (
                  <div key={name} className={`rule-chip ${getRuleClass(name)}`}>
                    <span className="chip-dot" />
                    {name}
                    <span className="count">{count.toLocaleString()}</span>
                  </div>
                ))}
                {sortedRules.length === 0 && (
                  <span className="waiting-text">Waiting for events…</span>
                )}
              </div>
            </div>
          </div>
        )}

        {demoTab === 'overview' && (
          <>
            <section className="demo-hero-strip">
              <div>
                <p className="demo-eyebrow">Final close-out consumer showcase</p>
                <h2>Rule-filtered Cardano events → CloudEvents consumers</h2>
                <p>
                  Switch rule profiles, watch the tip advance, then open the Minswap haul to show
                  address-match DEX filtering in an interactive consumer-facing view.
                </p>
              </div>
              <div className="demo-hero-actions">
                <button type="button" className="btn btn-accent" onClick={openMinswapDemo}>
                  Open Minswap Haul
                </button>
                <button type="button" className="btn" onClick={() => switchTab('consumers')}>
                  View consumer APIs
                </button>
              </div>
            </section>
            <BlockStream
              events={events}
              selectedHeight={selectedBlockHeight}
              onSelectBlock={handleSelectBlock}
              live={status === 'connected' && !paused}
            />
            <div className="overview-preview-grid">
              <button type="button" className="overview-preview-card minswap" onClick={() => switchTab('minswap')}>
                <span className="overview-preview-kicker">Interactive</span>
                <strong>Minswap haul demo</strong>
                <span>{buyCount} buys · {sellCount} sells in convoy</span>
              </button>
              <button type="button" className="overview-preview-card feed" onClick={() => switchTab('feed')}>
                <span className="overview-preview-kicker">SSE feed</span>
                <strong>Live CloudEvents table</strong>
                <span>{rowCount.toLocaleString()} events in buffer view</span>
              </button>
              <button type="button" className="overview-preview-card consumers" onClick={() => switchTab('consumers')}>
                <span className="overview-preview-kicker">Integrate</span>
                <strong>HTTP · gRPC · SSE</strong>
                <span>Copy-ready endpoints for reviewers</span>
              </button>
            </div>
          </>
        )}

        {demoTab === 'minswap' && (
          <section className="minswap-tab">
            <div className="minswap-tab-banner">
              <div>
                <p className="demo-eyebrow">Address-match consumer story</p>
                <h2>Minswap V2 batched swap haul</h2>
                <p>
                  Filtered `address-match` events are decoded into buy/sell cargo and driven across
                  dual lanes — a visual proof that consumers can react to DEX flow without indexing the full chain.
                </p>
              </div>
              <div className="minswap-tab-controls">
                <div className="minswap-kpi">
                  <span>Buys</span>
                  <strong>{buyCount}</strong>
                </div>
                <div className="minswap-kpi sell">
                  <span>Sells</span>
                  <strong>{sellCount}</strong>
                </div>
                <label className={`convoy-toggle ${convoyDemo ? 'on' : ''}`}>
                  <input
                    type="checkbox"
                    checked={convoyDemo}
                    onChange={e => {
                      const on = e.target.checked;
                      setConvoyDemo(on);
                      if (on) handleRuleConfigChange('address');
                    }}
                  />
                  Demo convoy
                </label>
                {selectedConfigId !== 'address' && (
                  <button type="button" className="btn btn-accent" onClick={() => handleRuleConfigChange('address')}>
                    Use Address Match
                  </button>
                )}
              </div>
            </div>
            <MinswapSwapFeed
              trucks={haulTrucks}
              onSelectBlock={(height) => {
                handleSelectBlock(height);
                switchTab('feed');
              }}
              onSelectSwap={(id) => {
                const event = events.find(e => e.id === id);
                if (event) setSelectedEvent(event);
              }}
            />
          </section>
        )}

        {demoTab === 'feed' && (
          <>
            <BlockStream
              events={events}
              selectedHeight={selectedBlockHeight}
              onSelectBlock={handleSelectBlock}
              live={status === 'connected' && !paused}
            />
            <div className="table-wrapper">
              {filteredEvents.length === 0 ? (
                <div className="table-container">
                  <div className="empty-state">
                    <div className="empty-state-icon">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M12 2L2 7l10 5 10-5-10-5z" />
                        <path d="M2 17l10 5 10-5" />
                        <path d="M2 12l10 5 10-5" />
                      </svg>
                    </div>
                    <div className="empty-state-text">
                      <div className="title">
                        {events.length === 0
                          ? 'Waiting for blockchain events'
                          : `No events in block ${selectedBlockHeight?.toLocaleString()}`}
                      </div>
                      <div className="subtitle">
                        {events.length === 0 ? (
                          <>Filter: <strong>{selectedConfig.label}</strong> — {selectedConfig.description}</>
                        ) : (
                          <>Select another block in the stream, or clear the block filter.</>
                        )}
                      </div>
                    </div>
                    <div className="empty-state-pulse">
                      <div className="dot" />
                      <div className="dot" />
                      <div className="dot" />
                    </div>
                  </div>
                </div>
              ) : (
                <>
                  <div className="table-container">
                    <table>
                      <thead>
                        {table.getHeaderGroups().map(headerGroup => (
                          <tr key={headerGroup.id}>
                            {headerGroup.headers.map(header => (
                              <th
                                key={header.id}
                                onClick={header.column.getToggleSortingHandler()}
                                style={{ width: header.getSize() }}
                              >
                                {flexRender(header.column.columnDef.header, header.getContext())}
                                <span className="sort-indicator">
                                  {{ asc: ' ↑', desc: ' ↓' }[header.column.getIsSorted() as string] ?? ''}
                                </span>
                              </th>
                            ))}
                          </tr>
                        ))}
                      </thead>
                      <tbody>
                        {table.getRowModel().rows.map((row, i) => {
                          const height = row.original.cardanoBlockHeight;
                          const inSelectedBlock = selectedBlockHeight != null && height === selectedBlockHeight;
                          const rowClass = [
                            i < 3 && sorting.length === 0 && !paused ? 'new-row' : '',
                            inSelectedBlock ? 'block-focus' : '',
                          ].filter(Boolean).join(' ');
                          return (
                          <tr
                            key={row.id}
                            className={rowClass}
                            onClick={() => handleRowClick(row.original)}
                          >
                            {row.getVisibleCells().map(cell => (
                              <td key={cell.id}>
                                {flexRender(cell.column.columnDef.cell, cell.getContext())}
                              </td>
                            ))}
                          </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                  <div className="table-footer">
                    <div className="row-count">
                      {status === 'connected' && !paused && <span className="live-dot" />}
                      {rowCount.toLocaleString()} event{rowCount !== 1 ? 's' : ''} displayed
                      {selectedBlockHeight != null && (
                        <span className="footer-block-filter"> · block {selectedBlockHeight.toLocaleString()}</span>
                      )}
                      {paused && <span style={{ color: 'var(--accent-amber)' }}> — paused</span>}
                    </div>
                    <span>Buffer: {events.length} / 500</span>
                  </div>
                </>
              )}
            </div>
          </>
        )}

        {demoTab === 'consumers' && (
          <section className="consumers-tab">
            <div className="demo-hero-strip compact">
              <div>
                <p className="demo-eyebrow">Same CloudEvent · three delivery surfaces</p>
                <h2>Plug-in consumers without coupling to the indexer</h2>
                <p>
                  Reviewers can subscribe via SSE for dashboards, pull via gRPC for services, or
                  receive Dapr/HTTP pub-sub deliveries — all from the Milestone 2 delivery layer.
                </p>
              </div>
            </div>
            <div className="consumer-card-grid">
              <article className="consumer-card">
                <header>
                  <span className="consumer-proto">SSE</span>
                  <strong>Event Viewer / browsers</strong>
                </header>
                <code>{SSE_BASE_URL}{selectedConfig.ruleFilter ? `?ruleFilter=${selectedConfig.ruleFilter}` : ''}</code>
                <p>Live stream used by this UI. Filter with <code>ruleFilter</code> for plug-and-play scenarios.</p>
                <button type="button" className="btn" onClick={() => navigator.clipboard?.writeText(
                  selectedConfig.ruleFilter
                    ? `${SSE_BASE_URL}?ruleFilter=${selectedConfig.ruleFilter}`
                    : SSE_BASE_URL,
                )}>Copy stream URL</button>
              </article>
              <article className="consumer-card">
                <header>
                  <span className="consumer-proto grpc">gRPC</span>
                  <strong>Service consumers</strong>
                </header>
                <code>localhost:4010 · BlockchainEvents.Subscribe</code>
                <p>Typed streaming RPC for backend workers. Same CloudEvents payload as SSE/HTTP.</p>
                <button type="button" className="btn" onClick={() => switchTab('feed')}>Inspect live payloads</button>
              </article>
              <article className="consumer-card">
                <header>
                  <span className="consumer-proto http">HTTP</span>
                  <strong>Dapr pub-sub / webhooks</strong>
                </header>
                <code>POST /dapr/subscribe · topic blockchain-events</code>
                <p>Redis Streams + Dapr delivers durable fan-out with retry/DLQ from Milestone 2.</p>
                <button type="button" className="btn" onClick={openMinswapDemo}>See DEX filter story</button>
              </article>
            </div>
            <div className="consumer-snippet">
              <div className="consumer-snippet-head">
                <span>Quick start · curl SSE</span>
                <button
                  type="button"
                  className="btn"
                  onClick={() => navigator.clipboard?.writeText(
                    `curl -N "${SSE_BASE_URL}${selectedConfig.ruleFilter ? `?ruleFilter=${selectedConfig.ruleFilter}` : ''}"`,
                  )}
                >
                  Copy
                </button>
              </div>
              <pre>{`curl -N "${SSE_BASE_URL}${selectedConfig.ruleFilter ? `?ruleFilter=${selectedConfig.ruleFilter}` : ''}"`}</pre>
            </div>
          </section>
        )}

        {/* ─── Detail Drawer ───────────────────── */}
        {selectedEvent && (
          <EventDetailDrawer
            event={selectedEvent}
            onClose={() => setSelectedEvent(null)}
          />
        )}
      </div>
    </>
  );
}
