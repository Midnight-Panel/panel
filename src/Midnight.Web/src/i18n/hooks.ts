import { useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";

import i18n from "./config";
import { type Locale, isLocale, routing } from "./routing";
import type { Namespace, TranslationValues, Translator } from "./types";

/** Drop-in for next-intl `useTranslations(namespace?)`. */
export function useTranslations(namespace?: Namespace): Translator {
  const { t } = useTranslation(undefined, { keyPrefix: namespace });
  return useMemo<Translator>(
    () => (key: string, values?: TranslationValues) => t(key, values) as string,
    [t], // `t` changes identity on language change -> consumers re-render
  );
}

/** Drop-in for next-intl `useLocale()`. */
export function useLocale(): Locale {
  const { i18n: inst } = useTranslation();
  const lng = inst.resolvedLanguage;
  return isLocale(lng) ? lng : routing.defaultLocale;
}

/** Replaces `router.replace(pathname, { locale })`. */
export function useChangeLocale() {
  const { i18n: inst } = useTranslation();
  return useCallback((locale: Locale) => inst.changeLanguage(locale), [inst]);
}

/** Non-hook translator (chart option builders, utils) ~ next-intl `getTranslations`. */
export function getTranslations(namespace?: Namespace): Translator {
  const t = i18n.getFixedT(null, null, namespace);
  return (key, values) => t(key, values) as string;
}

/**
 * Translator with positional arguments: t("Users.CreatedToast", name) fills "{0}".
 * Also returns a date formatter for the current locale.
 */
export function useT() {
  const { t, i18n: inst } = useTranslation();

  return useMemo(() => {
    const translate = (key: string, ...args: Array<string | number>) =>
      t(key, Object.fromEntries(args.map((arg, index) => [String(index), arg]))) as string;

    const formatDate = (value: string | null | undefined, withTime = true) =>
      value
        ? new Intl.DateTimeFormat(inst.language, withTime ? { dateStyle: "medium", timeStyle: "short" } : { dateStyle: "medium" }).format(new Date(value))
        : "-";

    return { t: translate, formatDate };
  }, [t, inst.language]);
}
