import i18n from "i18next";
import ICU from "i18next-icu";
import { initReactI18next } from "react-i18next";

import de from "../messages/de.json";
import en from "../messages/en.json";
import { isLocale, routing } from "./routing";

const STORAGE_KEY = "midnight.language";

function detectLocale(): string {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (isLocale(stored)) return stored;
  } catch {
    // Storage blocked, fall back to the browser language
  }

  const browser = navigator.language.split("-")[0].toLowerCase();
  return isLocale(browser) ? browser : routing.defaultLocale;
}

i18n.on("languageChanged", (lng) => {
  const locale = isLocale(lng) ? lng : routing.defaultLocale;
  document.documentElement.lang = locale;
  document.documentElement.dir = "ltr";

  try {
    localStorage.setItem(STORAGE_KEY, locale);
  } catch {
    // Not persisted, still switched for this session
  }
});

i18n
  .use(ICU)
  .use(initReactI18next)
  .init({
    resources: { en: { translation: en }, de: { translation: de } },
    lng: detectLocale(),
    supportedLngs: [...routing.locales],
    fallbackLng: routing.defaultLocale,
    load: "languageOnly",
    keySeparator: ".",
    nsSeparator: false,
    returnNull: false,
    interpolation: { escapeValue: false },
    react: { useSuspense: false },
  });

export default i18n;
