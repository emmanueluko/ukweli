import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, ApiError } from '../api';
import { ErrorPanel } from '../components/Feedback';
import { ScreenHeader } from '../components/ScreenHeader';

export function SignIn() {
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);

    try {
      await api.requestMagicLink(email.trim());
      // The server answers the same way whether or not the address is known, so
      // this screen does too.
      navigate(`/link-sent?email=${encodeURIComponent(email.trim())}`);
    } catch (cause) {
      setError(
        cause instanceof ApiError
          ? cause
          : new ApiError('unknown', 'Could not send a sign-in link.', true, 0),
      );
      setBusy(false);
    }
  }

  return (
    <main className="screen">
      <ScreenHeader backTo="/check" backLabel="Back" />

      <div className="stack" style={{ gap: 10, marginTop: 24 }}>
        <img
          src="/ukweli-mark.png"
          alt=""
          aria-hidden="true"
          width={52}
          height={52}
          style={{ borderRadius: 14, display: 'block' }}
        />
        <h1 style={{ fontSize: 30, lineHeight: 1.2 }}>Sign in with your email</h1>
        <p className="lede">We’ll email you a one-tap sign-in link. No passwords, ever.</p>
      </div>

      <form className="card" style={{ gap: 8 }} onSubmit={(event) => void submit(event)}>
        <label htmlFor="email" style={{ fontSize: 13, fontWeight: 600 }}>
          Email address
        </label>
        <input
          id="email"
          type="email"
          className="field"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          placeholder="you@example.com"
          autoComplete="email"
          required
        />
        <button
          type="submit"
          className="btn btn-primary"
          style={{ marginTop: 4 }}
          disabled={busy || email.trim().length === 0}
        >
          {busy ? (
            <>
              <span className="spinner" />
              Sending…
            </>
          ) : (
            'Email me a sign-in link'
          )}
        </button>
      </form>

      {error && <ErrorPanel error={error} />}

      <div className="panel push-down">
        <strong style={{ fontSize: 13, fontWeight: 700, color: 'var(--supported-fg)' }}>
          You don’t need an account to check a claim
        </strong>
        <span style={{ fontSize: 13, lineHeight: 1.55 }}>
          Signing in only saves your check history so you can find past results. Checking claims is
          always free and open.
        </span>
      </div>
    </main>
  );
}
