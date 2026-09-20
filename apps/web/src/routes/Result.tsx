import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, ApiError, formatDate, type Analysis } from '../api';
import { ErrorPanel, Loading } from '../components/Feedback';
import { ScreenHeader } from '../components/ScreenHeader';
import { SourceCard } from '../components/SourceCard';
import { VerdictBanner } from '../components/Verdict';

export function Result() {
  const { id = '' } = useParams();
  const [analysis, setAnalysis] = useState<Analysis | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [simple, setSimple] = useState(false);
  const [copied, setCopied] = useState(false);

  const load = useCallback(() => {
    setError(null);
    api
      .result(id)
      .then(setAnalysis)
      .catch((cause: unknown) => {
        setError(
          cause instanceof ApiError
            ? cause
            : new ApiError('unknown', 'Could not load this result.', true, 0),
        );
      });
  }, [id]);

  useEffect(load, [load]);

  if (error) {
    return (
      <main className="screen">
        <ScreenHeader backTo="/check" backLabel="New check" />
        <ErrorPanel error={error} onRetry={error.retryable ? load : undefined} />
      </main>
    );
  }

  if (!analysis) {
    return <Loading label="Loading this result…" />;
  }

  const checkedOn = formatDate(analysis.checkedOn);
  // The short form exists to be read quickly; it carries the same evidence.
  const explanation = simple && analysis.simpleExplanation ? analysis.simpleExplanation : analysis.explanation;

  async function copyShareText() {
    try {
      await navigator.clipboard.writeText(analysis!.shareText);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2400);
    } catch {
      // Clipboard access can be refused; the text is on screen either way.
      setCopied(false);
    }
  }

  return (
    <main className="screen">
      <ScreenHeader backTo="/check" backLabel="New check" action={{ to: '/my-checks', label: 'My checks' }} />

      <div className="stack" style={{ gap: 6 }}>
        <span className="eyebrow">Claim as analyzed</span>
        <h1 className="claim">{analysis.normalizedClaim}</h1>
      </div>

      <VerdictBanner status={analysis.status} checkedOn={checkedOn} />

      <section className="stack" style={{ gap: 10 }}>
        <div className="spread">
          <h2 className="section-title">Why</h2>
          {analysis.simpleExplanation && (
            <div className="toggle" role="group" aria-label="Explanation detail">
              <button type="button" aria-pressed={!simple} onClick={() => setSimple(false)}>
                Standard
              </button>
              <button type="button" aria-pressed={simple} onClick={() => setSimple(true)}>
                Simple English
              </button>
            </div>
          )}
        </div>
        <p className="body">{explanation}</p>
      </section>

      {analysis.sources.length > 0 ? (
        <section className="stack" style={{ gap: 10 }}>
          <h2 className="section-title">
            Evidence ({analysis.sources.length} {analysis.sources.length === 1 ? 'source' : 'sources'})
          </h2>
          {analysis.sources.map((source) => (
            <SourceCard key={source.id} source={source} />
          ))}
        </section>
      ) : (
        <section className="panel-dashed">
          <strong style={{ fontSize: 13, fontWeight: 700, color: 'var(--muted)' }}>
            No matching sources
          </strong>
          <span style={{ fontSize: 14, lineHeight: 1.55, color: 'var(--muted)' }}>
            Nothing in the curated store speaks to this claim. That is not evidence the claim is
            false — it means Ukweli has no official record either way.
          </span>
        </section>
      )}

      {analysis.unknowns.length > 0 && (
        <section className="stack" style={{ gap: 8 }}>
          <h2 className="section-title">What remains unknown</h2>
          <ul className="unknowns">
            {analysis.unknowns.map((unknown) => (
              <li key={unknown}>{unknown}</li>
            ))}
          </ul>
        </section>
      )}

      <section className="panel">
        <strong style={{ fontSize: 13, fontWeight: 700, color: 'var(--supported-fg)' }}>
          Safe next step
        </strong>
        <span style={{ fontSize: 14, lineHeight: 1.55 }}>{analysis.action}</span>
        <span className="meta">
          {analysis.actionIsGeneric
            ? 'A general caution, because no cited excerpt supports a more specific step.'
            : 'Grounded in the cited evidence above.'}{' '}
          Ukweli is not for emergencies or legal advice.
        </span>
      </section>

      <div className="stack push-down" style={{ gap: 8 }}>
        <button type="button" className="btn btn-primary" onClick={() => void copyShareText()}>
          {copied ? 'Copied' : 'Copy share summary'}
        </button>
        {/* Shown in full, so nobody forwards a summary without seeing it first. */}
        <p className="fine" style={{ margin: 0 }}>
          {analysis.shareText}
        </p>
        <Link to="/check" className="fine" style={{ textDecoration: 'underline' }}>
          Check another claim
        </Link>
      </div>
    </main>
  );
}
