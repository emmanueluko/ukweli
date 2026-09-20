/**
 * The Ukweli API, as the server actually returns it.
 *
 * These types mirror `Ukweli.Contracts` exactly. There is deliberately no
 * confidence or score field anywhere — the server does not produce one, and a
 * client that invented one would be contradicting the whole product.
 */

export type VerdictStatus =
  | 'supported'
  | 'contradicted'
  | 'mixed_unclear'
  | 'insufficient_evidence';

export type SourceRelation = 'supports' | 'conflicts' | 'context';
export type SourceType = 'primary_official' | 'secondary_trusted';
export type Jurisdiction = 'NG' | 'NG-LA';
export type Topic =
  | 'payments_levies'
  | 'disease_outbreaks'
  | 'health_products'
  | 'identity_documents'
  | 'emergencies'
  | 'elections';

/** The languages Ukweli answers in. */
export type Language = 'en' | 'pcm' | 'fr';

/**
 * How each language names itself. A switcher that labels Pidgin "Nigerian
 * Pidgin (pcm)" is written for a developer; this is written for a reader.
 */
export const languageNames: Record<Language, string> = {
  en: 'English',
  pcm: 'Pidgin',
  fr: 'Français',
};

export interface AnalysisSource {
  id: string;
  issuer: string;
  title: string;
  url: string;
  excerpt: string;
  publishedAt: string | null;
  checkedAt: string | null;
  relation: SourceRelation;
  sourceType: SourceType;
  placeholder: boolean;
}

export interface Analysis {
  id: string;
  normalizedClaim: string;
  jurisdiction: Jurisdiction | null;
  status: VerdictStatus;
  explanation: string;
  simpleExplanation: string;
  unknowns: string[];
  action: string;
  actionIsGeneric: boolean;
  sources: AnalysisSource[];
  checkedOn: string | null;
  shareText: string;
  /**
   * The language this rendering is actually in — not always the one asked for.
   * When a translation cannot be produced the server returns English and sets
   * `translationNote`, rather than showing English under another language's
   * label.
   */
  language: Language;
  availableLanguages: Language[];
  translationNote: string | null;
}

export interface ExampleClaim {
  id: string;
  claim: string;
  topic: Topic;
  jurisdiction: Jurisdiction | null;
}

export interface SourceDetail {
  id: string;
  issuer: string;
  title: string;
  sourceType: SourceType;
  jurisdiction: Jurisdiction;
  topic: Topic;
  publishedAt: string | null;
  checkedAt: string | null;
  url: string;
  collectionUrl: string | null;
  excerpt: string;
  placeholder: boolean;
  externalContentMayChange: boolean;
}

/**
 * The server's error envelope. `retryable` is the difference between "try
 * again" and "this will never work", and the interface should say which.
 */
export class ApiError extends Error {
  constructor(
    readonly code: string,
    message: string,
    readonly retryable: boolean,
    readonly status: number,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;

  try {
    response = await fetch(path, {
      ...init,
      // Sessions are cookie-based and same-origin.
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', ...init?.headers },
    });
  } catch {
    throw new ApiError(
      'network',
      'Could not reach Ukweli. Check your connection and try again.',
      true,
      0,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const body: unknown = await response.json().catch(() => null);

  if (!response.ok) {
    // The server's own message is written for a reader, so it is shown as-is.
    const envelope = body as { error?: { code: string; message: string; retryable: boolean } };

    throw new ApiError(
      envelope?.error?.code ?? 'unknown',
      envelope?.error?.message ?? 'Something went wrong. Please try again.',
      envelope?.error?.retryable ?? false,
      response.status,
    );
  }

  return body as T;
}

export const api = {
  analyze: (input: { text?: string; exampleId?: string }, language: Language = 'en') =>
    request<Analysis>(`/api/analyze?lang=${language}`, {
      method: 'POST',
      body: JSON.stringify(input),
    }),

  /**
   * Re-reads a stored result. Asking for another language returns the same
   * verdict, the same sources and the same dates — only the prose changes, and
   * excerpts are always quoted as the authority published them.
   */
  result: (id: string, language: Language = 'en') =>
    request<Analysis>(`/api/results/${encodeURIComponent(id)}?lang=${language}`),

  examples: () => request<ExampleClaim[]>('/api/examples'),

  source: (id: string) => request<SourceDetail>(`/api/sources/${encodeURIComponent(id)}`),

  health: () => request<{ ok: boolean; db: boolean; modelConfigured: boolean }>('/healthz'),

  requestMagicLink: (email: string) =>
    request<{ message: string }>('/api/auth/magic-link', {
      method: 'POST',
      body: JSON.stringify({ email }),
    }),

  me: () => request<{ email: string; createdAt: string }>('/api/me'),

  myAnalyses: (language: Language = 'en') =>
    request<Analysis[]>(`/api/me/analyses?lang=${language}`),

  signOut: () => request<{ authenticated: boolean }>('/api/auth/sign-out', { method: 'POST' }),
};

/** The limits the server enforces, repeated here only to guide the field. */
export const claimLimits = { min: 20, max: 1000 } as const;

/** The language the reader last chose, remembered across visits. */
const languageKey = 'ukweli.language';

export function storedLanguage(): Language {
  try {
    const saved = localStorage.getItem(languageKey);
    if (saved === 'en' || saved === 'pcm' || saved === 'fr') {
      return saved;
    }
  } catch {
    // Private browsing, or storage turned off. English is the fallback.
  }

  return 'en';
}

export function rememberLanguage(language: Language): void {
  try {
    localStorage.setItem(languageKey, language);
  } catch {
    // Not being able to remember the choice is not a reason to refuse it.
  }
}

/** "2026-08-22" → "22 Aug 2026", the form the design uses throughout. */
export function formatDate(value: string | null | undefined): string | null {
  if (!value) {
    return null;
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  return date.toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });
}
