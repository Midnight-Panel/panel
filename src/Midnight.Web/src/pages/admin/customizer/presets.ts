export interface ThemePreset {
  id: string;
  name: string;
  description: string;
  primaryColor: string;
  accentColor: string;
  gradient: string;
}

export const THEME_PRESETS: ThemePreset[] = [
  {
    id: "midnight",
    name: "Midnight (Standard)",
    description: "Klassisches tiefes Indigo mit violetten Akzenten",
    primaryColor: "#465FFF",
    accentColor: "#7C3AED",
    gradient: "linear-gradient(135deg, #0F172A 0%, #1E1B4B 50%, #0F172A 100%)",
  },
  {
    id: "ocean",
    name: "Ocean Blue",
    description: "Frisches Azurblau mit Cyan-Highlights",
    primaryColor: "#0284C7",
    accentColor: "#06B6D4",
    gradient: "linear-gradient(135deg, #032B43 0%, #004369 50%, #011627 100%)",
  },
  {
    id: "ember",
    name: "Ember Orange",
    description: "Feuriges Orange mit Karmesinrot",
    primaryColor: "#EA580C",
    accentColor: "#E11D48",
    gradient: "linear-gradient(135deg, #1C0A00 0%, #431407 50%, #0F0F11 100%)",
  },
  {
    id: "forest",
    name: "Emerald Forest",
    description: "Natürliches Smaragdgrün mit Minze",
    primaryColor: "#10B981",
    accentColor: "#14B8A6",
    gradient: "linear-gradient(135deg, #062C1E 0%, #064E3B 50%, #0A1410 100%)",
  },
  {
    id: "neon",
    name: "Cyber Neon",
    description: "Elektrisierendes Magenta und Lila",
    primaryColor: "#EC4899",
    accentColor: "#8B5CF6",
    gradient: "linear-gradient(135deg, #2D0A31 0%, #1E0836 50%, #0F051D 100%)",
  },
  {
    id: "monochrome",
    name: "Monochrome Slate",
    description: "Minimalistisches Schiefergrau im High-End-Look",
    primaryColor: "#94A3B8",
    accentColor: "#64748B",
    gradient: "linear-gradient(135deg, #18181B 0%, #27272A 50%, #09090B 100%)",
  },
];
