import type { VerdictStatus } from '../api';
import { ContradictedIcon, SupportedIcon, UnclearIcon } from './Icons';

/**
 * How each verdict is named and coloured.
 *
 * `mixed_unclear` and `insufficient_evidence` are different verdicts and are
 * worded differently: one means the sources disagree, the other means there
 * were none. Collapsing them would lose the distinction the product exists for.
 */
export const verdictPresentation: Record<
  VerdictStatus,
  { word: string; short: string; className: string; caveatFallback: string }
> = {
  supported: {
    word: 'Supported',
    short: 'Supported',
    className: 'verdict-supported',
    caveatFallback: 'Checked against official sources',
  },
  contradicted: {
    word: 'Contradicted',
    short: 'Contradicted',
    className: 'verdict-contradicted',
    caveatFallback: 'Checked against official sources',
  },
  mixed_unclear: {
    word: 'Mixed or unclear',
    short: 'Mixed',
    className: 'verdict-unclear',
    caveatFallback: 'Sources do not agree',
  },
  insufficient_evidence: {
    word: 'Insufficient evidence',
    short: 'Insufficient',
    className: 'verdict-unclear',
    caveatFallback: 'We can’t confirm or deny this',
  },
};

export function VerdictIcon({ status, size = 26 }: { status: VerdictStatus; size?: number }) {
  if (status === 'supported') {
    return <SupportedIcon size={size} />;
  }
  if (status === 'contradicted') {
    return <ContradictedIcon size={size} />;
  }
  return <UnclearIcon size={size} />;
}

export function VerdictBanner({
  status,
  checkedOn,
}: {
  status: VerdictStatus;
  checkedOn: string | null;
}) {
  const presentation = verdictPresentation[status];

  return (
    <div className={`verdict ${presentation.className}`}>
      <VerdictIcon status={status} />
      <div className="stack">
        <span className="verdict-word">{presentation.word}</span>
        <span className="verdict-caveat">
          {checkedOn ? `As checked on ${checkedOn}` : presentation.caveatFallback}
        </span>
      </div>
    </div>
  );
}

/** The small pill used in lists and on cards. */
export function VerdictTag({ status }: { status: VerdictStatus }) {
  const presentation = verdictPresentation[status];

  return (
    <span
      className={`verdict ${presentation.className}`}
      style={{
        gap: 6,
        padding: '4px 8px',
        borderRadius: 6,
        fontSize: 12,
        fontWeight: 700,
      }}
    >
      <VerdictIcon status={status} size={13} />
      {presentation.short}
    </span>
  );
}
