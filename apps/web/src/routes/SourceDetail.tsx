import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { api, ApiError, formatDate, type SourceDetail as SourceDetailData } from '../api';
import { ErrorPanel, Loading } from '../components/Feedback';
import { ExternalLink } from '../components/Icons';
import { ScreenHeader } from '../components/ScreenHeader';

const topicLabel: Record<SourceDetailData['topic'], string> = {
  payments_levies: 'Payments & levies',
  disease_outbreaks: 'Disease outbreaks',
};

const jurisdictionLabel: Record<SourceDetailData['jurisdiction'], string> = {
  NG: 'Nigeria (NG)',
  'NG-LA': 'Lagos State (NG-LA)',
};

export function SourceDetail() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const [source, setSource] = useState<SourceDetailData | null>(null);
  const [error, setError] = useState<ApiError | null>(null);

  const load = useCallback(() => {
    setError(null);
    api
      .source(id)
      .then(setSource)
      .catch((cause: unknown) => {
        setError(
          cause instanceof ApiError
            ? cause
            : new ApiError('unknown', 'Could not load this source.', true, 0),
        );
      });
  }, [id]);

  useEffect(load, [load]);

  if (error) {
    return (
      <main className="screen">
        <ScreenHeader backTo="/check" backLabel="Back" />
        <ErrorPanel error={error} onRetry={error.retryable ? load : undefined} />
      </main>
    );
  }

  if (!source) {
    return <Loading label="Loading this source…" />;
  }

  const published = formatDate(source.publishedAt);
  const checked = formatDate(source.checkedAt);
  const initials = source.issuer
    .split(' ')
    .filter((word) => /^[A-Z]/.test(word))
    .slice(0, 2)
    .map((word) => word[0])
    .join('');

  return (
    <main className="screen">
      <div className="topbar">
        <button type="button" className="btn-quiet" onClick={() => navigate(-1)}>
          ← Back to result
        </button>
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <span
          aria-hidden="true"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 52,
            height: 52,
            flexShrink: 0,
            background: 'var(--green-dark)',
            color: '#f7f5ef',
            borderRadius: 12,
            fontFamily: 'var(--serif)',
            fontSize: 15,
            fontWeight: 700,
          }}
        >
          {initials || 'UK'}
        </span>
        <div className="stack" style={{ gap: 2 }}>
          <span style={{ fontSize: 13, fontWeight: 700, color: 'var(--muted)' }}>
            {source.issuer}
          </span>
          <h1 style={{ fontSize: 19, lineHeight: 1.25 }}>{source.title}</h1>
        </div>
      </div>

      <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
        <span
          className={`tag ${source.sourceType === 'primary_official' ? 'tag-supports' : 'tag-neutral'}`}
          style={{ padding: '5px 9px' }}
        >
          {source.sourceType === 'primary_official' ? 'Primary source' : 'Secondary source'}
        </span>
        <span className="tag tag-neutral" style={{ padding: '5px 9px' }}>
          {jurisdictionLabel[source.jurisdiction]}
        </span>
        <span className="tag tag-neutral" style={{ padding: '5px 9px' }}>
          {topicLabel[source.topic]}
        </span>
      </div>

      <section className="card" style={{ gap: 8 }}>
        <span className="eyebrow">Verified excerpt</span>
        <blockquote style={{ margin: 0, fontSize: 15, lineHeight: 1.6, fontStyle: 'italic' }}>
          “{source.excerpt}”
        </blockquote>
      </section>

      <section className="card">
        <div className="spread" style={{ fontSize: 14 }}>
          <span style={{ color: 'var(--muted)' }}>Published</span>
          <span style={{ fontWeight: 600 }}>{published ?? 'Not dated on page'}</span>
        </div>
        <div
          className="spread"
          style={{ fontSize: 14, borderTop: '1px solid var(--line-faint)', paddingTop: 10 }}
        >
          <span style={{ color: 'var(--muted)' }}>Last verified by Ukweli</span>
          <span style={{ fontWeight: 600 }}>{checked ?? 'Not recorded'}</span>
        </div>
        {source.collectionUrl && (
          <div
            className="spread"
            style={{ fontSize: 14, borderTop: '1px solid var(--line-faint)', paddingTop: 10 }}
          >
            <span style={{ color: 'var(--muted)' }}>Found in collection</span>
            <span style={{ fontWeight: 600, textAlign: 'right', wordBreak: 'break-word' }}>
              {source.collectionUrl.replace(/^https?:\/\//, '')}
            </span>
          </div>
        )}
      </section>

      <a
        href={source.url}
        className="btn btn-primary"
        target="_blank"
        rel="noopener noreferrer"
      >
        <span>Open original document</span>
        <ExternalLink />
      </a>

      {source.externalContentMayChange && (
        <div className="panel-caution push-down">
          <strong style={{ fontSize: 13, fontWeight: 700, color: 'var(--caution-fg)' }}>
            External pages can change
          </strong>
          <span style={{ fontSize: 13, lineHeight: 1.55 }}>
            The excerpt above is what Ukweli verified{checked ? ` on ${checked}` : ''}. The live
            document may have been updated or moved since.
          </span>
        </div>
      )}
    </main>
  );
}
