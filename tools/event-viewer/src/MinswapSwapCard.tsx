import { useEffect, useMemo, useRef, useState } from 'react';

export interface MinswapSwapView {
  dex?: string;
  direction?: string;
  orderType?: string;
  swapInTicker?: string;
  swapOutTicker?: string;
  amountIn?: string;
  minReceive?: string;
  batcherFeeAda?: string;
  datumSource?: string;
}

export function extractMinswapSwap(event: {
  data?: {
    matchedCriteria?: Record<string, unknown> | null;
    transaction?: { minswapSwap?: MinswapSwapView | null } | null;
  } | null;
}): MinswapSwapView | null {
  const fromTx = event.data?.transaction?.minswapSwap;
  if (fromTx?.swapInTicker && fromTx?.swapOutTicker) return fromTx;

  const raw = event.data?.matchedCriteria?.minswap_swap;
  if (raw && typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    return {
      dex: String(o.dex ?? 'Minswap V2'),
      direction: String(o.direction ?? 'SWAP'),
      orderType: String(o.orderType ?? 'SwapExactIn'),
      swapInTicker: String(o.swapInTicker ?? '?'),
      swapOutTicker: String(o.swapOutTicker ?? '?'),
      amountIn: String(o.amountIn ?? '—'),
      minReceive: String(o.minReceive ?? '—'),
      batcherFeeAda: o.batcherFeeAda != null ? String(o.batcherFeeAda) : undefined,
      datumSource: o.datumSource != null ? String(o.datumSource) : undefined,
    };
  }
  return null;
}

const TOKEN_THEME: Record<string, { bg: string; fg: string }> = {
  ADA: { bg: '#3b82f6', fg: '#eff6ff' },
  MIN: { bg: '#14b8a6', fg: '#042f2e' },
  SNEK: { bg: '#f59e0b', fg: '#451a03' },
  WMTX: { bg: '#a78bfa', fg: '#2e1065' },
  HOSKY: { bg: '#fb7185', fg: '#4c0519' },
  IAG: { bg: '#38bdf8', fg: '#0c4a6e' },
  NIGHT: { bg: '#818cf8', fg: '#1e1b4b' },
  USDCX: { bg: '#4ade80', fg: '#052e16' },
  USDCx: { bg: '#4ade80', fg: '#052e16' },
  TOKEN: { bg: '#94a3b8', fg: '#0f172a' },
};

function tokenTheme(ticker: string) {
  if (TOKEN_THEME[ticker]) return TOKEN_THEME[ticker];
  const key = ticker.toUpperCase();
  if (TOKEN_THEME[key]) return TOKEN_THEME[key];
  let h = 0;
  for (let i = 0; i < ticker.length; i++) h = (h * 33 + ticker.charCodeAt(i)) % 360;
  return { bg: `hsl(${h} 70% 55%)`, fg: '#0f172a' };
}

function directionLabel(dir: string): string {
  switch (dir.toUpperCase()) {
    case 'BUY': return 'Buy';
    case 'SELL': return 'Sell';
    default: return 'Swap';
  }
}

function dirClass(dir?: string): string {
  return (dir ?? 'SWAP').toLowerCase();
}

/** Compact summary for the event detail drawer. */
export function MinswapSwapCard({ swap }: { swap: MinswapSwapView; compact?: boolean; fresh?: boolean }) {
  const inTheme = tokenTheme(swap.swapInTicker ?? '?');
  const outTheme = tokenTheme(swap.swapOutTicker ?? '?');
  return (
    <div className={`swap-mini dir-${dirClass(swap.direction)}`}>
      <span className="swap-mini-dir">{directionLabel(swap.direction ?? 'SWAP')}</span>
      <span className="swap-mini-chip" style={{ background: inTheme.bg, color: inTheme.fg }}>
        {swap.amountIn} {swap.swapInTicker}
      </span>
      <span className="swap-mini-arrow" aria-hidden="true">→</span>
      <span className="swap-mini-chip" style={{ background: outTheme.bg, color: outTheme.fg }}>
        {swap.minReceive} {swap.swapOutTicker}
      </span>
    </div>
  );
}

export interface BlockTruckCargo {
  eventId: string;
  swap: MinswapSwapView;
}

export type TruckLane = 'buy' | 'sell';

export interface BlockTruckData {
  blockHeight: number;
  blockHash?: string;
  slot?: number;
  fresh?: boolean;
  swaps: BlockTruckCargo[];
}

interface LaneTruck extends BlockTruckData {
  lane: TruckLane;
  key: string;
}

type JourneyPhase = 'approaching' | 'inspecting' | 'departing';
type FleetSize = 'van' | 'lorry' | 'artic';

function normalizeDir(dir?: string): 'BUY' | 'SELL' | 'SWAP' {
  const d = (dir ?? 'SWAP').toUpperCase();
  if (d === 'BUY' || d === 'SELL') return d;
  return 'SWAP';
}

function fleetSizeFor(swapCount: number): FleetSize {
  if (swapCount >= 3) return 'artic';
  if (swapCount === 2) return 'lorry';
  return 'van';
}

function fleetLabel(size: FleetSize): string {
  switch (size) {
    case 'artic': return 'Heavy lorry';
    case 'lorry': return 'Lorry';
    default: return 'Van';
  }
}

const FLEET: Record<FleetSize, {
  viewW: number;
  trailerX: number;
  trailerW: number;
  trailerH: number;
  doorH: number;
  shadowRx: number;
  wheels: number[];
  crates: number;
  shellCssW: number;
}> = {
  van: {
    viewW: 240,
    trailerX: 78,
    trailerW: 128,
    trailerH: 58,
    doorH: 58,
    shadowRx: 78,
    wheels: [40, 170],
    crates: 2,
    shellCssW: 260,
  },
  lorry: {
    viewW: 300,
    trailerX: 78,
    trailerW: 188,
    trailerH: 68,
    doorH: 68,
    shadowRx: 104,
    wheels: [40, 130, 230],
    crates: 3,
    shellCssW: 320,
  },
  artic: {
    viewW: 400,
    trailerX: 78,
    trailerW: 288,
    trailerH: 78,
    doorH: 78,
    shadowRx: 150,
    wheels: [40, 150, 220, 290, 340],
    crates: 5,
    shellCssW: 420,
  },
};

const CRATE_COLORS = ['#f8fafc', '#fde68a', '#a7f3d0', '#c4b5fd', '#fda4af'];

/** Keep in sync with `.journey-shell` animation duration in App.css */
const JOURNEY_MS = 18000;
const CARGO_OPEN_AT = 5000;
const CARGO_CLOSE_AT = 11800;
const MAX_QUEUE = 5;

function Wheel({ x, y = 108, r = 14 }: { x: number; y?: number; r?: number }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <g className="truck-wheel">
        <circle r={r} fill="#1e293b" stroke="#0f172a" strokeWidth="3" />
        <circle r={r * 0.5} fill="#94a3b8" stroke="#0f172a" strokeWidth="2" />
        <path d={`M0-${r * 0.5}v${r}M-${r * 0.5} 0h${r}`} stroke="#0f172a" strokeWidth="2" />
      </g>
    </g>
  );
}

function CartoonTruckArt({
  open,
  blockHeight,
  swapCount,
  lane,
  size,
}: {
  open: boolean;
  blockHeight: number;
  swapCount: number;
  lane: TruckLane;
  size: FleetSize;
}) {
  const fleet = FLEET[size];
  const colors = lane === 'buy'
    ? { body: '#2dd4bf', cabin: '#0f766e', cabinDark: '#115e59', accent: '#fbbf24', door: '#134e4a', badge: '#ecfdf5' }
    : { body: '#fb7185', cabin: '#e11d48', cabinDark: '#9f1239', accent: '#fde047', door: '#881337', badge: '#fff1f2' };
  const facingWest = lane === 'sell';
  const laneWord = lane === 'buy' ? 'BUY' : 'SELL';
  const midX = fleet.viewW / 2;
  const trailerY = size === 'artic' ? 28 : 36;
  const doorW = fleet.trailerW / 2;
  const badgeX = fleet.trailerX + fleet.trailerW - 18;
  const wheelR = size === 'van' ? 12 : size === 'artic' ? 15 : 14;

  return (
    <svg
      className={`cartoon-truck-svg lane-${lane} size-${size} ${open ? 'doors-open' : ''}`}
      viewBox={`0 0 ${fleet.viewW} 140`}
      role="img"
      aria-label={`Block ${blockHeight} ${laneWord} ${fleetLabel(size).toLowerCase()} with ${swapCount} Minswap swaps`}
    >
      <defs>
        <linearGradient id={`body-${lane}-${size}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={colors.body} />
          <stop offset="100%" stopColor={lane === 'buy' ? '#0d9488' : '#e11d48'} />
        </linearGradient>
        <linearGradient id={`cabin-${lane}-${size}`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor={colors.cabin} />
          <stop offset="100%" stopColor={colors.cabinDark} />
        </linearGradient>
        <filter id="truckSoftShadow" x="-20%" y="-20%" width="140%" height="140%">
          <feDropShadow dx="0" dy="3" stdDeviation="2" floodOpacity="0.35" />
        </filter>
      </defs>

      <text x={midX} y="14" textAnchor="middle" className="truck-svg-block-label">
        #{blockHeight} · {laneWord}
        {size === 'artic' ? ' · HEAVY HAUL' : size === 'van' ? ' · EXPRESS' : ''}
      </text>

      <g transform={facingWest ? `translate(${fleet.viewW},0) scale(-1,1)` : undefined} filter="url(#truckSoftShadow)">
        <ellipse className="truck-ground-shadow" cx={midX} cy="128" rx={fleet.shadowRx} ry="7" fill="rgba(15,23,42,0.35)" />

        <g className="truck-bounce-group">
          <rect
            x={fleet.trailerX}
            y={trailerY}
            width={fleet.trailerW}
            height={fleet.trailerH}
            rx="14"
            fill={`url(#body-${lane}-${size})`}
            stroke="#0f172a"
            strokeWidth="3"
          />
          <rect
            x={fleet.trailerX + 8}
            y={trailerY + 8}
            width={fleet.trailerW - 16}
            height={fleet.trailerH - 28}
            rx="8"
            fill="rgba(255,255,255,0.28)"
          />

          <g className={`truck-svg-cargo ${open ? 'is-open' : ''}`}>
            {Array.from({ length: fleet.crates }, (_, i) => {
              const cx = fleet.trailerX + 22 + i * 34;
              const cy = trailerY + (size === 'artic' && i % 2 === 1 ? 16 : 20);
              return (
                <g key={i} className="truck-crate-pop" style={{ animationDelay: `${i * 70}ms` }}>
                  <rect
                    x={cx}
                    y={cy}
                    width="28"
                    height={size === 'artic' ? 30 : 26}
                    rx="5"
                    fill={CRATE_COLORS[i % CRATE_COLORS.length]}
                    stroke="#0f172a"
                    strokeWidth="2"
                  />
                  <text x={cx + 14} y={cy + 18} textAnchor="middle" fontSize="9" fontWeight="800" fill="#0f172a">
                    {i === 0 ? '₳' : '◆'}
                  </text>
                </g>
              );
            })}
          </g>

          <g className={`truck-svg-door left ${open ? 'open' : ''}`}>
            <rect x={fleet.trailerX} y={trailerY} width={doorW} height={fleet.doorH} rx="12" fill={colors.door} stroke="#0f172a" strokeWidth="3" />
            <rect x={fleet.trailerX + 10} y={trailerY + 12} width={doorW - 22} height={fleet.doorH - 24} rx="6" fill="rgba(255,255,255,0.08)" />
            <circle cx={fleet.trailerX + doorW - 10} cy={trailerY + fleet.doorH / 2} r="4" fill={colors.accent} stroke="#0f172a" strokeWidth="1.5" />
          </g>
          <g className={`truck-svg-door right ${open ? 'open' : ''}`}>
            <rect x={fleet.trailerX + doorW} y={trailerY} width={doorW} height={fleet.doorH} rx="12" fill={colors.door} stroke="#0f172a" strokeWidth="3" />
            <rect x={fleet.trailerX + doorW + 12} y={trailerY + 12} width={doorW - 22} height={fleet.doorH - 24} rx="6" fill="rgba(255,255,255,0.08)" />
            <circle cx={fleet.trailerX + doorW + 10} cy={trailerY + fleet.doorH / 2} r="4" fill={colors.accent} stroke="#0f172a" strokeWidth="1.5" />
          </g>

          <rect
            x={fleet.trailerX + 18}
            y={trailerY + fleet.trailerH - 14}
            width={fleet.trailerW - 36}
            height="8"
            rx="3"
            fill={colors.accent}
            stroke="#0f172a"
            strokeWidth="1.5"
          />

          {size === 'artic' && (
            <>
              <rect x={fleet.trailerX + 18} y={trailerY + 4} width={fleet.trailerW - 36} height="5" rx="2" fill={colors.accent} stroke="#0f172a" strokeWidth="1" />
              {[0, 1, 2, 3, 4].map(i => (
                <circle key={i} className="roof-light" cx={fleet.trailerX + 40 + i * 50} cy={trailerY - 4} r="3.5" fill="#fde047" stroke="#0f172a" strokeWidth="1.5" style={{ animationDelay: `${i * 120}ms` }} />
              ))}
            </>
          )}

          <path
            d="M18 56h54c6 0 10 4 12 10l8 28c1 4-2 8-6 8H18c-5 0-8-4-8-8V64c0-5 3-8 8-8z"
            fill={`url(#cabin-${lane}-${size})`}
            stroke="#0f172a"
            strokeWidth="3"
          />
          <path d="M34 62h30c3 0 5 2 6 5l4 14H30l2-14c1-3 2-5 4-5z" fill="#7dd3fc" stroke="#0f172a" strokeWidth="2.5" />
          <path d="M36 66h18" stroke="rgba(255,255,255,0.7)" strokeWidth="2" strokeLinecap="round" />
          {/* cute driver eyes */}
          <circle cx="42" cy="76" r="2.4" fill="#0f172a" />
          <circle cx="52" cy="76" r="2.4" fill="#0f172a" />
          <circle cx="42.6" cy="75.4" r="0.7" fill="#fff" />
          <circle cx="52.6" cy="75.4" r="0.7" fill="#fff" />
          <rect x="12" y="94" width="40" height="10" rx="4" fill="#e2e8f0" stroke="#0f172a" strokeWidth="2" />
          <path d="M20 98c4 3 12 3 16 0" fill="none" stroke="#0f172a" strokeWidth="2" strokeLinecap="round" />
          <ellipse cx="16" cy="78" rx="5" ry="4" fill="#fde047" stroke="#0f172a" strokeWidth="2" className="truck-headlight" />
          <g className="truck-exhaust" aria-hidden="true">
            <circle className="puff p1" cx="10" cy="88" r="3" fill="rgba(226,232,240,0.5)" />
            <circle className="puff p2" cx="4" cy="82" r="4" fill="rgba(226,232,240,0.35)" />
            <circle className="puff p3" cx="0" cy="74" r="5" fill="rgba(226,232,240,0.2)" />
          </g>

          {fleet.wheels.map(x => (
            <Wheel key={x} x={x} r={wheelR} />
          ))}

          <circle cx={badgeX} cy="42" r={size === 'artic' ? 18 : 16} fill={colors.badge} stroke="#0f172a" strokeWidth="2.5" />
        </g>
      </g>

      <text
        x={facingWest ? fleet.viewW - badgeX : badgeX}
        y="47"
        textAnchor="middle"
        fontSize={size === 'artic' ? 14 : 13}
        fontWeight="800"
        fill="#0f172a"
      >
        {swapCount}
      </text>
    </svg>
  );
}

function SwapParcel({
  swap,
  onClick,
  index,
}: {
  swap: MinswapSwapView;
  onClick?: () => void;
  index: number;
}) {
  const inTheme = tokenTheme(swap.swapInTicker ?? '?');
  const outTheme = tokenTheme(swap.swapOutTicker ?? '?');
  return (
    <button
      type="button"
      className={`swap-parcel dir-${dirClass(swap.direction)}`}
      onClick={onClick}
      style={{ animationDelay: `${index * 80}ms` }}
    >
      <span className="swap-parcel-dir">{directionLabel(swap.direction ?? 'SWAP')}</span>
      <span className="swap-parcel-leg">
        <span className="swap-parcel-mark" style={{ background: inTheme.bg, color: inTheme.fg }}>
          {(swap.swapInTicker ?? '?').slice(0, 3)}
        </span>
        <span className="swap-parcel-amt">{swap.amountIn}</span>
      </span>
      <span className="swap-parcel-arrow" aria-hidden="true">→</span>
      <span className="swap-parcel-leg">
        <span className="swap-parcel-mark" style={{ background: outTheme.bg, color: outTheme.fg }}>
          {(swap.swapOutTicker ?? '?').slice(0, 3)}
        </span>
        <span className="swap-parcel-amt receive">{swap.minReceive}</span>
      </span>
    </button>
  );
}

function InspectManifest({
  truck,
  onSelectBlock,
  onSelectSwap,
}: {
  truck: LaneTruck;
  onSelectBlock?: (height: number) => void;
  onSelectSwap?: (eventId: string) => void;
}) {
  const size = fleetSizeFor(truck.swaps.length);
  const laneLabel = truck.lane === 'buy' ? 'Buy' : 'Sell';

  return (
    <div className={`inspect-manifest lane-${truck.lane} size-${size}`}>
      <div className="inspect-manifest-head">
        <button type="button" className="inspect-block-btn" onClick={() => onSelectBlock?.(truck.blockHeight)}>
          Block #{truck.blockHeight.toLocaleString()}
        </button>
        <span className={`inspect-fleet-chip size-${size}`}>{fleetLabel(size)}</span>
        <span className="inspect-lane-chip">{laneLabel} haul · {truck.swaps.length}</span>
      </div>
      <div className="inspect-manifest-body">
        {truck.swaps.map((item, index) => (
          <SwapParcel
            key={item.eventId}
            swap={item.swap}
            index={index}
            onClick={() => onSelectSwap?.(item.eventId)}
          />
        ))}
      </div>
      <div className="inspect-manifest-hint">Tap a parcel for full event details</div>
    </div>
  );
}

function BlockTruck({
  truck,
  open,
  phase,
  onSelectBlock,
}: {
  truck: LaneTruck;
  open: boolean;
  phase: JourneyPhase;
  onSelectBlock?: (height: number) => void;
}) {
  const laneLabel = truck.lane === 'buy' ? 'Buys' : 'Sells';
  const size = fleetSizeFor(truck.swaps.length);
  const shellW = FLEET[size].shellCssW;

  return (
    <article
      className={[
        'block-truck',
        `lane-${truck.lane}`,
        `size-${size}`,
        `phase-${phase}`,
        open ? 'is-open' : '',
        'is-journeying',
      ].filter(Boolean).join(' ')}
      style={{ width: shellW }}
    >
      <button
        type="button"
        className="block-truck-hit"
        onClick={() => onSelectBlock?.(truck.blockHeight)}
        aria-expanded={open}
        title={`${fleetLabel(size)} · ${laneLabel} in block ${truck.blockHeight}`}
      >
        <CartoonTruckArt
          open={open}
          blockHeight={truck.blockHeight}
          swapCount={truck.swaps.length}
          lane={truck.lane}
          size={size}
        />
        <div className="block-truck-caption">
          <span className="block-truck-height">#{truck.blockHeight.toLocaleString()}</span>
          <span className={`block-truck-meta size-${size}`}>
            {phase === 'inspecting' ? 'Cargo open' : phase === 'approaching' ? 'Incoming' : 'Departing'}
            {' · '}
            {fleetLabel(size)}
          </span>
        </div>
      </button>
    </article>
  );
}

function useLaneJourney(incoming: LaneTruck[]) {
  const queueRef = useRef<LaneTruck[]>([]);
  const seenRef = useRef<Set<string>>(new Set());
  const [current, setCurrent] = useState<LaneTruck | null>(null);
  const [cargoOpen, setCargoOpen] = useState(false);
  const [phase, setPhase] = useState<JourneyPhase>('approaching');
  const [queued, setQueued] = useState(0);
  const [kick, setKick] = useState(0);
  const [lastPassed, setLastPassed] = useState<LaneTruck | null>(null);

  useEffect(() => {
    if (incoming.length === 0) return;

    if (seenRef.current.size === 0) {
      for (const truck of incoming) seenRef.current.add(truck.key);
      queueRef.current.push(incoming[0]);
      setQueued(queueRef.current.length);
      setKick(k => k + 1);
      return;
    }

    const fresh = incoming.filter(t => !seenRef.current.has(t.key)).reverse();
    if (fresh.length === 0) return;

    for (const truck of fresh) {
      seenRef.current.add(truck.key);
      if (queueRef.current.length >= MAX_QUEUE) queueRef.current.shift();
      queueRef.current.push(truck);
    }
    setQueued(queueRef.current.length);
    setKick(k => k + 1);
  }, [incoming]);

  useEffect(() => {
    if (current) return;
    const next = queueRef.current.shift() ?? null;
    setQueued(queueRef.current.length);
    if (!next) return;
    setCargoOpen(false);
    setPhase('approaching');
    setCurrent(next);
  }, [current, kick]);

  useEffect(() => {
    if (!current) return;
    setCargoOpen(false);
    setPhase('approaching');

    const openT = window.setTimeout(() => {
      setCargoOpen(true);
      setPhase('inspecting');
    }, CARGO_OPEN_AT);
    const closeT = window.setTimeout(() => {
      setCargoOpen(false);
      setPhase('departing');
    }, CARGO_CLOSE_AT);
    const doneT = window.setTimeout(() => {
      setLastPassed(current);
      setCurrent(null);
    }, JOURNEY_MS);

    return () => {
      window.clearTimeout(openT);
      window.clearTimeout(closeT);
      window.clearTimeout(doneT);
    };
  }, [current]);

  return { current, cargoOpen, phase, queued, lastPassed };
}

function splitIntoLanes(trucks: BlockTruckData[]): { buys: LaneTruck[]; sells: LaneTruck[] } {
  const buys: LaneTruck[] = [];
  const sells: LaneTruck[] = [];

  for (const truck of trucks) {
    const buySwaps = truck.swaps.filter(s => normalizeDir(s.swap.direction) === 'BUY');
    const sellSwaps = truck.swaps.filter(s => normalizeDir(s.swap.direction) === 'SELL');
    const swapSwaps = truck.swaps.filter(s => normalizeDir(s.swap.direction) === 'SWAP');

    if (buySwaps.length + swapSwaps.length > 0) {
      buys.push({
        ...truck,
        lane: 'buy',
        key: `buy-${truck.blockHeight}`,
        swaps: [...buySwaps, ...swapSwaps],
      });
    }
    if (sellSwaps.length > 0) {
      sells.push({
        ...truck,
        lane: 'sell',
        key: `sell-${truck.blockHeight}`,
        swaps: sellSwaps,
      });
    }
  }

  return { buys, sells };
}

interface FeedProps {
  trucks: BlockTruckData[];
  onSelectBlock?: (height: number) => void;
  onSelectSwap?: (eventId: string) => void;
}

function LaneStage({
  lane,
  incoming,
  orderCount,
  onSelectBlock,
  onSelectSwap,
}: {
  lane: TruckLane;
  incoming: LaneTruck[];
  orderCount: number;
  onSelectBlock?: (height: number) => void;
  onSelectSwap?: (eventId: string) => void;
}) {
  const { current, cargoOpen, phase, queued, lastPassed } = useLaneJourney(incoming);
  const isBuy = lane === 'buy';
  const size = current ? fleetSizeFor(current.swaps.length) : 'lorry';
  const shellW = FLEET[size].shellCssW;

  return (
    <div className={`highway-lane lane-${lane} phase-${current ? phase : 'idle'}`}>
      <div className="lane-banner">
        <span className="lane-arrow" aria-hidden="true">{isBuy ? '←' : '→'}</span>
        <span className="lane-name">
          {isBuy ? 'Buy lane' : 'Sell lane'}
        </span>
        <span className={`lane-phase-chip ${current ? phase : 'idle'}`}>
          {!current ? 'Standby' : phase === 'approaching' ? 'Incoming' : phase === 'inspecting' ? 'Inspecting' : 'Clearing'}
        </span>
        <span className="lane-count">
          {orderCount} orders{queued > 0 ? ` · ${queued} queued` : ''}
        </span>
      </div>

      <div className="lane-stage">
        <div className="lane-scenery" aria-hidden="true">
          <span className="hill h1" />
          <span className="hill h2" />
          <span className="mile-marker m1">km</span>
          <span className="mile-marker m2">km</span>
          <span className="bird b1" />
          <span className="bird b2" />
        </div>

        <div className="swap-road-dashes" aria-hidden="true" />
        <div className={`lane-spotlight ${cargoOpen ? 'is-hot' : ''}`} aria-hidden="true">
          <span className="spotlight-arch">CHECKPOINT</span>
        </div>

        {current && (phase === 'approaching' || phase === 'departing') && (
          <div className={`speed-lines journey-${isBuy ? 'rtl' : 'ltr'}`} aria-hidden="true" />
        )}

        {current ? (
          <div
            key={current.key}
            className={`journey-shell journey-${isBuy ? 'rtl' : 'ltr'} size-${size} phase-${phase}`}
            style={{ width: shellW, marginLeft: -shellW / 2 }}
          >
            <div className="dust-cloud" aria-hidden="true" />
            <BlockTruck
              truck={current}
              open={cargoOpen}
              phase={phase}
              onSelectBlock={onSelectBlock}
            />
          </div>
        ) : (
          <div className="lane-idle">
            <span className="lane-idle-title">Lane clear</span>
            <span className="lane-idle-sub">
              {lastPassed
                ? `Last haul · block #${lastPassed.blockHeight.toLocaleString()}`
                : `Waiting for the next ${isBuy ? 'buy' : 'sell'} block…`}
            </span>
          </div>
        )}

        {cargoOpen && current && (
          <InspectManifest
            truck={current}
            onSelectBlock={onSelectBlock}
            onSelectSwap={onSelectSwap}
          />
        )}
      </div>
    </div>
  );
}

export function MinswapSwapFeed({ trucks, onSelectBlock, onSelectSwap }: FeedProps) {
  const { buys, sells } = useMemo(() => splitIntoLanes(trucks), [trucks]);
  const buyCount = buys.reduce((n, t) => n + t.swaps.length, 0);
  const sellCount = sells.reduce((n, t) => n + t.swaps.length, 0);
  const latestPair = useMemo(() => {
    const newest = trucks[0]?.swaps[0]?.swap;
    if (!newest) return null;
    return `${newest.swapInTicker} → ${newest.swapOutTicker}`;
  }, [trucks]);

  if (buys.length === 0 && sells.length === 0) return null;

  return (
    <section className="swap-convoy" aria-label="Minswap buy and sell delivery lanes">
      <div className="swap-convoy-header">
        <div className="swap-convoy-brand">
          <span className="swap-convoy-icon" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
              <path d="M1 16V8h11v8" />
              <path d="M12 11h4l3 3v2h-7" />
              <circle cx="5.5" cy="17.5" r="1.8" />
              <circle cx="16.5" cy="17.5" r="1.8" />
              <path d="M1 13h11" />
            </svg>
          </span>
          <div>
            <div className="swap-convoy-title">Minswap block haul</div>
            <div className="swap-convoy-sub">
              Lorries roll through the checkpoint — doors open, cargo inspect, then they clear the lane
            </div>
          </div>
        </div>
        <div className="swap-convoy-stats">
          <span className="swap-feed-live">
            <span className="swap-feed-pulse" />
            Live haul
          </span>
          {latestPair && <span className="swap-feed-ticker">{latestPair}</span>}
          <span className="swap-feed-count buy">{buyCount} buys</span>
          <span className="swap-feed-count sell">{sellCount} sells</span>
        </div>
      </div>

      <div className="swap-road cartoon-road dual-highway">
        <div className="swap-road-sky" aria-hidden="true">
          <span className="road-sun" />
          <span className="road-cloud c1" />
          <span className="road-cloud c2" />
          <span className="road-cloud c3" />
          <span className="road-cloud c4" />
          <span className="road-sign">MINSWAP · CHECKPOINT</span>
        </div>

        <LaneStage
          lane="buy"
          incoming={buys}
          orderCount={buyCount}
          onSelectBlock={onSelectBlock}
          onSelectSwap={onSelectSwap}
        />

        <div className="highway-median" aria-hidden="true">
          <span>← BUYS</span>
          <span className="median-line" />
          <span className="median-beacon" />
          <span className="median-line" />
          <span>SELLS →</span>
        </div>

        <LaneStage
          lane="sell"
          incoming={sells}
          orderCount={sellCount}
          onSelectBlock={onSelectBlock}
          onSelectSwap={onSelectSwap}
        />

        <div className="swap-road-edge" aria-hidden="true" />
      </div>
    </section>
  );
}
