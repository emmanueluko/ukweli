import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api, ApiError, claimLimits, type ExampleClaim, storedLanguage } from '../api';
import { Brand } from '../components/Brand';
import { ErrorPanel } from '../components/Feedback';
import { useSession } from '../useSession';

export function Home() {
  const navigate = useNavigate();
  const session = useSession();
  const [text, setText] = useState('');
  const [examples, setExamples] = useState<ExampleClaim[]>([]);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const errorRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    // An empty list is a valid answer, so a failure here is not worth an alarm:
    // the box still works.
    api.examples().then(setExamples).catch(() => setExamples([]));
  }, []);

  useEffect(() => {
    if (error) {
      errorRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
  }, [error]);

  const trimmed = text.trim();
  const tooShort = trimmed.length > 0 && trimmed.length < claimLimits.min;
  const tooLong = trimmed.length > claimLimits.max;
  const canSubmit = trimmed.length >= claimLimits.min && !tooLong && busy === null;

  async function run(input: { text?: string; exampleId?: string }, key: string) {
    setBusy(key);
    setError(null);

    try {
      const analysis = await api.analyze(input, storedLanguage());
      navigate(`/r/${analysis.id}`);
    } catch (cause) {
      setError(
        cause instanceof ApiError
          ? cause
          : new ApiError('unknown', 'Something went wrong. Please try again.', true, 0),
      );
      setBusy(null);
    }
  }

  return (
    <main className="screen">
      <div className="topbar">
        <Brand />
        {/* Offering "Sign in" to somebody already signed in is the interface
            telling them it has not noticed. While the answer is still loading
            neither is shown, rather than showing the wrong one and swapping. */}
        {session.state === 'signed-in' && (
          <Link to="/my-checks" className="btn-outline">
            My checks
          </Link>
        )}
        {session.state === 'anonymous' && (
          <Link to="/sign-in" className="btn-outline">
            Sign in
          </Link>
        )}
      </div>

      <div className="stack" style={{ gap: 10 }}>
        <h1 style={{ fontSize: 34, lineHeight: 1.15 }}>Check it before you share it.</h1>
        <p className="lede">
          Paste a civic claim — a levy, a payment demand, an outbreak alert — and see what verified
          official sources actually say.
        </p>
      </div>

      <form
        className="card"
        style={{ gap: 8 }}
        onSubmit={(event) => {
          event.preventDefault();
          if (canSubmit) {
            void run({ text: trimmed }, 'form');
          }
        }}
      >
        <label htmlFor="claim" style={{ fontSize: 13, fontWeight: 600 }}>
          Claim to check
        </label>
        <textarea
          id="claim"
          className="field"
          value={text}
          onChange={(event) => setText(event.target.value)}
          placeholder={'e.g. “Lagos State says every shop owner must pay a new levy before Friday…”'}
          aria-describedby="claim-help"
        />
        <div id="claim-help" className="spread" style={{ fontSize: 12, color: 'var(--muted)' }}>
          <span style={{ color: tooShort ? 'var(--contradicted-fg)' : undefined }}>
            Minimum {claimLimits.min} characters
          </span>
          <span style={{ color: tooLong ? 'var(--contradicted-fg)' : undefined }}>
            {trimmed.length.toLocaleString()} / {claimLimits.max.toLocaleString()}
          </span>
        </div>
        <button
          type="submit"
          className="btn btn-primary"
          style={{ marginTop: 4 }}
          disabled={!canSubmit}
        >
          {busy === 'form' ? (
            <>
              <span className="spinner" />
              Checking…
            </>
          ) : (
            'Check this claim'
          )}
        </button>
      </form>

      <div ref={errorRef}>{error && <ErrorPanel error={error} />}</div>

      {examples.length > 0 && (
        <div className="stack" style={{ gap: 10 }}>
          <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--muted)' }}>
            Or try an example
          </span>
          <div className="stack" style={{ gap: 8 }}>
            {examples.map((example) => (
              <button
                key={example.id}
                type="button"
                className="card"
                style={{
                  minHeight: 44,
                  padding: '10px 14px',
                  fontSize: 14,
                  textAlign: 'left',
                  cursor: 'pointer',
                  border: 'none',
                  color: 'var(--ink)',
                }}
                disabled={busy !== null}
                onClick={() => void run({ exampleId: example.id }, example.id)}
              >
                {busy === example.id ? 'Checking…' : `“${example.claim}”`}
              </button>
            ))}
          </div>
        </div>
      )}

      <div className="panel">
        <strong style={{ fontSize: 13, fontWeight: 700, color: 'var(--supported-fg)' }}>
          What Ukweli can check today
        </strong>
        <span style={{ fontSize: 13, lineHeight: 1.55 }}>
          Nigeria disease outbreak reports (NCDC). Lagos State payments and levies are not covered
          yet, so those claims return “insufficient evidence”.
        </span>
      </div>

      {session.state === 'anonymous' && (
        <div className="panel-dashed">
          <span style={{ fontSize: 13, lineHeight: 1.55, color: 'var(--muted)' }}>
            <Link to="/sign-in">Sign in</Link> to keep your checks. You can check claims
            without an account — they just are not saved.
          </span>
        </div>
      )}

      <div className="stack push-down" style={{ gap: 12 }}>
        <span className="fine">
          Please don’t paste names, phone numbers, IDs or personal stories — just the claim itself.
        </span>
        <span className="fine" style={{ borderTop: '1px solid var(--line-soft)', paddingTop: 12 }}>
          Ukweli answers only from verified sources. When it doesn’t know, it says so.
        </span>
      </div>
    </main>
  );
}
