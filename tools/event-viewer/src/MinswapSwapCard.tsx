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

/** Keep in sync with `.journey-shell` / `@keyframes journeyRtl` in App.css */
const JOURNEY_MS = 18000;
/** Open cargo once the truck has eased into center (~34%). */
const CARGO_OPEN_AT = 6200;
const CARGO_CLOSE_AT = 11800;
const MAX_QUEUE = 5;

function MinswapMark({ size = 28, className }: { size?: number; className?: string }) {
  const gid = `minMark-${size}`;
  return (
    <svg className={className} width={size} height={size} viewBox="0 0 32 32" aria-hidden="true">
      <defs>
        <linearGradient id={gid} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor="#2dd4bf" />
          <stop offset="100%" stopColor="#0f766e" />
        </linearGradient>
      </defs>
      <circle cx="16" cy="16" r="16" fill={`url(#${gid})`} />
      <path
        d="M7.5 22.5V9.5L16 18.2 24.5 9.5v13"
        fill="none"
        stroke="#ffffff"
        strokeWidth="2.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function Wheel({ x, y = 108, r = 14 }: { x: number; y?: number; r?: number }) {
  return (
    <g transform={`translate(${x} ${y})`}>
      <g className="truck-wheel">
        <circle r={r} fill="#0f172a" stroke="#020617" strokeWidth="2.5" />
        {[0, 60, 120, 180, 240, 300].map(a => (
          <rect
            key={`tread-${a}`}
            x="-1.4"
            y={-r + 1}
            width="2.8"
            height="3.2"
            rx="1"
            fill="#334155"
            transform={`rotate(${a})`}
          />
        ))}
        <circle r={r * 0.55} fill="#64748b" stroke="#1e293b" strokeWidth="1.5" />
        {[0, 72, 144, 216, 288].map(a => (
          <line
            key={`spoke-${a}`}
            x1="0"
            y1="0"
            x2="0"
            y2={-r * 0.5}
            stroke="#1e293b"
            strokeWidth="2.2"
            strokeLinecap="round"
            transform={`rotate(${a})`}
          />
        ))}
        <circle r={r * 0.22} fill="#cbd5e1" />
      </g>
    </g>
  );
}

function CartoonTruckArt({
  open,
  swapCount,
  lane,
  size,
}: {
  open: boolean;
  swapCount: number;
  lane: TruckLane;
  size: FleetSize;
}) {
  const fleet = FLEET[size];
  const colors = lane === 'buy'
    ? { body: '#14b8a6', cabin: '#0f766e', cabinDark: '#115e59', accent: '#5eead4', door: '#134e4a', panel: '#042f2e' }
    : { body: '#f43f5e', cabin: '#e11d48', cabinDark: '#9f1239', accent: '#fda4af', door: '#881337', panel: '#4c0519' };
  const facingWest = lane === 'sell';
  const midX = fleet.viewW / 2;
  const trailerY = size === 'artic' ? 30 : 38;
  const doorW = fleet.trailerW / 2;
  const wheelR = size === 'van' ? 12 : size === 'artic' ? 15 : 14;
  const logoX = fleet.trailerX + fleet.trailerW * 0.42;
  const logoY = trailerY + fleet.trailerH * 0.38;
  const logoR = size === 'artic' ? 18 : size === 'lorry' ? 16 : 14;

  return (
    <svg
      className={`cartoon-truck-svg lane-${lane} size-${size} ${open ? 'doors-open' : ''}`}
      viewBox={`0 0 ${fleet.viewW} 140`}
      role="img"
      aria-label={`Minswap ${lane === 'buy' ? 'buy' : 'sell'} haul · ${swapCount} swaps`}
    >
      <defs>
        <linearGradient id={`body-${lane}-${size}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={colors.body} />
          <stop offset="100%" stopColor={lane === 'buy' ? '#0f766e' : '#be123c'} />
        </linearGradient>
        <linearGradient id={`cabin-${lane}-${size}`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor={colors.cabin} />
          <stop offset="100%" stopColor={colors.cabinDark} />
        </linearGradient>
        <linearGradient id={`glass-${lane}-${size}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="rgba(224,242,254,0.85)" />
          <stop offset="100%" stopColor="rgba(56,189,248,0.35)" />
        </linearGradient>
        <linearGradient id={`beam-${lane}-${size}`} x1="1" y1="0" x2="0" y2="0">
          <stop offset="0%" stopColor="rgba(254,240,138,0.55)" />
          <stop offset="100%" stopColor="rgba(254,240,138,0)" />
        </linearGradient>
        <filter id={`truckSoftShadow-${lane}-${size}`} x="-20%" y="-20%" width="140%" height="140%">
          <feDropShadow dx="0" dy="4" stdDeviation="2.5" floodOpacity="0.4" />
        </filter>
      </defs>

      <g transform={facingWest ? `translate(${fleet.viewW},0) scale(-1,1)` : undefined} filter={`url(#truckSoftShadow-${lane}-${size})`}>
        <ellipse className="truck-ground-shadow" cx={midX} cy="128" rx={fleet.shadowRx} ry="6" fill="rgba(15,23,42,0.4)" />

        <g className="truck-speedlines" aria-hidden="true">
          <line className="speedline s1" x1={fleet.viewW + 4} y1="52" x2={fleet.viewW + 46} y2="52" />
          <line className="speedline s2" x1={fleet.viewW + 12} y1="76" x2={fleet.viewW + 60} y2="76" />
          <line className="speedline s3" x1={fleet.viewW + 4} y1="98" x2={fleet.viewW + 44} y2="98" />
        </g>

        <g className="truck-bounce-group">
          <rect
            x={fleet.trailerX}
            y={trailerY}
            width={fleet.trailerW}
            height={fleet.trailerH}
            rx="12"
            fill={`url(#body-${lane}-${size})`}
            stroke="#0f172a"
            strokeWidth="2.5"
          />
          <rect
            x={fleet.trailerX + 6}
            y={trailerY + 6}
            width={fleet.trailerW - 12}
            height={fleet.trailerH - 22}
            rx="8"
            fill="rgba(255,255,255,0.12)"
          />

          <g className={`truck-svg-cargo ${open ? 'is-open' : ''}`}>
            {Array.from({ length: fleet.crates }, (_, i) => {
              const cx = fleet.trailerX + 20 + i * 34;
              const cy = trailerY + (size === 'artic' && i % 2 === 1 ? 14 : 18);
              return (
                <g key={i} className="truck-crate-pop" style={{ animationDelay: `${i * 70}ms` }}>
                  <rect
                    x={cx}
                    y={cy}
                    width="26"
                    height={size === 'artic' ? 28 : 24}
                    rx="4"
                    fill={CRATE_COLORS[i % CRATE_COLORS.length]}
                    stroke="#0f172a"
                    strokeWidth="1.5"
                  />
                  <rect x={cx + 4} y={cy + 4} width="18" height="3" rx="1" fill="rgba(15,23,42,0.12)" />
                </g>
              );
            })}
          </g>

          <g className={`truck-svg-door left ${open ? 'open' : ''}`}>
            <rect x={fleet.trailerX} y={trailerY} width={doorW} height={fleet.doorH} rx="11" fill={colors.door} stroke="#0f172a" strokeWidth="2.5" />
            <rect x={fleet.trailerX + 10} y={trailerY + 12} width={doorW - 22} height={fleet.doorH - 24} rx="5" fill="rgba(255,255,255,0.06)" />
            <circle cx={fleet.trailerX + doorW - 10} cy={trailerY + fleet.doorH / 2} r="3.5" fill={colors.accent} />
          </g>
          <g className={`truck-svg-door right ${open ? 'open' : ''}`}>
            <rect x={fleet.trailerX + doorW} y={trailerY} width={doorW} height={fleet.doorH} rx="11" fill={colors.door} stroke="#0f172a" strokeWidth="2.5" />
            <rect x={fleet.trailerX + doorW + 12} y={trailerY + 12} width={doorW - 22} height={fleet.doorH - 24} rx="5" fill="rgba(255,255,255,0.06)" />
            <circle cx={fleet.trailerX + doorW + 10} cy={trailerY + fleet.doorH / 2} r="3.5" fill={colors.accent} />
          </g>

          {/* Counter-flip so Minswap mark stays readable on both lanes */}
          <g
            className={`truck-brand-badge ${open ? 'is-dim' : ''}`}
            transform={`translate(${logoX} ${logoY})${facingWest ? ' scale(-1,1)' : ''}`}
          >
            <circle r={logoR + 2} fill={colors.panel} opacity="0.35" />
            <circle r={logoR} fill="#042f2e" stroke="#5eead4" strokeWidth="1.5" />
            <path
              d={`M${-logoR * 0.45} ${logoR * 0.35} V${-logoR * 0.4} L0 ${logoR * 0.15} L${logoR * 0.45} ${-logoR * 0.4} V${logoR * 0.35}`}
              fill="none"
              stroke="#5eead4"
              strokeWidth="2.2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </g>

          <rect
            x={fleet.trailerX + 16}
            y={trailerY + fleet.trailerH - 12}
            width={fleet.trailerW - 32}
            height="6"
            rx="2"
            fill={colors.accent}
            opacity="0.85"
          />

          {size === 'artic' && (
            <g>
              {[0, 1, 2, 3, 4].map(i => (
                <circle
                  key={i}
                  className="roof-light"
                  cx={fleet.trailerX + 36 + i * 52}
                  cy={trailerY - 3}
                  r="2.8"
                  fill="#fde047"
                  stroke="#0f172a"
                  strokeWidth="1"
                  style={{ animationDelay: `${i * 120}ms` }}
                />
              ))}
            </g>
          )}

          <path
            className="truck-beam"
            d="M15 73 L-56 57 L-56 103 L15 89 Z"
            fill={`url(#beam-${lane}-${size})`}
          />
          <path
            d="M18 58h52c6 0 10 4 12 10l8 26c1 4-2 8-6 8H18c-5 0-8-4-8-8V66c0-5 3-8 8-8z"
            fill={`url(#cabin-${lane}-${size})`}
            stroke="#0f172a"
            strokeWidth="2.5"
          />
          <path
            d="M34 64h28c3 0 5 2 6 5l3 12H32l2-12c1-3 2-5 4-5z"
            fill={`url(#glass-${lane}-${size})`}
            stroke="#0f172a"
            strokeWidth="1.8"
          />
          <g className="truck-driver" aria-hidden="true">
            <circle cx="47" cy="73.5" r="6.2" fill="#fcd34d" stroke="#0f172a" strokeWidth="1.4" />
            <path d="M40.9 72.5 a6.2 6.2 0 0 1 12.2 0 z" fill="#0ea5e9" stroke="#0f172a" strokeWidth="1.1" />
            <path d="M39 72.2 h5.5" stroke="#0f172a" strokeWidth="1.6" strokeLinecap="round" />
            <g className="driver-eyes">
              <circle cx="44.7" cy="74.4" r="1.05" fill="#0f172a" />
              <circle cx="49.3" cy="74.4" r="1.05" fill="#0f172a" />
            </g>
            <path d="M45 77.2 q2 1.7 4 0" stroke="#0f172a" strokeWidth="1.1" fill="none" strokeLinecap="round" />
          </g>
          <path d="M36 68h16" stroke="rgba(255,255,255,0.55)" strokeWidth="1.5" strokeLinecap="round" />
          <rect x="14" y="96" width="36" height="6" rx="2" fill="#1e293b" opacity="0.55" />
          <ellipse cx="16" cy="80" rx="4.5" ry="3.5" fill="#fef08a" stroke="#0f172a" strokeWidth="1.5" className="truck-headlight" />
          <g className="truck-exhaust" aria-hidden="true">
            <circle className="puff p1" cx="10" cy="88" r="2.5" fill="rgba(226,232,240,0.45)" />
            <circle className="puff p2" cx="5" cy="82" r="3.5" fill="rgba(226,232,240,0.28)" />
            <circle className="puff p3" cx="1" cy="75" r="4.5" fill="rgba(226,232,240,0.16)" />
          </g>

          {fleet.wheels.map(x => (
            <Wheel key={x} x={x} r={wheelR} />
          ))}

          {open && (
            <g
              className="cargo-burst"
              aria-hidden="true"
              transform={`translate(${fleet.trailerX + fleet.trailerW / 2} ${trailerY + 8})`}
            >
              {[0, 1, 2, 3, 4, 5].map(i => (
                <g key={i} className={`burst-coin bc${i + 1}`}>
                  <circle r="5.5" fill="#fde047" stroke="#a16207" strokeWidth="1.6" />
                  <circle r="3" fill="none" stroke="#a16207" strokeWidth="1" opacity="0.7" />
                </g>
              ))}
            </g>
          )}

          {open && (
            <g className="cargo-sparkles" aria-hidden="true">
              {[0, 1, 2].map(i => (
                <path
                  key={i}
                  className={`cargo-sparkle csp${i + 1}`}
                  d="M0 -5 L1.4 -1.4 L5 0 L1.4 1.4 L0 5 L-1.4 1.4 L-5 0 L-1.4 -1.4 Z"
                  transform={`translate(${fleet.trailerX + 16 + i * (fleet.trailerW / 2 - 8)} ${trailerY - 8 - (i % 2) * 7})`}
                />
              ))}
            </g>
          )}

          {open && (
            <g
              className="driver-bubble"
              aria-hidden="true"
              transform={`translate(46 34)${facingWest ? ' scale(-1,1)' : ''}`}
            >
              <g className="driver-bubble-inner">
                <rect x="-27" y="-12" width="54" height="18" rx="9" fill="#f8fafc" stroke="#0f172a" strokeWidth="2" />
                <path d="M2 5 L8 16 L13 5 Z" fill="#f8fafc" stroke="#0f172a" strokeWidth="2" strokeLinejoin="round" />
                <rect x="1" y="2" width="13" height="5" fill="#f8fafc" />
                <text x="0" y="0.5" textAnchor="middle" dominantBaseline="middle" className="bubble-text">
                  SWAPS!
                </text>
              </g>
            </g>
          )}
        </g>
      </g>
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
      <span className="swap-parcel-leg">
        <span className="swap-parcel-amt">{swap.amountIn}</span>
        <span
          className="swap-parcel-ticker"
          style={{ background: inTheme.bg, color: inTheme.fg }}
        >
          {swap.swapInTicker}
        </span>
      </span>
      <span className="swap-parcel-arrow" aria-hidden="true">→</span>
      <span className="swap-parcel-leg">
        <span className="swap-parcel-amt receive">{swap.minReceive}</span>
        <span
          className="swap-parcel-ticker"
          style={{ background: outTheme.bg, color: outTheme.fg }}
        >
          {swap.swapOutTicker}
        </span>
      </span>
    </button>
  );
}

const CARGO_VISIBLE = 3;

function InspectManifest({
  truck,
  onSelectSwap,
}: {
  truck: LaneTruck;
  onSelectSwap?: (eventId: string) => void;
}) {
  const size = fleetSizeFor(truck.swaps.length);
  const visible = truck.swaps.slice(0, CARGO_VISIBLE);
  const more = truck.swaps.length - visible.length;

  return (
    <aside className={`inspect-manifest lane-${truck.lane} size-${size}`} aria-label="Haul cargo">
      <div className="inspect-manifest-head">
        <span className="inspect-manifest-title">Cargo manifest</span>
        <span className="inspect-manifest-count">{truck.swaps.length}</span>
      </div>
      <div className="inspect-manifest-body">
        {visible.map((item, index) => (
          <SwapParcel
            key={item.eventId}
            swap={item.swap}
            index={index}
            onClick={() => onSelectSwap?.(item.eventId)}
          />
        ))}
        {more > 0 && (
          <div className="inspect-manifest-more">+{more} more</div>
        )}
      </div>
    </aside>
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
        aria-label={`${truck.lane} haul · ${truck.swaps.length} swaps`}
      >
        <CartoonTruckArt
          open={open}
          swapCount={truck.swaps.length}
          lane={truck.lane}
          size={size}
        />
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
  onSelectBlock,
  onSelectSwap,
}: {
  lane: TruckLane;
  incoming: LaneTruck[];
  onSelectBlock?: (height: number) => void;
  onSelectSwap?: (eventId: string) => void;
}) {
  const { current, cargoOpen, phase } = useLaneJourney(incoming);
  const isBuy = lane === 'buy';
  const size = current ? fleetSizeFor(current.swaps.length) : 'lorry';
  const shellW = FLEET[size].shellCssW;

  return (
    <div className={`highway-lane lane-${lane} phase-${current ? phase : 'idle'}`}>
      <div className="lane-stage">
        <div className="lane-scenery" aria-hidden="true">
          <span className="lane-sky">
            <span className="lane-sun" />
            <span className="lane-cloud lc1" />
            <span className="lane-cloud lc2" />
            <span className="lane-cloud lc3" />
            <span className="bird b1" />
            <span className="bird b2" />
            <span className="lane-balloon">
              <span className="balloon-envelope" />
              <span className="balloon-basket" />
            </span>
          </span>
          <span className="lane-hills">
            <span className="hill h1" />
            <span className="hill h2" />
            <span className="hill h3" />
          </span>
          <span className="lane-meadow">
            <span className="meadow-scroll">
              <span className="meadow-band" />
              <span className="meadow-band" />
            </span>
          </span>
          <span className="lane-asphalt" />
          <span className="scenery-layer far">
            <span className="tree-strip">
              <span className="tree t1 size-sm" />
              <span className="tree t2 size-md" />
              <span className="tree t3 size-sm" />
              <span className="tree t1b size-sm" />
              <span className="tree t2b size-md" />
              <span className="tree t3b size-sm" />
            </span>
          </span>
          <span className="scenery-layer mid">
            <span className="tree-strip">
              <span className="tree t4 size-lg" />
              <span className="tree t5 size-md" />
              <span className="windmill wm1">
                <span className="wm-pole" />
                <span className="wm-blades">
                  <span className="wm-blade" />
                  <span className="wm-blade" />
                  <span className="wm-blade" />
                </span>
                <span className="wm-cap" />
              </span>
              <span className="tree t4b size-lg" />
              <span className="tree t5b size-md" />
              <span className="windmill wm1b">
                <span className="wm-pole" />
                <span className="wm-blades">
                  <span className="wm-blade" />
                  <span className="wm-blade" />
                  <span className="wm-blade" />
                </span>
                <span className="wm-cap" />
              </span>
            </span>
          </span>
          <span className="scenery-layer near">
            <span className="tree-strip">
              <span className="tree t6 size-lg" />
              <span className="tree t7 size-md" />
              <span className="tree t6b size-lg" />
              <span className="tree t7b size-md" />
            </span>
          </span>
        </div>

        <div className="lane-watermark" aria-hidden="true">
          {isBuy ? 'BUYS' : 'SELLS'}
        </div>

        <div className="swap-road-dashes" aria-hidden="true" />
        <div className={`lane-glow ${cargoOpen ? 'is-hot' : ''}`} aria-hidden="true" />
        <div className="inspect-spotlight" aria-hidden="true" />
        <div className={`checkpoint-sign ${cargoOpen ? 'is-lit' : ''}`} aria-hidden="true">
          <span className="checkpoint-chains">
            <span />
            <span />
          </span>
          <span className="checkpoint-board">Cargo check</span>
        </div>

        {current ? (
          <div
            key={current.key}
            className={`journey-shell journey-${isBuy ? 'rtl' : 'ltr'} size-${size} phase-${phase}`}
            style={{ width: shellW, marginLeft: -shellW / 2 }}
          >
            <div className="dust-cloud" aria-hidden="true" />
            <div className="truck-lean">
              <BlockTruck
                truck={current}
                open={cargoOpen}
                phase={phase}
                onSelectBlock={onSelectBlock}
              />
            </div>
          </div>
        ) : (
          <div className="lane-idle-pro">
            <span className="lane-idle-pro-title">
              Awaiting {isBuy ? 'buy' : 'sell'} hauls
            </span>
            <span className="lane-idle-pro-sub">Filtered Minswap V2 swaps stream here live</span>
          </div>
        )}

        {cargoOpen && current && (
          <InspectManifest
            truck={current}
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
  const total = buyCount + sellCount;

  return (
    <section className="swap-convoy" aria-label="Minswap filtered swap hauls">
      <div className="swap-convoy-header">
        <div className="swap-convoy-brand">
          <span className="swap-convoy-mark" aria-hidden="true">
            <MinswapMark size={34} />
          </span>
          <div>
            <div className="swap-convoy-title">Minswap filtered swaps</div>
            <div className="swap-convoy-sub">
              Live address-match hauls · doors open to inspect cargo
            </div>
          </div>
        </div>
        <div className="swap-convoy-stats">
          <span className="swap-feed-live">
            <span className="swap-feed-pulse" />
            Live
          </span>
          <span className="swap-feed-count buy" title="Buy-side swaps">
            <span className="swap-feed-count-label">Buys</span>
            {buyCount}
          </span>
          <span className="swap-feed-count sell" title="Sell-side swaps">
            <span className="swap-feed-count-label">Sells</span>
            {sellCount}
          </span>
          <span className="swap-feed-count total" title="Total filtered swaps">
            {total}
          </span>
        </div>
      </div>

      <div className="swap-road cartoon-road dual-highway">
        <LaneStage
          lane="buy"
          incoming={buys}
          onSelectBlock={onSelectBlock}
          onSelectSwap={onSelectSwap}
        />

        <div className="highway-median" aria-hidden="true">
          <span className="median-line" />
        </div>

        <LaneStage
          lane="sell"
          incoming={sells}
          onSelectBlock={onSelectBlock}
          onSelectSwap={onSelectSwap}
        />
      </div>
    </section>
  );
}
