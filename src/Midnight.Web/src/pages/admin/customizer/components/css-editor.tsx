import { useState, useMemo } from "react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { CopyIcon, CheckIcon, AlertIcon, InfoHexagonIcon, CodeIcon } from "@/icons";

interface CssEditorProps {
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
}

interface CssIssue {
  line: number;
  message: string;
  severity: "error" | "warning";
}

const FORBIDDEN_FOOTER_PATTERNS = [
  { pattern: /#mp-immutable-footer/i, message: "Selector '#mp-immutable-footer' is protected and filtered server-side." },
  { pattern: /\[data-mp-enforced\]/i, message: "Attribute selector '[data-mp-enforced]' is protected." },
  { pattern: /\.mp-immutable-footer/i, message: "Class '.mp-immutable-footer' is protected." },
  { pattern: /footer\s*\{[^}]*display\s*:\s*none/i, message: "Hiding 'footer' via 'display: none' is not allowed." },
  { pattern: /footer\s*\{[^}]*visibility\s*:\s*hidden/i, message: "Hiding 'footer' via 'visibility: hidden' is not allowed." },
  { pattern: /footer\s*\{[^}]*opacity\s*:\s*0/i, message: "Setting 'footer' opacity to 0 is not allowed." },
];

export function CssEditor({ value, onChange, disabled }: CssEditorProps) {
  const [copied, setCopied] = useState(false);

  const lines = useMemo(() => (value || "").split("\n"), [value]);
  const lineNumbers = useMemo(
    () => Array.from({ length: Math.max(lines.length, 1) }, (_, i) => i + 1),
    [lines.length],
  );

  const issues = useMemo<CssIssue[]>(() => {
    const list: CssIssue[] = [];
    if (!value) return list;

    lines.forEach((lineText, idx) => {
      for (const rule of FORBIDDEN_FOOTER_PATTERNS) {
        if (rule.pattern.test(lineText)) {
          list.push({
            line: idx + 1,
            message: rule.message,
            severity: "error",
          });
        }
      }
    });

    // Check across multi-line blocks for footer suppression
    if (/footer\b[^}]*display\s*:\s*none/i.test(value) && !list.some((i) => i.message.includes("display: none"))) {
      list.push({
        line: 1,
        message: "Global rules that hide the footer are discarded server-side.",
        severity: "error",
      });
    }

    return list;
  }, [value, lines]);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Fallback
    }
  };

  return (
    <div className="space-y-3">
      {/* Prominent Footer Protection Warning Notice */}
      <div className="rounded-xl border border-brand-500/20 bg-brand-500/5 p-4 text-xs text-gray-300">
        <div className="flex items-start gap-3">
          <div className="p-1 rounded bg-brand-500/10 text-brand-400 mt-0.5">
            <InfoHexagonIcon className="size-4" />
          </div>
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <span className="font-medium text-white font-outfit text-sm">
                Branding integrity protection
              </span>
              <Badge variant="light" color="primary" size="sm">
                Active
              </Badge>
            </div>
            <p className="text-gray-400 leading-relaxed text-xs">
              Footer selectors (e.g. <code className="bg-black/30 px-1.5 py-0.5 rounded text-brand-300 font-mono">#mp-immutable-footer</code>) and rules that hide or cover the <strong className="text-gray-200">“Powered by Midnight Panel”</strong> notice are strictly stripped server-side before saving.
            </p>
          </div>
        </div>
      </div>

      {/* Issues / Alerts */}
      {issues.length > 0 && (
        <div className="rounded-xl border border-red-500/30 bg-red-500/10 p-3.5 space-y-2">
          <div className="flex items-center gap-2 text-red-400 font-medium text-xs">
            <AlertIcon className="size-4 shrink-0" />
            <span>Disallowed CSS rules detected ({issues.length})</span>
          </div>
          <div className="space-y-1 pl-6">
            {issues.map((issue, idx) => (
              <div key={idx} className="text-xs text-red-300/90 font-mono">
                <span className="font-semibold text-red-200">Line {issue.line}:</span> {issue.message}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Editor Frame */}
      <div className="relative overflow-hidden rounded-xl border border-gray-800 bg-[#0e0e10] shadow-theme-sm">
        {/* Editor Header Bar */}
        <div className="flex items-center justify-between border-b border-gray-800 bg-[#161619] px-4 py-2 text-xs">
          <div className="flex items-center gap-2">
            <span className="flex gap-1.5">
              <span className="size-2.5 rounded-full bg-red-500/80" />
              <span className="size-2.5 rounded-full bg-amber-500/80" />
              <span className="size-2.5 rounded-full bg-emerald-500/80" />
            </span>
            <div className="flex items-center gap-1.5 ml-2 text-gray-400">
              <CodeIcon className="size-3.5" />
              <span className="font-mono text-gray-300 uppercase tracking-wider text-[11px]">
                custom.css
              </span>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-[11px] text-gray-500 font-mono">
              {lines.length} {lines.length === 1 ? "line" : "lines"}
            </span>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-7 text-xs text-gray-400 hover:text-white"
              onClick={handleCopy}
            >
              {copied ? (
                <>
                  <CheckIcon className="size-3.5 text-emerald-400 mr-1" /> Copied
                </>
              ) : (
                <>
                  <CopyIcon className="size-3.5 mr-1" /> Copy
                </>
              )}
            </Button>
          </div>
        </div>

        {/* Editor Body */}
        <div className="flex text-sm font-mono min-h-[300px]">
          {/* Line Numbers */}
          <div className="select-none bg-[#08080a] py-3 pr-3 pl-4 text-right text-xs text-gray-500 border-r border-gray-800/80 font-mono">
            {lineNumbers.map((num) => {
              const hasError = issues.some((i) => i.line === num);
              return (
                <div
                  key={num}
                  className={`leading-6 transition-colors ${
                    hasError ? "text-red-400 font-bold bg-red-500/10 px-1 rounded" : ""
                  }`}
                >
                  {num}
                </div>
              );
            })}
          </div>

          {/* Text Area */}
          <div className="relative flex-1">
            <textarea
              value={value}
              onChange={(e) => onChange(e.target.value)}
              disabled={disabled}
              placeholder={`/* Add your own styles here */\n.custom-badge {\n  border-radius: 9999px;\n}`}
              spellCheck={false}
              className="size-full resize-none border-0 bg-transparent p-3 font-mono text-xs leading-6 text-gray-200 outline-none placeholder:text-gray-600 focus:ring-0 min-h-[300px]"
            />
          </div>
        </div>
      </div>
    </div>
  );
}
