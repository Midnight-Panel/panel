export const routing = {
  locales: ["en"] as const,
  defaultLocale: "en",
} as const;

export type Locale = (typeof routing.locales)[number];

export const isLocale = (v: unknown): v is Locale =>
  routing.locales.includes(v as Locale);

/** No right-to-left locale is shipped, kept for the layout helpers. */
export const isRtl = (_locale: Locale): boolean => false;
