import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api, ApiError, claimLimits, type ExampleClaim } from '../api';
import { Brand } from '../components/Brand';
import { ErrorPanel } from '../components/Feedback';
import { VerdictIcon, verdictPresentation } from '../components/Verdict';

export function Landing() {
  const navigate = useNavigate();
  const [text, setText] = useState('');
  const [examples, setExamples] = useState<ExampleClaim[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  useEffect(() => {
    api.examples().then(setExamples).catch(() => setExamples([]));
  }, []);

  const trimmed = text.trim();
  const canSubmit = trimmed.length >= claimLimits.min && trimmed.length <= claimLimits.max && !busy;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!canSubmit) {
      return;
    }

    setBusy(true);
    setError(null);

    try {
      const analysis = await api.analyze({ text: trimmed });
      navigate(`/r/${analysis.id}`);
    } catch (cause) {
      setError(
        cause instanceof ApiError
          ? cause
          : new ApiError('unknown', 'Something went wrong. Please try again.', true, 0),
      );
      setBusy(false);
    }
  }

  return (
    <div className="landing">
      <a href="#main" className="skip-link">
        Skip to content
      </a>

      <header className="landing-inner topbar" style={{ padding: '22px 24px' }}>
        <Brand to="/" />
        <nav style={{ display: 'flex', alignItems: 'center', gap: 20 }}>
          <a href="#how" style={{ fontSize: 15, fontWeight: 600, color: 'var(--ink)', textDecoration: 'none' }}>
            How it works
          </a>
          <Link to="/sign-in" style={{ fontSize: 15, fontWeight: 600, color: 'var(--ink)', textDecoration: 'none' }}>
            Sign in
          </Link>
          <Link to="/check" className="btn btn-primary" style={{ height: 46, padding: '0 20px', fontSize: 15 }}>
            Check a claim
          </Link>
        </nav>
      </header>

      <main id="main" className="landing-inner">
        <section className="hero">
          <div className="stack" style={{ gap: 24 }}>
            <h1>Before you share it, check it.</h1>
            <p style={{ margin: 0, fontSize: 18, lineHeight: 1.6, color: 'var(--muted)', maxWidth: 520 }}>
              That levy notice, payment demand or outbreak alert going around — Ukweli checks it
              against verified official sources and answers with evidence, or tells you honestly
              when it can’t.
            </p>
            <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
              <Link to="/check" className="btn btn-primary" style={{ height: 56, padding: '0 26px' }}>
                Check a claim now
              </Link>
              <a href="#how" className="btn btn-secondary" style={{ height: 56, padding: '0 26px', fontSize: 16 }}>
                See how it works
              </a>
            </div>
            <span style={{ fontSize: 14, color: 'var(--muted)' }}>
              Free, no account needed. Not for emergencies.
            </span>
          </div>

          <form
            className="card"
            style={{ gap: 14, padding: 26, borderRadius: 18, maxWidth: 520 }}
            onSubmit={(event) => void submit(event)}
          >
            <label htmlFor="hero-claim" style={{ fontSize: 14, fontWeight: 600 }}>
              Claim to check
            </label>
            <textarea
              id="hero-claim"
              className="field"
              style={{ borderRadius: 12, padding: 14 }}
              value={text}
              onChange={(event) => setText(event.target.value)}
              placeholder="Paste the forwarded message here…"
            />
            <div className="spread" style={{ fontSize: 12.5, color: 'var(--muted)' }}>
              <span>Checked against official sources only</span>
              <span>
                {trimmed.length.toLocaleString()} / {claimLimits.max.toLocaleString()}
              </span>
            </div>
            <button type="submit" className="btn btn-primary" style={{ height: 54 }} disabled={!canSubmit}>
              {busy ? (
                <>
                  <span className="spinner" />
                  Checking…
                </>
              ) : (
                'Check this claim'
              )}
            </button>
            {error && <ErrorPanel error={error} />}
          </form>
        </section>

        <section
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 20,
            padding: '26px 34px',
            background: 'var(--green-mist)',
            borderRadius: 18,
            flexWrap: 'wrap',
          }}
        >
          <strong style={{ fontSize: 15, fontWeight: 600, color: 'var(--green-dark)' }}>
            Answers come only from official sources
          </strong>
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
            <span className="pill">Nigeria Centre for Disease Control</span>
          </div>
          <span style={{ fontSize: 14, color: 'var(--muted)', marginLeft: 'auto' }}>
            Every verdict cites its evidence
          </span>
        </section>

        <section id="how" className="stack" style={{ gap: 40, padding: '96px 0 88px' }}>
          <h2 style={{ fontSize: 40 }}>How a check works</h2>
          <div className="grid-3" style={{ gap: 48 }}>
            {[
              {
                n: '1',
                title: 'Paste the claim',
                body: 'The forwarded message, the poster, the rumour — just the claim itself, no names or phone numbers.',
              },
              {
                n: '2',
                title: 'We check verified sources',
                body: 'Ukweli compares it against a hand-verified library of official documents — never the open internet.',
              },
              {
                n: '3',
                title: 'You get a cited verdict',
                body: 'Supported, contradicted or honestly unknown — with the exact excerpts, dates and links behind it.',
              },
            ].map((step) => (
              <div key={step.n} className="stack" style={{ gap: 10 }}>
                <span className="step-number">{step.n}</span>
                <strong style={{ fontSize: 18 }}>{step.title}</strong>
                <p style={{ margin: 0, fontSize: 15, lineHeight: 1.6, color: 'var(--muted)' }}>
                  {step.body}
                </p>
              </div>
            ))}
          </div>
        </section>

        {examples.length > 0 && (
          <section className="stack" style={{ gap: 36, paddingBottom: 96 }}>
            <div className="stack" style={{ gap: 12, maxWidth: 760 }}>
              <h2 style={{ fontSize: 40, lineHeight: 1.15 }}>Every answer shows its evidence</h2>
              <p style={{ margin: 0, fontSize: 16, lineHeight: 1.6, color: 'var(--muted)' }}>
                And when the evidence isn’t there, Ukweli never fills the gap with a guess.
              </p>
            </div>
            <div className="grid-3">
              {examples.map((example) => (
                <Link
                  key={example.id}
                  to={`/check?example=${encodeURIComponent(example.id)}`}
                  className="card"
                  style={{ gap: 12, padding: 22, borderRadius: 16, textDecoration: 'none', color: 'var(--ink)' }}
                >
                  <strong style={{ fontSize: 15, lineHeight: 1.5, fontWeight: 600 }}>
                    “{example.claim}”
                  </strong>
                  <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--green-dark)', marginTop: 'auto' }}>
                    Run this check
                  </span>
                </Link>
              ))}
            </div>
          </section>
        )}

        <section className="dark-panel">
          <h2 style={{ fontSize: 42, lineHeight: 1.15, color: 'var(--ivory)', maxWidth: 820 }}>
            When Ukweli doesn’t know, it says so.
          </h2>
          <div className="grid-3" style={{ gap: 40, marginTop: 8 }}>
            <p style={{ margin: 0, fontSize: 15, lineHeight: 1.6, color: '#d7e3d9' }}>
              No guesses. A claim outside our sources gets “insufficient evidence”, never a made-up
              answer.
            </p>
            <p style={{ margin: 0, fontSize: 15, lineHeight: 1.6, color: '#d7e3d9' }}>
              No hidden reasoning. Every verdict shows the exact excerpt, the publication date, and
              the day we last verified it.
            </p>
            <p style={{ margin: 0, fontSize: 15, lineHeight: 1.6, color: '#d7e3d9' }}>
              No confidence theatre. We tell you what remains unknown alongside what the evidence
              supports.
            </p>
          </div>
        </section>

        <section className="spread" style={{ padding: '72px 0', gap: 24, flexWrap: 'wrap' }}>
          <div className="stack" style={{ gap: 8 }}>
            <h2 style={{ fontSize: 32 }}>Something forwarded to you today?</h2>
            <span style={{ fontSize: 15, color: 'var(--muted)' }}>
              Covering NCDC outbreak reports for Nigeria. Lagos State payments and levies are not
              curated yet.
            </span>
          </div>
          <Link to="/check" className="btn btn-primary" style={{ height: 56, padding: '0 28px' }}>
            Check a claim now
          </Link>
        </section>
      </main>

      <footer
        className="landing-inner spread"
        style={{ padding: '24px 24px 40px', borderTop: '1px solid var(--line-soft)', gap: 16, flexWrap: 'wrap' }}
      >
        <Brand to="/" size="sm" />
        <nav style={{ display: 'flex', gap: 24, fontSize: 13.5 }}>
          <Link to="/sign-in" style={{ color: 'var(--muted)', textDecoration: 'none' }}>
            Sign in
          </Link>
          <Link to="/check" style={{ color: 'var(--muted)', textDecoration: 'none' }}>
            Check a claim
          </Link>
        </nav>
        <span style={{ fontSize: 13, color: 'var(--muted)' }}>
          Not for emergencies or legal advice
        </span>
      </footer>

      {/* Referenced so the verdict palette is exercised on this page too. */}
      <span hidden aria-hidden="true">
        <VerdictIcon status="supported" />
        {verdictPresentation.supported.word}
      </span>
    </div>
  );
}
