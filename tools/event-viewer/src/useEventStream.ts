import { useEffect, useRef, useCallback, useState } from 'react';
import type { BlockchainEvent } from './types';
import { buildStreamUrl } from './ruleConfigs';

const MAX_EVENTS = 500;
const MAX_LOGS = 50;
const RECONNECT_BASE_MS = 1000;
const RECONNECT_MAX_MS = 15000;

export type ConnectionStatus = 'connecting' | 'connected' | 'disconnected' | 'error';

export interface ConnectionLogEntry {
  time: string;
  message: string;
  level: 'info' | 'warn' | 'error' | 'success';
}

export interface EventStreamState {
  events: BlockchainEvent[];
  stats: StreamStats;
  status: ConnectionStatus;
  paused: boolean;
  connectionLogs: ConnectionLogEntry[];
  togglePause: () => void;
  clearEvents: () => void;
}

export interface StreamStats {
  totalReceived: number;
  eventsPerSecond: number;
  ruleBreakdown: Record<string, number>;
  lastEventTime: string | null;
  connectionUptime: number;
}

function nowIso(): string {
  return new Date().toISOString();
}

export function useEventStream(baseUrl: string, ruleFilter: string | null): EventStreamState {
  const [events, setEvents] = useState<BlockchainEvent[]>([]);
  const [status, setStatus] = useState<ConnectionStatus>('connecting');
  const [paused, setPaused] = useState(false);
  const [connectionLogs, setConnectionLogs] = useState<ConnectionLogEntry[]>([]);
  const [stats, setStats] = useState<StreamStats>({
    totalReceived: 0,
    eventsPerSecond: 0,
    ruleBreakdown: {},
    lastEventTime: null,
    connectionUptime: 0,
  });

  const totalRef = useRef(0);
  const ruleBreakdownRef = useRef<Record<string, number>>({});
  const recentTimestamps = useRef<number[]>([]);
  const connectedAt = useRef<number>(0);
  const batchRef = useRef<BlockchainEvent[]>([]);
  const rafRef = useRef<number>(0);
  const pausedRef = useRef(false);
  const reconnectAttempt = useRef(0);
  const reconnectTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const addLog = useCallback((message: string, level: ConnectionLogEntry['level'] = 'info') => {
    setConnectionLogs(prev => {
      const entry: ConnectionLogEntry = { time: nowIso(), message, level };
      const next = [entry, ...prev];
      return next.length > MAX_LOGS ? next.slice(0, MAX_LOGS) : next;
    });
  }, []);

  const flushBatch = useCallback(() => {
    const batch = batchRef.current;
    if (batch.length === 0) return;
    batchRef.current = [];

    if (!pausedRef.current) {
      setEvents(prev => {
        const next = [...batch, ...prev];
        return next.length > MAX_EVENTS ? next.slice(0, MAX_EVENTS) : next;
      });
    }

    const now = Date.now();
    recentTimestamps.current.push(...batch.map(() => now));
    recentTimestamps.current = recentTimestamps.current.filter(t => now - t < 5000);

    setStats({
      totalReceived: totalRef.current,
      eventsPerSecond: Math.round((recentTimestamps.current.length / 5) * 10) / 10,
      ruleBreakdown: { ...ruleBreakdownRef.current },
      lastEventTime: batch[0]?.time ?? null,
      connectionUptime: connectedAt.current > 0 ? Math.floor((now - connectedAt.current) / 1000) : 0,
    });
  }, []);

  const clearEvents = useCallback(() => {
    setEvents([]);
    totalRef.current = 0;
    ruleBreakdownRef.current = {};
    recentTimestamps.current = [];
    setStats(s => ({ ...s, totalReceived: 0, eventsPerSecond: 0, ruleBreakdown: {} }));
  }, []);

  const togglePause = useCallback(() => {
    setPaused(p => {
      pausedRef.current = !p;
      return !p;
    });
  }, []);

  useEffect(() => {
    // Reset buffer when filter changes so results are immediately distinct (AC-3)
    setEvents([]);
    totalRef.current = 0;
    ruleBreakdownRef.current = {};
    recentTimestamps.current = [];
    batchRef.current = [];
    connectedAt.current = 0;
    setStats({
      totalReceived: 0,
      eventsPerSecond: 0,
      ruleBreakdown: {},
      lastEventTime: null,
      connectionUptime: 0,
    });

    const streamUrl = buildStreamUrl(baseUrl, ruleFilter);
    let eventSource: EventSource | null = null;
    let disposed = false;

    const connect = () => {
      if (disposed) return;

      setStatus('connecting');
      const filterLabel = ruleFilter ?? 'all';
      addLog(`Connecting to delivery layer (${filterLabel} filter)…`, 'info');

      eventSource = new EventSource(streamUrl);

      eventSource.onopen = () => {
        if (disposed) return;
        reconnectAttempt.current = 0;
        setStatus('connected');
        connectedAt.current = Date.now();
        addLog(`Connected via SSE — streaming ${filterLabel} events`, 'success');
      };

      eventSource.onmessage = (e) => {
        try {
          const event: BlockchainEvent = JSON.parse(e.data);
          totalRef.current++;

          const ruleName = event.data?.ruleName ?? 'Unknown';
          ruleBreakdownRef.current[ruleName] = (ruleBreakdownRef.current[ruleName] ?? 0) + 1;

          batchRef.current.push(event);

          cancelAnimationFrame(rafRef.current);
          rafRef.current = requestAnimationFrame(flushBatch);
        } catch {
          addLog('Skipped malformed event payload', 'warn');
        }
      };

      eventSource.onerror = () => {
        if (disposed) return;
        eventSource?.close();
        eventSource = null;
        connectedAt.current = 0;
        setStatus('disconnected');
        addLog('Connection lost — scheduling reconnect', 'warn');

        reconnectAttempt.current += 1;
        const delay = Math.min(
          RECONNECT_BASE_MS * Math.pow(2, reconnectAttempt.current - 1),
          RECONNECT_MAX_MS,
        );
        reconnectTimer.current = setTimeout(connect, delay);
      };
    };

    connect();

    const interval = setInterval(() => {
      if (connectedAt.current > 0) {
        const now = Date.now();
        recentTimestamps.current = recentTimestamps.current.filter(t => now - t < 5000);
        setStats(s => ({
          ...s,
          eventsPerSecond: Math.round((recentTimestamps.current.length / 5) * 10) / 10,
          connectionUptime: Math.floor((now - connectedAt.current) / 1000),
        }));
      }
    }, 1000);

    return () => {
      disposed = true;
      eventSource?.close();
      if (reconnectTimer.current) clearTimeout(reconnectTimer.current);
      cancelAnimationFrame(rafRef.current);
      clearInterval(interval);
    };
  }, [baseUrl, ruleFilter, flushBatch, addLog]);

  return { events, stats, status, paused, connectionLogs, togglePause, clearEvents };
}
