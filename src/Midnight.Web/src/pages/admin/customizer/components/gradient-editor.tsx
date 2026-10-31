import { useState, useEffect } from "react";
import { ChevronDownIcon, ChevronUpIcon } from "@/icons";

interface GradientEditorProps {
  value: string;
  onChange: (val: string) => void;
}

export function GradientEditor({ value, onChange }: GradientEditorProps) {
  const [angle, setAngle] = useState(135);
  const [color1, setColor1] = useState("#0e0e10");
  const [color2, setColor2] = useState("#1c1c22");
  const [color3, setColor3] = useState("#0e0e10");
  const [useThreeColors, setUseThreeColors] = useState(true);
  const [showRaw, setShowRaw] = useState(false);

  // Parse the gradient string into the controls
  useEffect(() => {
    if (!value) return;
    const match = value.match(/linear-gradient\s*\(\s*(\d+)deg\s*,\s*(#[a-fA-F0-9]{6}|rgba?\([^)]+\))\s*\d*%\s*,\s*(#[a-fA-F0-9]{6}|rgba?\([^)]+\))\s*\d*%(?:\s*,\s*(#[a-fA-F0-9]{6}|rgba?\([^)]+\))\s*\d*%)?\s*\)/i);
    if (match) {
      if (match[1]) setAngle(parseInt(match[1], 10));
      if (match[2] && match[2].startsWith("#")) setColor1(match[2].toUpperCase());
      if (match[3] && match[3].startsWith("#")) setColor2(match[3].toUpperCase());
      if (match[4] && match[4].startsWith("#")) {
        setColor3(match[4].toUpperCase());
        setUseThreeColors(true);
      } else {
        setUseThreeColors(false);
      }
    }
    // Follows outside changes too (presets, reset), otherwise the next slider move would restore old colors
  }, [value]);

  const updateGradient = (newAngle: number, c1: string, c2: string, c3: string, three: boolean) => {
    const css = three
      ? `linear-gradient(${newAngle}deg, ${c1} 0%, ${c2} 50%, ${c3} 100%)`
      : `linear-gradient(${newAngle}deg, ${c1} 0%, ${c2} 100%)`;
    onChange(css);
  };

  const handleAngleChange = (newAngle: number) => {
    setAngle(newAngle);
    updateGradient(newAngle, color1, color2, color3, useThreeColors);
  };

  const handleColor1Change = (c: string) => {
    setColor1(c);
    updateGradient(angle, c, color2, color3, useThreeColors);
  };

  const handleColor2Change = (c: string) => {
    setColor2(c);
    updateGradient(angle, color1, c, color3, useThreeColors);
  };

  const handleColor3Change = (c: string) => {
    setColor3(c);
    updateGradient(angle, color1, color2, c, useThreeColors);
  };

  const handleToggleThree = () => {
    const next = !useThreeColors;
    setUseThreeColors(next);
    updateGradient(angle, color1, color2, color3, next);
  };

  return (
    <div className="flex flex-col gap-4 rounded-xl border border-gray-200/80 bg-gray-50/50 p-4 dark:border-white/10 dark:bg-gray-800/40">
      {/* Live Preview Bar */}
      <div>
        <span className="text-xs font-medium text-gray-500 dark:text-gray-400 block mb-1.5">
          Gradient preview
        </span>
        <div
          style={{ background: value || "linear-gradient(135deg, #0e0e10 0%, #1c1c22 100%)" }}
          className="h-10 w-full rounded-xl border border-white/10 shadow-inner transition-all duration-300"
        />
      </div>

      {/* Color stops */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        <div>
          <label className="text-xs text-gray-600 dark:text-gray-300 font-medium block mb-1">
            Start (0%)
          </label>
          <div className="flex items-center gap-2">
            <input
              type="color"
              value={color1}
              onChange={(e) => handleColor1Change(e.target.value.toUpperCase())}
              className="size-8 cursor-pointer rounded-lg border border-gray-300 dark:border-white/10 bg-transparent p-0.5"
            />
            <span className="font-mono text-xs text-gray-700 dark:text-gray-300">{color1}</span>
          </div>
        </div>

        <div>
          <label className="text-xs text-gray-600 dark:text-gray-300 font-medium block mb-1">
            {useThreeColors ? "Middle (50%)" : "End (100%)"}
          </label>
          <div className="flex items-center gap-2">
            <input
              type="color"
              value={color2}
              onChange={(e) => handleColor2Change(e.target.value.toUpperCase())}
              className="size-8 cursor-pointer rounded-lg border border-gray-300 dark:border-white/10 bg-transparent p-0.5"
            />
            <span className="font-mono text-xs text-gray-700 dark:text-gray-300">{color2}</span>
          </div>
        </div>

        {useThreeColors && (
          <div>
            <label className="text-xs text-gray-600 dark:text-gray-300 font-medium block mb-1">
              End (100%)
            </label>
            <div className="flex items-center gap-2">
              <input
                type="color"
                value={color3}
                onChange={(e) => handleColor3Change(e.target.value.toUpperCase())}
                className="size-8 cursor-pointer rounded-lg border border-gray-300 dark:border-white/10 bg-transparent p-0.5"
              />
              <span className="font-mono text-xs text-gray-700 dark:text-gray-300">{color3}</span>
            </div>
          </div>
        )}
      </div>

      <div className="flex items-center justify-between pt-1">
        <button
          type="button"
          onClick={handleToggleThree}
          className="text-xs text-brand-500 hover:text-brand-400 font-medium transition"
        >
          {useThreeColors ? "Reduce to 2 color stops" : "+ Add 3rd color stop"}
        </button>
      </div>

      {/* Angle slider */}
      <div className="flex flex-col gap-1.5 pt-1">
        <div className="flex items-center justify-between text-xs">
          <span className="text-gray-600 dark:text-gray-300 font-medium">Angle</span>
          <span className="font-mono font-semibold text-gray-700 dark:text-gray-300">{angle}°</span>
        </div>
        <input
          type="range"
          min={0}
          max={360}
          value={angle}
          onChange={(e) => handleAngleChange(parseInt(e.target.value, 10))}
          className="h-2 w-full cursor-pointer accent-brand-500 rounded-lg bg-gray-200 dark:bg-gray-700"
        />
      </div>

      {/* Collapsible CSS code */}
      <div className="border-t border-gray-200 dark:border-white/10 pt-2.5">
        <button
          type="button"
          onClick={() => setShowRaw(!showRaw)}
          className="flex items-center justify-between w-full text-xs text-gray-500 dark:text-gray-400 hover:text-gray-300"
        >
          <span>Advanced: view CSS rule</span>
          {showRaw ? <ChevronUpIcon className="size-3.5" /> : <ChevronDownIcon className="size-3.5" />}
        </button>
        {showRaw && (
          <input
            type="text"
            value={value}
            onChange={(e) => onChange(e.target.value)}
            className="mt-2 w-full rounded-lg border border-gray-200 bg-gray-100 p-2 text-xs font-mono text-gray-800 dark:border-white/10 dark:bg-gray-900 dark:text-gray-200"
          />
        )}
      </div>
    </div>
  );
};
