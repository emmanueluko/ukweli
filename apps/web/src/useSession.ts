import { useEffect, useState } from 'react';
import { api } from './api';

export type Session =
  | { state: 'loading' }
  /** Auth is switched off entirely for this deployment. */
  | { state: 'disabled' }
  | { state: 'anonymous' }
  | { state: 'signed-in'; email: string };

/**
 * Who is viewing, asked once per page load.
 *
 * The answer is cached at module level: several screens need it, and each of
 * them mounting its own request would mean the header flickering between
 * "Sign in" and "My checks" as the answers arrived at different times.
 */
let cached: Session | null = null;
let inFlight: Promise<Session> | null = null;

function load(): Promise<Session> {
  if (inFlight) {
    return inFlight;
  }

  inFlight = api
    .me()
    .then((me): Session => ({ state: 'signed-in', email: me.email }))
    .catch((cause: unknown): Session => {
      // 404 means the auth routes are not mapped at all, 401 means simply not
      // signed in. They are different situations and the interface says so.
      const status = (cause as { status?: number })?.status;
      return status === 404 ? { state: 'disabled' } : { state: 'anonymous' };
    })
    .then((result) => {
      cached = result;
      inFlight = null;
      return result;
    });

  return inFlight;
}

/** Clears the cache after signing in or out, so the next read is truthful. */
export function forgetSession() {
  cached = null;
  inFlight = null;
}

export function useSession(): Session {
  const [session, setSession] = useState<Session>(cached ?? { state: 'loading' });

  useEffect(() => {
    if (cached) {
      setSession(cached);
      return;
    }

    let active = true;
    void load().then((result) => {
      if (active) {
        setSession(result);
      }
    });

    return () => {
      active = false;
    };
  }, []);

  return session;
}
