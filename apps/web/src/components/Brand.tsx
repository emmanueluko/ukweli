import { Link } from 'react-router-dom';

interface BrandProps {
  /** Where the wordmark goes. */
  to?: string;
  size?: 'sm' | 'md' | 'lg';
  /** Shows the strapline from the logo beneath the wordmark. */
  withTagline?: boolean;
}

const sizes = {
  sm: { mark: 28, radius: 8, text: 17 },
  md: { mark: 36, radius: 10, text: 24 },
  lg: { mark: 44, radius: 12, text: 28 },
} as const;

/**
 * The wordmark.
 *
 * The emblem is the logo artwork cropped to the head-and-Africa mark: the full
 * tile carries the word "Ukweli" inside it, which at 36px would be an
 * illegible smudge sitting next to the same word set in type. The full tile is
 * still used where it has room — the favicon, the app icon, the share image.
 */
export function Brand({ to = '/', size = 'md', withTagline = false }: BrandProps) {
  const { mark, radius, text } = sizes[size];

  return (
    <Link to={to} className="brand" aria-label="Ukweli — home">
      <img
        src="/ukweli-mark.png"
        alt=""
        aria-hidden="true"
        width={mark}
        height={mark}
        style={{ borderRadius: radius, flexShrink: 0, display: 'block' }}
      />
      <span className="stack">
        <span className="brand-name" style={{ fontSize: text, lineHeight: 1.1 }}>
          Ukweli
        </span>
        {withTagline && <span className="brand-tagline">Truth for stronger communities</span>}
      </span>
    </Link>
  );
}
