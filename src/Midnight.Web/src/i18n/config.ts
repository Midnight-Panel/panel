import i18n from "i18next";
import ICU from "i18next-icu";
import { initReactI18next } from "react-i18next";

import en from "../messages/en.json";
import { isLocale, routing } from "./routing";

const STORAGE_KEY = "midnight.language";

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

// The panel is English only, older installs may still have "de" stored
const savedLang = (() => {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    return isLocale(stored) ? stored : routing.defaultLocale;
  } catch {
    return routing.defaultLocale;
  }
})();

i18n
  .use(ICU)
  .use(initReactI18next)
  .init({
    resources: {
      en: { translation: en },
    },
    lng: savedLang,
    supportedLngs: [...routing.locales],
    fallbackLng: "en",
    load: "languageOnly",
    keySeparator: ".",
    nsSeparator: false,
    returnNull: false,
    interpolation: { escapeValue: false },
    react: { useSuspense: false },
  });

export default i18n;
