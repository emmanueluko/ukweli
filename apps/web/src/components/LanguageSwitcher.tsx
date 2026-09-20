import { languageNames, type Language } from '../api';

/**
 * Chooses the language a result is read in.
 *
 * The verdict, the sources and the dates are the same in every language, so
 * switching never reloads the page or clears what is on screen — only the
 * prose is replaced. The first switch to a language can take a few seconds
 * while it is produced; after that it is stored and instant.
 */
export function LanguageSwitcher({
  value,
  options,
  pending,
  onChange,
}: {
  value: Language;
  options: Language[];
  pending: boolean;
  onChange: (language: Language) => void;
}) {
  return (
    <div className="toggle" role="group" aria-label="Language">
      {options.map((option) => (
        <button
          key={option}
          type="button"
          lang={option}
          aria-pressed={option === value}
          // Disabled only while a switch is in flight, so two taps in a row
          // cannot leave the screen showing one language and labelled another.
          disabled={pending}
          onClick={() => onChange(option)}
        >
          {languageNames[option]}
        </button>
      ))}
    </div>
  );
}
