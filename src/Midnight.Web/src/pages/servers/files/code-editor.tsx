import { javascript } from "@codemirror/lang-javascript";
import { json } from "@codemirror/lang-json";
import { xml } from "@codemirror/lang-xml";
import { yaml } from "@codemirror/lang-yaml";
import { HighlightStyle, StreamLanguage, syntaxHighlighting, type LanguageSupport } from "@codemirror/language";
import { lua } from "@codemirror/legacy-modes/mode/lua";
import { properties } from "@codemirror/legacy-modes/mode/properties";
import { python } from "@codemirror/legacy-modes/mode/python";
import { shell } from "@codemirror/legacy-modes/mode/shell";
import { toml } from "@codemirror/legacy-modes/mode/toml";
import type { Extension } from "@codemirror/state";
import { EditorView, keymap } from "@codemirror/view";
import { tags } from "@lezer/highlight";
import CodeMirror from "@uiw/react-codemirror";
import { useMemo } from "react";
import { languageForFile, type EditorLanguage } from "./languages";

function languageExtension(language: EditorLanguage): Extension | LanguageSupport | null {
  switch (language) {
    case "json":
      return json();
    case "yaml":
      return yaml();
    case "xml":
      return xml();
    case "javascript":
      return javascript();
    case "typescript":
      return javascript({ typescript: true });
    case "properties":
      return StreamLanguage.define(properties);
    case "toml":
      return StreamLanguage.define(toml);
    case "shell":
      return StreamLanguage.define(shell);
    case "python":
      return StreamLanguage.define(python);
    case "lua":
      return StreamLanguage.define(lua);
    default:
      return null;
  }
}

// Near-black neutral palette (matches the console: #0e0e10 / gray-800 #232327)
const darkTheme = EditorView.theme(
  {
    "&": { color: "#e4e4e7", backgroundColor: "#0e0e10", height: "100%" },
    ".cm-content": { caretColor: "#fafafa", fontFamily: "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace" },
    ".cm-cursor, .cm-dropCursor": { borderLeftColor: "#fafafa" },
    "&.cm-focused .cm-selectionBackground, .cm-selectionBackground, .cm-content ::selection": { backgroundColor: "#36363c !important" },
    ".cm-gutters": { backgroundColor: "#0e0e10", color: "#55555c", border: "none", borderRight: "1px solid #1a1a1d" },
    ".cm-activeLine": { backgroundColor: "#ffffff08" },
    ".cm-activeLineGutter": { backgroundColor: "#ffffff08", color: "#a1a1aa" },
    ".cm-panels": { backgroundColor: "#161619", color: "#e4e4e7" },
    ".cm-panels.cm-panels-top": { borderBottom: "1px solid #232327" },
    ".cm-panels.cm-panels-bottom": { borderTop: "1px solid #232327" },
    ".cm-searchMatch": { backgroundColor: "#f59e0b33", outline: "1px solid #f59e0b66" },
    ".cm-searchMatch.cm-searchMatch-selected": { backgroundColor: "#f59e0b55" },
    ".cm-selectionMatch": { backgroundColor: "#ffffff14" },
    ".cm-matchingBracket, .cm-nonmatchingBracket": { backgroundColor: "#ffffff1a", outline: "none" },
    ".cm-foldPlaceholder": { backgroundColor: "#232327", border: "none", color: "#a1a1aa" },
    ".cm-tooltip": { border: "1px solid #232327", backgroundColor: "#161619", color: "#e4e4e7" },
    ".cm-tooltip-autocomplete > ul > li[aria-selected]": { backgroundColor: "#36363c", color: "#fafafa" },
    ".cm-textfield": { backgroundColor: "#0e0e10", border: "1px solid #36363c", color: "#e4e4e7" },
    ".cm-button": { backgroundImage: "none", backgroundColor: "#232327", border: "1px solid #36363c", color: "#e4e4e7" },
  },
  { dark: true },
);

const lightTheme = EditorView.theme({
  "&": { color: "#232327", backgroundColor: "#ffffff", height: "100%" },
  ".cm-content": { caretColor: "#0e0e10", fontFamily: "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace" },
  ".cm-gutters": { backgroundColor: "#fafafa", color: "#a1a1aa", border: "none", borderRight: "1px solid #e4e4e7" },
  ".cm-activeLine": { backgroundColor: "#f4f4f580" },
  ".cm-activeLineGutter": { backgroundColor: "#f4f4f5", color: "#55555c" },
  "&.cm-focused .cm-selectionBackground, .cm-selectionBackground, .cm-content ::selection": { backgroundColor: "#d4d4d8 !important" },
  ".cm-panels": { backgroundColor: "#fafafa", color: "#232327" },
});

const darkHighlight = HighlightStyle.define([
  { tag: [tags.keyword, tags.operatorKeyword, tags.modifier], color: "#c792ea" },
  { tag: [tags.string, tags.special(tags.string)], color: "#a5d6a7" },
  { tag: [tags.number, tags.bool, tags.null, tags.atom], color: "#f5a97f" },
  { tag: [tags.propertyName, tags.attributeName, tags.definition(tags.propertyName)], color: "#7dcfff" },
  { tag: [tags.tagName, tags.typeName, tags.className], color: "#f38ba8" },
  { tag: [tags.function(tags.variableName), tags.function(tags.propertyName)], color: "#89b4fa" },
  { tag: [tags.comment, tags.lineComment, tags.blockComment], color: "#6b6b74", fontStyle: "italic" },
  { tag: [tags.meta, tags.processingInstruction, tags.heading], color: "#e0af68" },
  { tag: [tags.operator, tags.punctuation, tags.bracket, tags.separator], color: "#a1a1aa" },
  { tag: tags.invalid, color: "#f87171" },
]);

const lightHighlight = HighlightStyle.define([
  { tag: [tags.keyword, tags.operatorKeyword, tags.modifier], color: "#7c3aed" },
  { tag: [tags.string, tags.special(tags.string)], color: "#15803d" },
  { tag: [tags.number, tags.bool, tags.null, tags.atom], color: "#c2410c" },
  { tag: [tags.propertyName, tags.attributeName, tags.definition(tags.propertyName)], color: "#0369a1" },
  { tag: [tags.tagName, tags.typeName, tags.className], color: "#be123c" },
  { tag: [tags.function(tags.variableName), tags.function(tags.propertyName)], color: "#1d4ed8" },
  { tag: [tags.comment, tags.lineComment, tags.blockComment], color: "#85858e", fontStyle: "italic" },
  { tag: [tags.meta, tags.processingInstruction, tags.heading], color: "#a16207" },
  { tag: tags.invalid, color: "#dc2626" },
]);

export interface CodeEditorProps {
  value: string;
  onChange: (value: string) => void;
  fileName: string;
  /** Overrides the language picked by the extension */
  language?: EditorLanguage;
  dark: boolean;
  readOnly?: boolean;
  onSave?: () => void;
}

/** CodeMirror 6 with the panel themes; Mod-s triggers onSave */
export default function CodeEditor({ value, onChange, fileName, language, dark, readOnly = false, onSave }: CodeEditorProps) {
  const resolved = language ?? languageForFile(fileName);

  const extensions = useMemo(() => {
    const list: Extension[] = [
      EditorView.lineWrapping,
      dark ? darkTheme : lightTheme,
      syntaxHighlighting(dark ? darkHighlight : lightHighlight),
      keymap.of([
        {
          key: "Mod-s",
          preventDefault: true,
          run: () => {
            onSave?.();
            return true;
          },
        },
      ]),
    ];

    const lang = languageExtension(resolved);
    if (lang) list.push(lang);

    return list;
  }, [dark, resolved, onSave]);

  return (
    <CodeMirror
      value={value}
      onChange={onChange}
      theme="none"
      height="100%"
      className="h-full text-[13px]"
      readOnly={readOnly}
      editable={!readOnly}
      extensions={extensions}
      basicSetup={{ highlightActiveLine: !readOnly, highlightActiveLineGutter: !readOnly, foldGutter: true, tabSize: 2 }}
    />
  );
}
