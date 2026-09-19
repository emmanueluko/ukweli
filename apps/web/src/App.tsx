import { useEffect, useState } from 'react';

interface Health {
  ok: boolean;
  db: boolean;
  modelConfigured: boolean;
}

/**
 * Placeholder only. Per the specification the real interface is not built until
 * the backend is complete; this renders the name and reports whether the API
 * behind the dev proxy is reachable, which is enough to prove the proxy works.
 */
export function App() {
  const [health, setHealth] = useState<Health | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();

    fetch('/healthz', { signal: controller.signal })
      .then((response) => response.json() as Promise<Health>)
      .then(setHealth)
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setError(cause instanceof Error ? cause.message : 'API unreachable');
        }
      });

    return () => controller.abort();
  }, []);

  return (
    <main>
      <h1>Ukweli</h1>
      <p className="tagline">
        Evidence-backed civic information verification for African communities.
      </p>

      <section className="status">
        <h2>API</h2>
        {error && <p className="down">Unreachable — {error}</p>}
        {!error && !health && <p className="pending">Checking…</p>}
        {health && (
          <ul>
            <li>Server: {health.ok ? 'up' : 'degraded'}</li>
            <li>Database: {health.db ? 'connected' : 'unreachable'}</li>
            <li>Model key: {health.modelConfigured ? 'configured' : 'not configured'}</li>
          </ul>
        )}
      </section>
    </main>
  );
}
