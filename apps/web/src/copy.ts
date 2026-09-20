import type { Language } from './api';

/**
 * The interface's own words, in each language Ukweli answers in.
 *
 * These are hand-written and fixed. A label that a model produced could drift
 * between visits, and "What remains unknown" is not a heading worth risking:
 * it is the part of the screen that keeps the product honest.
 *
 * This covers the chrome around a result. The result's own prose is translated
 * server-side, where the safety rules can be applied to it.
 */
export interface Chrome {
  claimAsAnalyzed: string;
  why: string;
  standard: string;
  simple: string;
  evidence: (count: number) => string;
  noSources: string;
  noSourcesBody: string;
  unknowns: string;
  safeStep: string;
  grounded: string;
  generic: string;
  notEmergency: string;
  copy: string;
  copied: string;
  checkAnother: string;
  newCheck: string;
  myChecks: string;
  notSaved: string;
  notSavedBody: string;
  signIn: string;
  loading: string;
  translating: string;
}

const english: Chrome = {
  claimAsAnalyzed: 'Claim as analyzed',
  why: 'Why',
  standard: 'Standard',
  simple: 'Simple English',
  evidence: (count) => `Evidence (${count} ${count === 1 ? 'source' : 'sources'})`,
  noSources: 'No matching sources',
  noSourcesBody:
    'Nothing in the curated store speaks to this claim. That is not evidence the claim is false — it means Ukweli has no official record either way.',
  unknowns: 'What remains unknown',
  safeStep: 'Safe next step',
  grounded: 'Grounded in the cited evidence above.',
  generic: 'A general caution, because no cited excerpt supports a more specific step.',
  notEmergency: 'Ukweli is not for emergencies or legal advice.',
  copy: 'Copy share summary',
  copied: 'Copied',
  checkAnother: 'Check another claim',
  newCheck: 'New check',
  myChecks: 'My checks',
  notSaved: 'This check is not being saved',
  notSavedBody: 'Copy the summary below to keep it, or sign in so future checks are saved to your account.',
  signIn: 'sign in',
  loading: 'Loading this result…',
  translating: 'Translating…',
};

const pidgin: Chrome = {
  claimAsAnalyzed: 'Di claim wey we check',
  why: 'Why',
  standard: 'Full talk',
  simple: 'Short talk',
  evidence: (count) => `Evidence (${count} ${count === 1 ? 'source' : 'sources'})`,
  noSources: 'We no see any source wey match',
  noSourcesBody:
    'Nothing for di store wey we curate talk about dis claim. Dat no mean say di claim na lie — e mean say Ukweli no get official record either way.',
  unknowns: 'Wetin we still no sabi',
  safeStep: 'Safe next step',
  grounded: 'E base on di evidence wey dey up there.',
  generic: 'Na general caution, because no excerpt wey we cite fit carry better step.',
  notEmergency: 'Ukweli no be for emergency or legal advice.',
  copy: 'Copy di share summary',
  copied: 'E don copy',
  checkAnother: 'Check anoda claim',
  newCheck: 'New check',
  myChecks: 'My checks',
  notSaved: 'We no dey save dis check',
  notSavedBody: 'Copy di summary below make you keep am, or sign in make we dey save your checks.',
  signIn: 'sign in',
  loading: 'We dey load dis result…',
  translating: 'We dey translate…',
};

const french: Chrome = {
  claimAsAnalyzed: 'Affirmation analysée',
  why: 'Pourquoi',
  standard: 'Version complète',
  simple: 'Version courte',
  evidence: (count) => `Éléments (${count} source${count === 1 ? '' : 's'})`,
  noSources: 'Aucune source correspondante',
  noSourcesBody:
    "Rien dans le fonds documentaire ne traite de cette affirmation. Cela ne prouve pas qu'elle soit fausse — cela veut dire qu'Ukweli n'a aucun document officiel dans un sens ou dans l'autre.",
  unknowns: "Ce qui reste inconnu",
  safeStep: 'Prochaine étape sûre',
  grounded: 'Fondée sur les éléments cités ci-dessus.',
  generic: "Une mise en garde générale, car aucun extrait cité ne permet d'être plus précis.",
  notEmergency: "Ukweli n'est pas un service d'urgence ni un conseil juridique.",
  copy: 'Copier le résumé à partager',
  copied: 'Copié',
  checkAnother: 'Vérifier une autre affirmation',
  newCheck: 'Nouvelle vérification',
  myChecks: 'Mes vérifications',
  notSaved: "Cette vérification n'est pas enregistrée",
  notSavedBody:
    'Copiez le résumé ci-dessous pour le conserver, ou connectez-vous pour enregistrer vos prochaines vérifications.',
  signIn: 'connectez-vous',
  loading: 'Chargement du résultat…',
  translating: 'Traduction en cours…',
};

export function chrome(language: Language): Chrome {
  return language === 'pcm' ? pidgin : language === 'fr' ? french : english;
}
