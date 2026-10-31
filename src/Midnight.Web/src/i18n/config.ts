import i18n from "i18next";
import ICU from "i18next-icu";
import { initReactI18next } from "react-i18next";

import en from "../messages/en.json";
import { routing } from "./routing";

const STORAGE_KEY = "midnight.language";

i18n.on("languageChanged", (lng) => {
  const locale = lng === "en" ? lng : routing.defaultLocale;
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
    resources: { en: { translation: en } },
    lng: "en",
    supportedLngs: ["en"],
    fallbackLng: "en",
    load: "languageOnly",
    keySeparator: ".",
    nsSeparator: false,
    returnNull: false,
    interpolation: { escapeValue: false },
    react: { useSuspense: false },
  });

export default i18n;
