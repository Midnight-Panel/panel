import { Button } from "@/components/ui/button";
import { CheckIcon, CopyIcon } from "@/icons";
import { useState } from "react";

interface CodeEditorProps {
  value: string;
  onChange?: (value: string) => void;
  readOnly?: boolean;
  language?: string;
  minHeight?: string;
  placeholder?: string;
}

export function CodeEditor({
  value,
  onChange,
  readOnly = false,
  language = "bash",
  minHeight = "280px",
  placeholder,
}: CodeEditorProps) {
  const [copied, setCopied] = useState(false);

  const lines = (value || "").split("\n");
  const lineNumbers = Array.from({ length: Math.max(lines.length, 1) }, (_, i) => i + 1);

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
    <div className="relative overflow-hidden rounded-xl border border-white/10 bg-[#0B0F19] shadow-2xl">
      {/* Editor Header */}
      <div className="flex items-center justify-between border-b border-white/5 bg-slate-900/60 px-4 py-2 text-xs">
        <div className="flex items-center gap-2">
          <span className="flex gap-1.5">
            <span className="size-2.5 rounded-full bg-red-500/80" />
            <span className="size-2.5 rounded-full bg-amber-500/80" />
            <span className="size-2.5 rounded-full bg-emerald-500/80" />
          </span>
          <span className="ml-2 font-mono text-slate-400 uppercase tracking-wider">{language}</span>
        </div>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="h-7 text-xs text-slate-400 hover:text-white"
          onClick={handleCopy}
        >
          {copied ? <CheckIcon className="size-3.5 text-emerald-400" /> : <CopyIcon className="size-3.5" />}
          {copied ? "Kopiert" : "Kopieren"}
        </Button>
      </div>

      {/* Editor Body with Line Numbers */}
      <div className="flex text-sm font-mono" style={{ minHeight }}>
        {/* Line Numbers */}
        <div className="select-none bg-[#090D16] py-3 pr-3 pl-4 text-right text-xs text-slate-600 border-r border-white/5 font-mono">
          {lineNumbers.map((num) => (
            <div key={num} className="leading-6">
              {num}
            </div>
          ))}
        </div>

        {/* Text Area */}
        <div className="relative flex-1">
          <textarea
            value={value}
            onChange={(e) => onChange?.(e.target.value)}
            readOnly={readOnly}
            placeholder={placeholder}
            spellCheck={false}
            className="size-full resize-none border-0 bg-transparent p-3 font-mono text-sm leading-6 text-slate-200 outline-none placeholder:text-slate-600 focus:ring-0"
            style={{ minHeight }}
          />
        </div>
      </div>
    </div>
  );
}
