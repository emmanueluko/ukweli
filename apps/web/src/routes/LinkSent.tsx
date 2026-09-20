import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { api, ApiError } from '../api';
import { EnvelopeIcon } from '../components/Icons';
import { ScreenHeader } from '../components/ScreenHeader';

type SendState =
  | { kind: 'idle' }
  | { kind: 'sending' }
  | { kind: 'sent' }
  | { kind: 'failed'; message: string };

export function LinkSent() {
  const [params] = useSearchParams();
  const email = params.get('email')?.trim() ?? '';
  const [state, setState] = useState<SendState>({ kind: 'idle' });
  const resetTimer = useRef<number>(undefined);

  useEffect(() => () => window.clearTimeout(resetTimer.current), []);

  async function resend() {
    if (!email || state.kind === 'sending') {
      return;
    }

    setState({ kind: 'sending' });
    window.clearTimeout(resetTimer.current);

    try {
      await api.requestMagicLink(email);
      setState({ kind: 'sent' });
    } catch (cause) {
      // A rate limit is worth showing: it is the one failure the person can do
      // something about, namely wait. Everything else stays quiet, because the
      // endpoint never reveals whether an address is known and an error message
      // here would leak exactly that.
      setState(
        cause instanceof ApiError && cause.code === 'rate_limited'
          ? { kind: 'failed', message: cause.message }
          : { kind: 'sent' },
      );
    }

    // The confirmation clears itself, so a second press has something to say.
    // Left permanent, the button read "Link sent again" forever and looked
    // broken however well it worked.
    resetTimer.current = window.setTimeout(() => setState({ kind: 'idle' }), 5000);
  }

  return (
    <main className="screen">
      <ScreenHeader backTo="/sign-in" backLabel="Back" />

      <div
        className="stack"
        style={{ alignItems: 'center', gap: 14, marginTop: 40, textAlign: 'center', padding: '0 12px' }}
      >
        <span
          aria-hidden="true"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 72,
            height: 72,
            background: 'var(--supported-bg)',
            color: 'var(--supported-fg)',
            borderRadius: 20,
          }}
        >
          <EnvelopeIcon />
        </span>
        <h1 className="page-title">Check your inbox</h1>
        <p className="lede">
          {email ? (
            <>
              We sent a sign-in link to <strong style={{ color: 'var(--ink)' }}>{email}</strong>.
            </>
          ) : (
            <>We sent a sign-in link to the address you gave.</>
          )}{' '}
          Open it on this device to finish signing in. The link works once and expires in 15
          minutes.
        </p>
      </div>

      <div className="stack" style={{ gap: 10, marginTop: 12 }}>
        {/* Without the address in the URL there is nothing to resend to, so the
            screen offers the one thing that does work rather than a dead button. */}
        {email ? (
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => void resend()}
            disabled={state.kind === 'sending'}
          >
            {state.kind === 'sending' && 'Sending…'}
            {state.kind === 'sent' && 'Sent — check your inbox'}
            {(state.kind === 'idle' || state.kind === 'failed') && 'Resend the link'}
          </button>
        ) : (
          <Link to="/sign-in" className="btn btn-primary">
            Enter your email again
          </Link>
        )}

        <div aria-live="polite">
          {state.kind === 'failed' && (
            <p className="fine" style={{ margin: 0, color: 'var(--contradicted-fg)' }}>
              {state.message}
            </p>
          )}
          {state.kind === 'sent' && (
            <p className="fine" style={{ margin: 0 }}>
              Another link is on its way. The previous one still works until it is used.
            </p>
          )}
        </div>

        <Link
          to="/sign-in"
          className="btn-quiet"
          style={{ justifyContent: 'center', color: 'var(--muted)', textDecoration: 'underline' }}
        >
          Use a different email
        </Link>
      </div>

      <p className="fine push-down" style={{ borderTop: '1px solid var(--line-soft)', paddingTop: 12 }}>
        Didn’t get it? Check your spam folder, or wait a minute — delivery can be slow on some
        networks.
      </p>
    </main>
  );
}
