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
  { pattern: /#midnight-footer/i, message: "Selektor '#midnight-footer' ist geschützt und wird serverseitig gefiltert." },
  { pattern: /\[data-midnight-footer\]/i, message: "Attributselektor '[data-midnight-footer]' ist geschützt." },
  { pattern: /\.midnight-footer/i, message: "Klasse '.midnight-footer' ist geschützt." },
  { pattern: /footer\s*\{[^}]*display\s*:\s*none/i, message: "Verstecken von 'footer' über 'display: none' ist nicht gestattet." },
  { pattern: /footer\s*\{[^}]*visibility\s*:\s*hidden/i, message: "Ausblenden von 'footer' über 'visibility: hidden' ist nicht gestattet." },
  { pattern: /footer\s*\{[^}]*opacity\s*:\s*0/i, message: "Deckkraft von 'footer' auf 0 setzen ist nicht gestattet." },
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
        message: "Globale Ausblendungsregeln für den Footer werden serverseitig verworfen.",
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
                Integritätsschutz des Brandings
              </span>
              <Badge variant="light" color="primary" size="sm">
                Aktiv
              </Badge>
            </div>
            <p className="text-gray-400 leading-relaxed text-xs">
              Footer-Selektoren (z.&nbsp;B. <code className="bg-black/30 px-1.5 py-0.5 rounded text-brand-300 font-mono">#midnight-footer</code>) sowie Regeln, die den Hinweis <strong className="text-gray-200">„Powered by Midnight Panel“</strong> ausblenden oder verdecken, werden vor dem Speichern serverseitig strikt bereinigt.
            </p>
          </div>
        </div>
      </div>

      {/* Issues / Alerts */}
      {issues.length > 0 && (
        <div className="rounded-xl border border-red-500/30 bg-red-500/10 p-3.5 space-y-2">
          <div className="flex items-center gap-2 text-red-400 font-medium text-xs">
            <AlertIcon className="size-4 shrink-0" />
            <span>Nicht erlaubte CSS-Regeln erkannt ({issues.length})</span>
          </div>
          <div className="space-y-1 pl-6">
            {issues.map((issue, idx) => (
              <div key={idx} className="text-xs text-red-300/90 font-mono">
                <span className="font-semibold text-red-200">Zeile {issue.line}:</span> {issue.message}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Editor Frame */}
      <div className="relative overflow-hidden rounded-xl border border-gray-800 bg-[#0B0F19] shadow-theme-sm">
        {/* Editor Header Bar */}
        <div className="flex items-center justify-between border-b border-gray-800 bg-[#111827] px-4 py-2 text-xs">
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
              {lines.length} {lines.length === 1 ? "Zeile" : "Zeilen"}
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
                  <CheckIcon className="size-3.5 text-emerald-400 mr-1" /> Kopiert
                </>
              ) : (
                <>
                  <CopyIcon className="size-3.5 mr-1" /> Kopieren
                </>
              )}
            </Button>
          </div>
        </div>

        {/* Editor Body */}
        <div className="flex text-sm font-mono min-h-[300px]">
          {/* Line Numbers */}
          <div className="select-none bg-[#090D16] py-3 pr-3 pl-4 text-right text-xs text-gray-500 border-r border-gray-800/80 font-mono">
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
              placeholder={`/* Eigene Styles hier einfügen */\n.custom-badge {\n  border-radius: 9999px;\n}`}
              spellCheck={false}
              className="size-full resize-none border-0 bg-transparent p-3 font-mono text-xs leading-6 text-slate-200 outline-none placeholder:text-slate-600 focus:ring-0 min-h-[300px]"
            />
          </div>
        </div>
      </div>
    </div>
  );
}
