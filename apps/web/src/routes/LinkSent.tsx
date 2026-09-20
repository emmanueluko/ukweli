import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { api } from '../api';
import { EnvelopeIcon } from '../components/Icons';
import { ScreenHeader } from '../components/ScreenHeader';

export function LinkSent() {
  const [params] = useSearchParams();
  const email = params.get('email') ?? '';
  const [resent, setResent] = useState(false);

  async function resend() {
    if (!email) {
      return;
    }

    try {
      await api.requestMagicLink(email);
      setResent(true);
    } catch {
      // Still reported as sent: the endpoint never reveals whether an address
      // is known, and a failure here must not leak that either.
      setResent(true);
    }
  }

  return (
    <main className="screen">
      <ScreenHeader backTo="/sign-in" backLabel="Back" />

      <div
        className="stack"
        style={{ alignItems: 'center', gap: 14, marginTop: 48, textAlign: 'center', padding: '0 12px' }}
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
        <h1 style={{ fontSize: 30, lineHeight: 1.2 }}>Check your inbox</h1>
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
        <button type="button" className="btn btn-secondary" onClick={() => void resend()}>
          {resent ? 'Link sent again' : 'Resend the link'}
        </button>
        <Link
          to="/sign-in"
          className="btn-quiet"
          style={{ justifyContent: 'center', color: 'var(--muted)', textDecoration: 'underline' }}
        >
          Use a different email
        </Link>
      </div>

      <p className="fine push-down" style={{ borderTop: '1px solid var(--line-soft)', paddingTop: 12 }}>
        Didn’t get it? Check spam, or wait a minute — delivery can be slow on some networks. In
        development, mail is captured by Mailpit at localhost:8025.
      </p>
    </main>
  );
}
