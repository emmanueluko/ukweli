import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api, ApiError, type Analysis } from '../api';
import { Brand } from '../components/Brand';
import { Loading } from '../components/Feedback';
import { ScreenHeader } from '../components/ScreenHeader';
import { VerdictTag } from '../components/Verdict';

export function MyChecks() {
  const navigate = useNavigate();
  const [analyses, setAnalyses] = useState<Analysis[] | null>(null);
  const [email, setEmail] = useState<string | null>(null);
  const [state, setState] = useState<'loading' | 'ready' | 'anonymous' | 'disabled'>('loading');

  const load = useCallback(async () => {
    try {
      const [me, rows] = await Promise.all([api.me(), api.myAnalyses()]);
      setEmail(me.email);
      setAnalyses(rows);
      setState('ready');
    } catch (cause) {
      // 404 means auth is switched off entirely; 401 means simply not signed in.
      // They are different situations and get different screens.
      if (cause instanceof ApiError && cause.status === 404) {
        setState('disabled');
      } else {
        setState('anonymous');
      }
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  if (state === 'loading') {
    return <Loading label="Loading your checks…" />;
  }

  if (state === 'disabled') {
    return (
      <main className="screen">
        <ScreenHeader backTo="/check" backLabel="New check" />
        <h1 style={{ fontSize: 30 }}>My checks</h1>
        <div className="panel-dashed">
          <strong style={{ fontSize: 13, fontWeight: 700, color: 'var(--muted)' }}>
            Accounts are switched off
          </strong>
          <span style={{ fontSize: 13, lineHeight: 1.55, color: 'var(--muted)' }}>
            This deployment runs with <code>AUTH_ENABLED=false</code>, so there is no sign-in and
            no saved history. Checking claims works exactly the same either way.
          </span>
        </div>
        <Link to="/check" className="btn btn-primary push-down">
          Check a claim
        </Link>
      </main>
    );
  }

  if (state === 'anonymous') {
    return (
      <main className="screen">
        <ScreenHeader backTo="/check" backLabel="New check" />
        <h1 style={{ fontSize: 30 }}>My checks</h1>
        <div className="panel">
          <strong style={{ fontSize: 13, fontWeight: 700, color: 'var(--supported-fg)' }}>
            Sign in to see your saved checks
          </strong>
          <span style={{ fontSize: 13, lineHeight: 1.55 }}>
            Your history is the only thing an account unlocks. Checking claims never needs one.
          </span>
        </div>
        <Link to="/sign-in" className="btn btn-primary">
          Sign in
        </Link>
      </main>
    );
  }

  return (
    <main className="screen">
      <div className="topbar">
        <Link to="/check" className="btn-quiet">
          ← New check
        </Link>
        <Brand to="/" size="sm" />
      </div>

      <div className="spread" style={{ gap: 12 }}>
        <h1 className="page-title">My checks</h1>
        {analyses && analyses.length > 0 && (
          <Link to="/check" className="btn-outline">
            New check
          </Link>
        )}
      </div>

      {analyses && analyses.length > 0 ? (
        <div className="stack" style={{ gap: 10 }}>
          {analyses.map((analysis) => (
            <Link
              key={analysis.id}
              to={`/r/${analysis.id}`}
              className="card"
              style={{ gap: 8, textDecoration: 'none', color: 'var(--ink)' }}
            >
              <div className="spread">
                <VerdictTag status={analysis.status} />
              </div>
              <span style={{ fontSize: 14.5, lineHeight: 1.5 }}>{analysis.normalizedClaim}</span>
            </Link>
          ))}
        </div>
      ) : (
        <div className="stack" style={{ gap: 14 }}>
          <div className="panel-dashed">
            <strong style={{ fontSize: 13, fontWeight: 700, color: 'var(--muted)' }}>
              Nothing here yet
            </strong>
            <span style={{ fontSize: 13, lineHeight: 1.55, color: 'var(--muted)' }}>
              Checks you run while signed in are saved here, newest first. Anything you
              checked before signing in was not kept.
            </span>
          </div>
          <Link to="/check" className="btn btn-primary">
            Check a claim
          </Link>
        </div>
      )}

      <div className="panel-dashed">
        <strong style={{ fontSize: 13, fontWeight: 700, color: 'var(--muted)' }}>
          Only you can see this list
        </strong>
        <span style={{ fontSize: 13, lineHeight: 1.55, color: 'var(--muted)' }}>
          Ukweli saves the analyzed claim, never the raw message you pasted.
        </span>
      </div>

      <div
        className="spread push-down"
        style={{ borderTop: '1px solid var(--line-soft)', paddingTop: 14 }}
      >
        <span style={{ fontSize: 13, color: 'var(--muted)' }}>
          Signed in as <strong style={{ color: 'var(--ink)' }}>{email}</strong>
        </span>
        <button
          type="button"
          className="btn-quiet"
          onClick={() => {
            void api.signOut().finally(() => navigate('/check'));
          }}
        >
          Sign out
        </button>
      </div>
    </main>
  );
}
