import type { ApiError } from '../api';

/** Shown while a check is running. */
export function Loading({ label }: { label: string }) {
  return (
    <div className="screen" style={{ justifyContent: 'center', alignItems: 'center' }}>
      <div role="status" aria-live="polite" className="stack" style={{ alignItems: 'center', gap: 12 }}>
        <span
          className="spinner"
          style={{ borderColor: 'var(--line)', borderTopColor: 'var(--green)' }}
        />
        <span className="lede">{label}</span>
      </div>
    </div>
  );
}

/**
 * An error, worded from the server's own message.
 *
 * `retryable` decides whether a retry is offered at all — telling someone to
 * try again when it cannot work is worse than saying nothing.
 */
export function ErrorPanel({ error, onRetry }: { error: ApiError; onRetry?: () => void }) {
  return (
    <div
      role="alert"
      className="panel"
      style={{ background: 'var(--contradicted-bg)', gap: 10 }}
    >
      <strong style={{ fontSize: 13, fontWeight: 700, color: 'var(--contradicted-fg)' }}>
        {error.retryable ? 'Ukweli could not finish this check' : 'That did not work'}
      </strong>
      <span style={{ fontSize: 14, lineHeight: 1.55 }}>{error.message}</span>
      {error.retryable && onRetry && (
        <button type="button" className="btn btn-secondary" onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  );
}
