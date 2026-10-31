import i18n from "i18next";
import ICU from "i18next-icu";
import { initReactI18next } from "react-i18next";

import en from "../messages/en.json";
import de from "../messages/de.json";
import { routing } from "./routing";

const STORAGE_KEY = "midnight.language";

i18n.on("languageChanged", (lng) => {
  const locale = lng === "de" || lng === "en" ? lng : routing.defaultLocale;
  document.documentElement.lang = locale;
  document.documentElement.dir = "ltr";

  try {
    localStorage.setItem(STORAGE_KEY, locale);
  } catch {
    // Not persisted, still switched for this session
  }
});

const savedLang = (() => {
  try {
    return localStorage.getItem(STORAGE_KEY) || "de";
  } catch {
    return "de";
  }
})();

i18n
  .use(ICU)
  .use(initReactI18next)
  .init({
    resources: {
      de: { translation: de },
      en: { translation: en },
    },
    lng: savedLang,
    supportedLngs: ["de", "en"],
    fallbackLng: "de",
    load: "languageOnly",
    keySeparator: ".",
    nsSeparator: false,
    returnNull: false,
    interpolation: { escapeValue: false },
    react: { useSuspense: false },
  });

export default i18n;
