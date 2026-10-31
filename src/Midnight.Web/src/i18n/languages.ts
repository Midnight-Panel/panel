import type { ComponentType, SVGProps } from "react";

import UsFlagIcon from "@/icons/flag-us.svg?react";

import { type Locale, isRtl } from "./routing";

export interface Language {
  id: Locale;
  /** Native display name. */
  name: string;
  /** Short code shown in compact UI (e.g. the dropdown trigger). */
  shortName: string;
  FlagIcon: ComponentType<SVGProps<SVGSVGElement>>;
  badge?: string;
}

export const languages: readonly Language[] = [
  { id: "en", name: "English", shortName: "EN", FlagIcon: UsFlagIcon },
];

const defaultLanguage = languages[0];

export const getLanguage = (locale: Locale): Language =>
  languages.find((l) => l.id === locale) ?? defaultLanguage;

export { isRtl };
