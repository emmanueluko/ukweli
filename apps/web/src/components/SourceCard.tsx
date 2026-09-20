import { Link } from 'react-router-dom';
import type { AnalysisSource } from '../api';
import { formatDate } from '../api';

const relationTag: Record<AnalysisSource['relation'], { label: string; className: string }> = {
  supports: { label: 'Supports', className: 'tag-supports' },
  conflicts: { label: 'Conflicts', className: 'tag-conflicts' },
  context: { label: 'Context', className: 'tag-neutral' },
};

export function SourceCard({ source }: { source: AnalysisSource }) {
  const published = formatDate(source.publishedAt);
  const checked = formatDate(source.checkedAt);

  return (
    <article className="card">
      <div className="spread">
        <span style={{ fontSize: 12.5, fontWeight: 700, color: 'var(--muted)' }}>
          {source.issuer}
        </span>
        <span style={{ display: 'flex', gap: 6 }}>
          <span className={`tag ${relationTag[source.relation].className}`}>
            {relationTag[source.relation].label}
          </span>
          <span className="tag tag-neutral">
            {source.sourceType === 'primary_official' ? 'Primary' : 'Secondary'}
          </span>
        </span>
      </div>

      <h3 style={{ fontFamily: 'var(--sans)', fontSize: 15, fontWeight: 600, lineHeight: 1.4 }}>
        {source.title}
      </h3>

      {/* Quoted, because it is quoted: this is the document's own wording. */}
      <blockquote className="excerpt">“{source.excerpt}”</blockquote>

      <div className="spread">
        <span className="meta">
          {published ? `Published ${published}` : 'Not dated on page'}
          {checked ? ` · Checked ${checked}` : ''}
        </span>
        <Link to={`/sources/${encodeURIComponent(source.id)}`} className="btn-quiet">
          View source
        </Link>
      </div>
    </article>
  );
}
