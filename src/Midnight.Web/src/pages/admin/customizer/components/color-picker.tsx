import { SparkIcon } from "@/icons";

export function getLuminance(hex: string): number {
  const match = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
  if (!match) return 0;
  const rgb = [parseInt(match[1], 16), parseInt(match[2], 16), parseInt(match[3], 16)].map((v) => {
    v /= 255;
    return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
  });
  return 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2];
}

export function getContrast(c1: string, c2: string): number {
  const l1 = getLuminance(c1);
  const l2 = getLuminance(c2);
  return (Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05);
}

// Converts hex to HSL, adjusts lightness and returns the new hex value
export function autoAdjustContrast(hex: string, targetContrast = 4.5, onDark = true): string {
  const match = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
  if (!match) return hex;

  let r = parseInt(match[1], 16) / 255;
  let g = parseInt(match[2], 16) / 255;
  let b = parseInt(match[3], 16) / 255;

  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  let h = 0;
  let s = 0;
  let l = (max + min) / 2;

  if (max !== min) {
    const d = max - min;
    s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
    switch (max) {
      case r:
        h = (g - b) / d + (g < b ? 6 : 0);
        break;
      case g:
        h = (b - r) / d + 2;
        break;
      case b:
        h = (r - g) / d + 4;
        break;
    }
    h /= 6;
  }

  const bg = onDark ? "#0F172A" : "#FFFFFF";

  // Adjust step by step
  let currentHex = hex;
  for (let i = 0; i < 40; i++) {
    if (getContrast(currentHex, bg) >= targetContrast) {
      return currentHex;
    }
    l = onDark ? Math.min(0.95, l + 0.02) : Math.max(0.05, l - 0.02);

    // HSL back to hex
    const hue2rgb = (p: number, q: number, t: number) => {
      if (t < 0) t += 1;
      if (t > 1) t -= 1;
      if (t < 1 / 6) return p + (q - p) * 6 * t;
      if (t < 1 / 2) return q;
      if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6;
      return p;
    };

    const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
    const p = 2 * l - q;
    const newR = Math.round(hue2rgb(p, q, h + 1 / 3) * 255);
    const newG = Math.round(hue2rgb(p, q, h) * 255);
    const newB = Math.round(hue2rgb(p, q, h - 1 / 3) * 255);

    currentHex = `#${newR.toString(16).padStart(2, "0")}${newG.toString(16).padStart(2, "0")}${newB.toString(16).padStart(2, "0")}`.toUpperCase();
  }

  return currentHex;
}

const COLOR_PALETTE = [
  "#465FFF", "#3B82F6", "#0284C7", "#06B6D4", "#10B981",
  "#22C55E", "#84CC16", "#EAB308", "#F97316", "#EF4444",
  "#EC4899", "#A855F7", "#7C3AED", "#6366F1", "#64748B"
];

interface ColorPickerProps {
  label: string;
  description?: string;
  value: string;
  onChange: (val: string) => void;
  showContrastBadge?: boolean;
}

export function ColorPicker({
  label,
  description,
  value,
  onChange,
  showContrastBadge = true,
}: ColorPickerProps) {
  const contrastOnDark = getContrast(value, "#0F172A");
  const isAccessible = contrastOnDark >= 4.5;

  const handleAutoAdjust = () => {
    const adjusted = autoAdjustContrast(value, 4.5, true);
    onChange(adjusted);
  };

  return (
    <div className="flex flex-col gap-2.5">
      <div>
        <label className="text-sm font-semibold text-gray-900 dark:text-gray-100">{label}</label>
        {description && <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{description}</p>}
      </div>

      <div className="flex items-center gap-3">
        <div className="relative">
          <input
            type="color"
            value={value || "#465FFF"}
            onChange={(e) => onChange(e.target.value.toUpperCase())}
            className="size-11 cursor-pointer rounded-xl border border-gray-200 dark:border-white/10 bg-transparent p-1 transition hover:scale-105"
            title="Pick color"
          />
        </div>

        <div className="flex-1">
          <input
            type="text"
            value={value}
            maxLength={7}
            onChange={(e) => {
              const val = e.target.value;
              onChange(val.startsWith("#") ? val.toUpperCase() : `#${val.toUpperCase()}`);
            }}
            placeholder="#465FFF"
            className="w-full rounded-xl border border-gray-200 bg-gray-50 px-3.5 py-2.5 font-mono text-sm uppercase text-gray-900 focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-500/20 dark:border-white/10 dark:bg-gray-800/80 dark:text-white"
          />
        </div>
      </div>

      {/* Preset Swatches */}
      <div className="flex flex-wrap items-center gap-1.5 pt-1">
        {COLOR_PALETTE.map((c) => (
          <button
            key={c}
            type="button"
            onClick={() => onChange(c)}
            style={{ backgroundColor: c }}
            className={`size-6 rounded-lg transition-transform hover:scale-115 ${
              value.toUpperCase() === c.toUpperCase()
                ? "ring-2 ring-white ring-offset-2 ring-offset-gray-900 scale-110"
                : "opacity-80 hover:opacity-100"
            }`}
            title={c}
          />
        ))}
      </div>

      {/* WCAG contrast indicator with one-click adjustment */}
      {showContrastBadge && (
        <div className="mt-1 flex flex-wrap items-center justify-between gap-2 rounded-xl border border-gray-200/60 bg-gray-50/70 p-2.5 text-xs dark:border-white/5 dark:bg-gray-800/50">
          <div className="flex items-center gap-2">
            <span
              className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold ${
                isAccessible
                  ? "bg-emerald-500/15 text-emerald-600 dark:text-emerald-400"
                  : "bg-amber-500/15 text-amber-600 dark:text-amber-400"
              }`}
            >
              {isAccessible ? "✓ WCAG AA (Good)" : "⚠ Contrast too low"}
            </span>
            <span className="text-gray-500 dark:text-gray-400 font-mono text-[11px]">
              {contrastOnDark.toFixed(1)}:1 (Dark)
            </span>
          </div>

          {!isAccessible && (
            <button
              type="button"
              onClick={handleAutoAdjust}
              className="inline-flex items-center gap-1 text-[11px] font-medium text-brand-500 hover:text-brand-400 transition"
              title="Automatically adjusts lightness to reach WCAG AA (4.5:1)"
            >
              <SparkIcon className="size-3.5" />
              Auto-adjust
            </button>
          )}
        </div>
      )}
    </div>
  );
};
