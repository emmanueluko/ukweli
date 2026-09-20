import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  api,
  ApiError,
  formatDate,
  rememberLanguage,
  storedLanguage,
  type Analysis,
  type Language,
} from '../api';
import { chrome } from '../copy';
import { ErrorPanel, Loading } from '../components/Feedback';
import { LanguageSwitcher } from '../components/LanguageSwitcher';
import { ScreenHeader } from '../components/ScreenHeader';
import { SourceCard } from '../components/SourceCard';
import { VerdictBanner } from '../components/Verdict';

export function Result() {
  const { id = '' } = useParams();
  const [analysis, setAnalysis] = useState<Analysis | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [simple, setSimple] = useState(false);
  const [copied, setCopied] = useState(false);
  const [language, setLanguage] = useState<Language>(storedLanguage);
  // Only the prose is being replaced, so the rest of the screen stays put.
  const [switching, setSwitching] = useState(false);
  // null while unknown, so nothing flashes on screen before the answer arrives.
  const [signedIn, setSignedIn] = useState<boolean | null>(null);

  const load = useCallback(() => {
    setError(null);
    api
      .result(id, language)
      .then(setAnalysis)
      .catch((cause: unknown) => {
        setError(
          cause instanceof ApiError
            ? cause
            : new ApiError('unknown', 'Could not load this result.', true, 0),
        );
      });
    // Deliberately not keyed on language: switching is handled below so that
    // the verdict and the sources never blank out for something that cannot
    // change between languages.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  useEffect(load, [load]);

  useEffect(() => {
    // 404 means auth is switched off entirely, 401 means simply not signed in.
    // Neither is worth surfacing here — both just mean "no account to save to".
    api
      .me()
      .then(() => setSignedIn(true))
      .catch(() => setSignedIn(false));
  }, []);

  function switchLanguage(next: Language) {
    if (next === language || switching) {
      return;
    }

    setLanguage(next);
    rememberLanguage(next);
    setSwitching(true);

    api
      .result(id, next)
      .then(setAnalysis)
      .catch(() => {
        // The result on screen is still correct; only this rendering of it
        // failed. Going back to the language actually shown is more honest
        // than leaving the switcher pointing at one the reader is not reading.
        setLanguage((current) => (analysis ? analysis.language : current));
      })
      .finally(() => setSwitching(false));
  }

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

  // The language actually being read, which is English whenever a translation
  // could not be produced. Labelling English as Pidgin would be a quiet lie.
  const shown = analysis.language;
  const words = chrome(shown);
  const checkedOn = formatDate(analysis.checkedOn);
  // The short form exists to be read quickly; it carries the same evidence.
  const explanation =
    simple && analysis.simpleExplanation ? analysis.simpleExplanation : analysis.explanation;

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
      <ScreenHeader
        backTo="/check"
        backLabel={words.newCheck}
        action={{ to: '/my-checks', label: words.myChecks }}
      />

      <div className="spread" style={{ alignItems: 'center' }}>
        <span className="eyebrow">{words.claimAsAnalyzed}</span>
        <LanguageSwitcher
          value={language}
          options={analysis.availableLanguages}
          pending={switching}
          onChange={switchLanguage}
        />
      </div>

      {/* The claim is the user's own sentence, recorded as the system read it.
          It is not translated: it is what was asked, not what Ukweli answered. */}
      <h1 className="claim" lang="en">
        {analysis.normalizedClaim}
      </h1>

      {analysis.translationNote && (
        <p className="fine" role="status" style={{ margin: 0 }}>
          {analysis.translationNote}
        </p>
      )}

      <VerdictBanner status={analysis.status} checkedOn={checkedOn} />

      <section className="stack" style={{ gap: 10 }} aria-busy={switching}>
        <div className="spread">
          <h2 className="section-title">{words.why}</h2>
          {analysis.simpleExplanation && (
            <div className="toggle" role="group" aria-label="Explanation detail">
              <button type="button" aria-pressed={!simple} onClick={() => setSimple(false)}>
                {words.standard}
              </button>
              <button type="button" aria-pressed={simple} onClick={() => setSimple(true)}>
                {words.simple}
              </button>
            </div>
          )}
        </div>
        <p className="body" lang={shown} style={switching ? { opacity: 0.5 } : undefined}>
          {switching ? words.translating : explanation}
        </p>
      </section>

      {analysis.sources.length > 0 ? (
        <section className="stack" style={{ gap: 10 }}>
          <h2 className="section-title">{words.evidence(analysis.sources.length)}</h2>
          {/* Excerpts are quoted exactly as published, in the language the
              authority published them in — a translated quotation is no longer
              a quotation. */}
          {analysis.sources.map((source) => (
            <SourceCard key={source.id} source={source} />
          ))}
        </section>
      ) : (
        <section className="panel-dashed">
          <strong style={{ fontSize: 13, fontWeight: 700, color: 'var(--muted)' }}>
            {words.noSources}
          </strong>
          <span style={{ fontSize: 14, lineHeight: 1.55, color: 'var(--muted)' }} lang={shown}>
            {words.noSourcesBody}
          </span>
        </section>
      )}

      {analysis.unknowns.length > 0 && (
        <section className="stack" style={{ gap: 8 }}>
          <h2 className="section-title">{words.unknowns}</h2>
          <ul className="unknowns" lang={shown}>
            {analysis.unknowns.map((unknown) => (
              <li key={unknown}>{unknown}</li>
            ))}
          </ul>
        </section>
      )}

      <section className="panel">
        <strong style={{ fontSize: 13, fontWeight: 700, color: 'var(--supported-fg)' }}>
          {words.safeStep}
        </strong>
        <span style={{ fontSize: 14, lineHeight: 1.55 }} lang={shown}>
          {analysis.action}
        </span>
        <span className="meta">
          {analysis.actionIsGeneric ? words.generic : words.grounded} {words.notEmergency}
        </span>
      </section>

      {signedIn === false && (
        <div className="panel-dashed">
          <strong style={{ fontSize: 13, fontWeight: 700, color: 'var(--muted)' }}>
            {words.notSaved}
          </strong>
          <span style={{ fontSize: 13, lineHeight: 1.55, color: 'var(--muted)' }} lang={shown}>
            {words.notSavedBody.split(words.signIn)[0]}
            <Link to="/sign-in">{words.signIn}</Link>
            {words.notSavedBody.split(words.signIn)[1]}
          </span>
        </div>
      )}

      <div className="stack push-down" style={{ gap: 8 }}>
        <button type="button" className="btn btn-primary" onClick={() => void copyShareText()}>
          {copied ? words.copied : words.copy}
        </button>
        {/* Shown in full, so nobody forwards a summary without seeing it first. */}
        <p className="fine" lang={shown} style={{ margin: 0 }}>
          {analysis.shareText}
        </p>
        <Link to="/check" className="fine" style={{ textDecoration: 'underline' }}>
          {words.checkAnother}
        </Link>
      </div>
    </main>
  );
}
