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
import { extractMinswapSwap, MinswapSwapFeed } from './MinswapSwapCard';
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

/* ─── App Component ──────────────────────────── */
export default function App() {
  const initialConfig = DEMO_RULE_CONFIGS.find(c => c.ruleFilter === DEFAULT_RULE_FILTER)
    ?? DEMO_RULE_CONFIGS[0];
  const [selectedConfigId, setSelectedConfigId] = useState(initialConfig.id);
  const selectedConfig = DEMO_RULE_CONFIGS.find(c => c.id === selectedConfigId) ?? DEMO_RULE_CONFIGS[0];

  const { events, stats, status, paused, connectionLogs, togglePause, clearEvents } =
    useEventStream(SSE_BASE_URL, selectedConfig.ruleFilter);
  const [sorting, setSorting] = useState<SortingState>([]);
  const [selectedEvent, setSelectedEvent] = useState<BlockchainEvent | null>(null);
  const [selectedBlockHeight, setSelectedBlockHeight] = useState<number | null>(null);
  const [globalFilter, setGlobalFilter] = useState('');
  const [logsExpanded, setLogsExpanded] = useState(false);

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

  const rowCount = table.getRowModel().rows.length;

  return (
    <>
      {/* Animated mesh background */}
      <div className="mesh-bg" aria-hidden="true">
        <div className="orb orb-1" />
        <div className="orb orb-2" />
        <div className="orb orb-3" />
      </div>

      <div className="app">
        {/* ─── Header ──────────────────────────── */}
        <div className="header">
          <div className="header-left">
            <div className="logo">
              <div className="logo-icon">
                <CardanoLogo />
              </div>
              <div className="logo-text">
                <span className="logo-title">Event Viewer</span>
                <span className="logo-subtitle">Cardano</span>
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

        {/* ─── Connection Log ──────────────────── */}
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

        {/* ─── Stats Grid ───────────────────────── */}
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

        {/* ─── Live Block Stream ────────────────── */}
        <BlockStream
          events={events}
          selectedHeight={selectedBlockHeight}
          onSelectBlock={handleSelectBlock}
          live={status === 'connected' && !paused}
        />

        {/* ─── Minswap outgoing swap feed ───────── */}
        <MinswapSwapFeed
          trucks={swapTrucks}
          onSelectBlock={handleSelectBlock}
          onSelectSwap={(id) => {
            const event = events.find(e => e.id === id);
            if (event) setSelectedEvent(event);
          }}
        />

        {/* ─── Event Table ─────────────────────── */}
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
