import * as signalR from '@microsoft/signalr';

const API_BASE =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  'http://localhost:5104';

let _connection: signalR.HubConnection | null = null;
let _listeners: Map<string, Set<(...args: any[]) => void>> = new Map();

/**
 * Get or create a singleton SignalR connection to the HmsFleetHub.
 * Uses accessTokenFactory from localStorage for JWT auth.
 * Automatic reconnect enabled.
 */
export function getHmsConnection(): signalR.HubConnection {
  if (_connection && _connection.state === signalR.HubConnectionState.Connected) {
    return _connection;
  }
  if (_connection && _connection.state === signalR.HubConnectionState.Connecting) {
    return _connection;
  }

  if (!_connection) {
    _connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE}/hub/fleet`, {
        accessTokenFactory: () => localStorage.getItem('accessToken') || '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    _connection.onreconnecting(() => {
      console.log('[SignalR] Reconnecting...');
    });
    _connection.onreconnected(() => {
      console.log('[SignalR] Reconnected');
      // Re-register all listeners after reconnect
      _listeners.forEach((callbacks, eventName) => {
        callbacks.forEach((cb) => {
          _connection!.on(eventName, cb);
        });
      });
    });
    _connection.onclose((err) => {
      console.warn('[SignalR] Connection closed', err);
    });
  }

  return _connection;
}

/**
 * Start the connection if not already connected.
 */
export async function startHmsConnection(): Promise<void> {
  const conn = getHmsConnection();
  if (conn.state === signalR.HubConnectionState.Disconnected) {
    try {
      await conn.start();
      console.log('[SignalR] Connected');
    } catch (err) {
      console.error('[SignalR] Failed to start:', err);
      // Retry after delay
      setTimeout(() => startHmsConnection(), 5000);
    }
  }
}

/**
 * Stop the connection gracefully.
 */
export async function stopHmsConnection(): Promise<void> {
  if (_connection && _connection.state !== signalR.HubConnectionState.Disconnected) {
    await _connection.stop();
    _connection = null;
    _listeners.clear();
  }
}

/**
 * Register a listener for a SignalR event.
 * Returns an unsubscribe function.
 */
export function onHmsEvent(
  eventName: string,
  callback: (...args: any[]) => void
): () => void {
  const conn = getHmsConnection();
  conn.on(eventName, callback);

  // Track for re-registration on reconnect
  if (!_listeners.has(eventName)) {
    _listeners.set(eventName, new Set());
  }
  _listeners.get(eventName)!.add(callback);

  return () => {
    conn.off(eventName, callback);
    _listeners.get(eventName)?.delete(callback);
    if (_listeners.get(eventName)?.size === 0) {
      _listeners.delete(eventName);
    }
  };
}
