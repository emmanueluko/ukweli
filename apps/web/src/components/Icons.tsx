/**
 * The icons drawn in the design, as inline stroke SVG so they take the
 * surrounding text colour. No icon font, no emoji.
 */
interface IconProps {
  size?: number;
}

const base = {
  viewBox: '0 0 24 24',
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 2,
  strokeLinecap: 'round' as const,
  strokeLinejoin: 'round' as const,
};

export function CheckMark({ size = 19 }: IconProps) {
  return (
    <svg {...base} width={size} height={size} strokeWidth={2.6} aria-hidden="true">
      <path d="M5 12.5l4.5 4.5L19 7.5" />
    </svg>
  );
}

export function SupportedIcon({ size = 26 }: IconProps) {
  return (
    <svg {...base} width={size} height={size} aria-hidden="true">
      <circle cx="12" cy="12" r="9" />
      <path d="M8.5 12.5l2.5 2.5 4.5-5" />
    </svg>
  );
}

export function ContradictedIcon({ size = 26 }: IconProps) {
  return (
    <svg {...base} width={size} height={size} aria-hidden="true">
      <path d="M8 3h8l5 5v8l-5 5H8l-5-5V8z" />
      <path d="M9.5 9.5l5 5" />
      <path d="M14.5 9.5l-5 5" />
    </svg>
  );
}

export function UnclearIcon({ size = 26 }: IconProps) {
  return (
    <svg {...base} width={size} height={size} aria-hidden="true">
      <circle cx="12" cy="12" r="9" />
      <path d="M9.7 9.3a2.4 2.4 0 1 1 3.4 2.2c-.7.3-1.1.9-1.1 1.6v.2" />
      <circle cx="12" cy="16.6" r="0.6" fill="currentColor" stroke="none" />
    </svg>
  );
}

export function BackArrow({ size = 18 }: IconProps) {
  return (
    <svg {...base} width={size} height={size} aria-hidden="true">
      <path d="M19 12H5" />
      <path d="M11 18l-6-6 6-6" />
    </svg>
  );
}

export function ExternalLink({ size = 18 }: IconProps) {
  return (
    <svg {...base} width={size} height={size} aria-hidden="true">
      <path d="M14 5h5v5" />
      <path d="M19 5l-8 8" />
      <path d="M9 5H6a2 2 0 0 0-2 2v11a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2v-3" />
    </svg>
  );
}

export function EnvelopeIcon({ size = 32 }: IconProps) {
  return (
    <svg {...base} width={size} height={size} aria-hidden="true">
      <rect x="3" y="5" width="18" height="14" rx="2" />
      <path d="M3 7l9 6 9-6" />
    </svg>
  );
}
