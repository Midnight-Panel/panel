import { FitAddon } from "@xterm/addon-fit";
import { Terminal, type ITheme } from "@xterm/xterm";
import "@xterm/xterm/css/xterm.css";
import { useEffect, useRef } from "react";
import type { ConsoleConnection } from "../console-connection";

/** Near black in both themes, neutral grays without blue tint */
const theme: ITheme = {
  background: "#0e0e10",
  foreground: "#e4e4e7",
  cursor: "#0e0e10",
  cursorAccent: "#0e0e10",
  selectionBackground: "rgba(255, 255, 255, 0.18)",
  black: "#18181b",
  brightBlack: "#71717a",
  red: "#f87171",
  brightRed: "#fca5a5",
  green: "#4ade80",
  brightGreen: "#86efac",
  yellow: "#facc15",
  brightYellow: "#fde047",
  blue: "#60a5fa",
  brightBlue: "#93c5fd",
  magenta: "#e879f9",
  brightMagenta: "#f0abfc",
  cyan: "#22d3ee",
  brightCyan: "#67e8f9",
  white: "#d4d4d8",
  brightWhite: "#fafafa",
};

/** Read only xterm.js output of the console connection (history + live lines) */
export function ConsoleTerminal({ connection }: { connection: ConsoleConnection }) {
  const container = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const element = container.current;
    if (!element) return;

    const terminal = new Terminal({
      theme,
      disableStdin: true,
      convertEol: true,
      cursorBlink: false,
      cursorInactiveStyle: "none",
      fontFamily: '"JetBrains Mono", "Fira Code", Menlo, Consolas, ui-monospace, monospace',
      fontSize: 13,
      lineHeight: 1.3,
      scrollback: 5000,
    });
    const fit = new FitAddon();
    terminal.loadAddon(fit);
    terminal.open(element);

    const write = (lines: string[]) => {
      if (lines.length > 0) terminal.write(lines.join("\n") + "\n");
    };

    const resize = () => {
      try {
        fit.fit();
      } catch {
        // Not visible (yet)
      }
    };

    resize();
    write(connection.getLines());

    const unsubscribe = connection.subscribeTerminal((event) => {
      if (event.type === "reset") terminal.reset();
      write(event.lines);
    });

    const observer = new ResizeObserver(resize);
    observer.observe(element);

    return () => {
      unsubscribe();
      observer.disconnect();
      terminal.dispose();
    };
  }, [connection]);

  return <div ref={container} className="size-full" />;
}
