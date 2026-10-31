export const editorLanguages = [
  "plaintext",
  "json",
  "yaml",
  "xml",
  "javascript",
  "typescript",
  "properties",
  "toml",
  "shell",
  "python",
  "lua",
] as const;

export type EditorLanguage = (typeof editorLanguages)[number];

export const languageLabels: Record<EditorLanguage, string> = {
  plaintext: "Plain text",
  json: "JSON",
  yaml: "YAML",
  xml: "XML",
  javascript: "JavaScript",
  typescript: "TypeScript",
  properties: "Properties / INI",
  toml: "TOML",
  shell: "Shell",
  python: "Python",
  lua: "Lua",
};

/** Language by file extension (or well known names) */
export function languageForFile(fileName: string): EditorLanguage {
  const name = fileName.toLowerCase();
  const extension = name.includes(".") ? name.slice(name.lastIndexOf(".") + 1) : "";

  if (name === ".env" || name.endsWith(".env")) return "properties";
  if (name === "dockerfile" || name.endsWith(".sh") || name.endsWith(".bash")) return "shell";

  switch (extension) {
    case "json":
    case "json5":
    case "mcmeta":
    case "jsonc":
      return "json";
    case "yml":
    case "yaml":
      return "yaml";
    case "xml":
    case "html":
    case "htm":
    case "svg":
      return "xml";
    case "js":
    case "mjs":
    case "cjs":
    case "jsx":
      return "javascript";
    case "ts":
    case "tsx":
      return "typescript";
    case "properties":
    case "ini":
    case "cfg":
    case "conf":
    case "lang":
      return "properties";
    case "toml":
      return "toml";
    case "py":
      return "python";
    case "lua":
      return "lua";
    default:
      return "plaintext";
  }
}
