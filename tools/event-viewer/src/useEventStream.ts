import { useEffect, useRef, useCallback, useState } from 'react';
import type { BlockchainEvent } from './types';

const MAX_EVENTS = 500;

export type ConnectionStatus = 'connecting' | 'connected' | 'disconnected' | 'error';

export interface EventStreamState {
  events: BlockchainEvent[];
  stats: StreamStats;
  status: ConnectionStatus;
  paused: boolean;
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

export function useEventStream(url: string): EventStreamState {
  const [events, setEvents] = useState<BlockchainEvent[]>([]);
  const [status, setStatus] = useState<ConnectionStatus>('connecting');
  const [paused, setPaused] = useState(false);
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
    const eventSource = new EventSource(url);

    eventSource.onopen = () => {
      setStatus('connected');
      connectedAt.current = Date.now();
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
        // skip malformed events
      }
    };

    eventSource.onerror = () => {
      setStatus(prev => prev === 'connected' ? 'disconnected' : 'error');
    };

    // Periodic stats update for uptime
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
      eventSource.close();
      cancelAnimationFrame(rafRef.current);
      clearInterval(interval);
    };
  }, [url, flushBatch]);

  return { events, stats, status, paused, togglePause, clearEvents };
}
