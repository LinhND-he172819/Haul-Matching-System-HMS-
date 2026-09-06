import { useEffect } from 'react';
import { startHmsConnection, onHmsEvent } from '../realtime/hmsFleetConnection';

/**
 * Hook to subscribe to a SignalR event with automatic cleanup.
 * Ensures connection is started on mount.
 */
export function useHmsEvent(
  eventName: string,
  callback: (...args: any[]) => void
): void {
  useEffect(() => {
    startHmsConnection();

    const unsubscribe = onHmsEvent(eventName, callback);
    return () => {
      unsubscribe();
    };
  }, [eventName, callback]);
}

/**
 * Hook to ensure the SignalR connection is started on mount.
 */
export function useHmsConnection(): void {
  useEffect(() => {
    startHmsConnection();
  }, []);
}
