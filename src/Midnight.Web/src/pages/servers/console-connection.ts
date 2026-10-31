import { ApiError } from "@/api/client";
import { serversApi, websocketUrl } from "@/api/servers";
import type { ConsoleClientMessage, ConsoleServerMessage, ServerState, ServerStats } from "@/api/types";

export type ConnectionStatus = "connecting" | "connected" | "reconnecting" | "closed";

export interface StatsSample {
  time: number;
  stats: ServerStats;
}

export interface ConsoleSnapshot {
  connection: ConnectionStatus;
  /** Live state from status events, undefined until the first one */
  state?: ServerState;
  stats?: ServerStats;
  /** The last stats events, oldest first */
  samples: StatsSample[];
  /** install-started was received and install-completed not yet */
  installing: boolean;
  /** Increased on every reconnect attempt */
  attempt: number;
}

export interface ConsoleHandlers {
  /** Events that should reach the user outside of the terminal (errors) */
  onError?: (message: string) => void;
  /** The install finished, the server detail should be reloaded */
  onInstallCompleted?: () => void;
}

/** Output of the terminal: either new lines or a full replacement (history after (re)connecting) */
export type TerminalEvent = { type: "lines"; lines: string[] } | { type: "reset"; lines: string[] };

const MAX_LINES = 1000;
const MAX_SAMPLES = 40;
const ESC = "\u001b[";

const ansi = {
  install: (line: string) => `${ESC}38;5;244m[installer]${ESC}0m ${line}`,
  notice: (line: string) => `${ESC}1;33m[Midnight]${ESC}0m ${ESC}33m${line}${ESC}0m`,
  error: (line: string) => `${ESC}1;31m[Midnight]${ESC}0m ${ESC}31m${line}${ESC}0m`,
  system: (line: string) => `${ESC}1;36m[Midnight]${ESC}0m ${line}`,
};

function isServerMessage(value: unknown): value is ConsoleServerMessage {
  return typeof value === "object" && value !== null && typeof (value as { event?: unknown }).event === "string";
}

/**
 * Websocket connection to /api/servers/{uuid}/console. Requests a fresh single use ticket for every
 * (re)connect and reconnects with exponential backoff until stop() is called.
 */
export class ConsoleConnection {
  private socket: WebSocket | null = null;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private stopped = false;
  private failures = 0;
  /** Changes on start / stop / retry so pending connects of an older run are dropped */
  private generation = 0;
  private lines: string[] = [];
  private snapshot: ConsoleSnapshot = { connection: "connecting", samples: [], installing: false, attempt: 0 };
  private readonly snapshotListeners = new Set<(snapshot: ConsoleSnapshot) => void>();
  private readonly terminalListeners = new Set<(event: TerminalEvent) => void>();
  private readonly messageListeners = new Set<(message: ConsoleServerMessage) => void>();
  private readonly uuid: string;

  private handlers: ConsoleHandlers = {};

  constructor(uuid: string) {
    this.uuid = uuid;
  }

  setHandlers(handlers: ConsoleHandlers) {
    this.handlers = handlers;
  }

  start() {
    this.stopped = false;
    this.generation++;
    void this.connect();
  }

  stop() {
    this.stopped = true;
    this.generation++;
    if (this.timer) clearTimeout(this.timer);
    this.timer = null;

    const socket = this.socket;
    this.socket = null;
    socket?.close(1000);
    this.update({ connection: "closed" });
  }

  /** Reconnect right away (e.g. after the user pressed "Retry") */
  retry() {
    if (this.timer) clearTimeout(this.timer);
    this.failures = 0;
    this.stopped = false;
    this.generation++;
    this.socket?.close(1000);
    this.socket = null;
    void this.connect();
  }

  get isOpen() {
    return this.socket?.readyState === WebSocket.OPEN;
  }

  send(message: ConsoleClientMessage) {
    if (!this.isOpen) return false;
    this.socket!.send(JSON.stringify(message));
    return true;
  }

  getSnapshot() {
    return this.snapshot;
  }

  getLines() {
    return this.lines;
  }

  subscribe(listener: (snapshot: ConsoleSnapshot) => void) {
    this.snapshotListeners.add(listener);
    return () => {
      this.snapshotListeners.delete(listener);
    };
  }

  subscribeTerminal(listener: (event: TerminalEvent) => void) {
    this.terminalListeners.add(listener);
    return () => {
      this.terminalListeners.delete(listener);
    };
  }

  /** Every message of the websocket, e.g. for tabs that refresh on backup-completed */
  subscribeMessages(listener: (message: ConsoleServerMessage) => void) {
    this.messageListeners.add(listener);
    return () => {
      this.messageListeners.delete(listener);
    };
  }

  private update(patch: Partial<ConsoleSnapshot>) {
    this.snapshot = { ...this.snapshot, ...patch };
    for (const listener of this.snapshotListeners) listener(this.snapshot);
  }

  private emit(event: TerminalEvent) {
    for (const listener of this.terminalListeners) listener(event);
  }

  private push(...lines: string[]) {
    this.lines = [...this.lines, ...lines].slice(-MAX_LINES);
    this.emit({ type: "lines", lines });
  }

  private async connect() {
    if (this.stopped) return;

    const generation = this.generation;

    this.update({ connection: this.failures === 0 && this.snapshot.attempt === 0 ? "connecting" : "reconnecting", attempt: this.snapshot.attempt + 1 });

    let path: string;

    try {
      path = (await serversApi.consoleTicket(this.uuid)).path;
    } catch (error) {
      // No access (anymore): reconnecting won't help
      if (generation !== this.generation) return;

      if (error instanceof ApiError && (error.status === 401 || error.status === 403 || error.status === 404)) {
        this.update({ connection: "closed" });
        this.handlers.onError?.(error.message);
        return;
      }

      this.scheduleReconnect();
      return;
    }

    if (this.stopped || generation !== this.generation) return;

    const socket = new WebSocket(websocketUrl(path));
    this.socket = socket;

    socket.onopen = () => {
      if (this.socket !== socket) return;
      this.failures = 0;
      this.update({ connection: "connected" });
    };

    socket.onmessage = (message) => {
      if (this.socket !== socket || typeof message.data !== "string") return;

      try {
        const parsed: unknown = JSON.parse(message.data);
        if (isServerMessage(parsed)) this.handle(parsed);
      } catch {
        // Ignore malformed messages
      }
    };

    socket.onclose = () => {
      if (this.socket !== socket) return;
      this.socket = null;
      this.scheduleReconnect();
    };
  }

  private scheduleReconnect() {
    if (this.stopped) return;

    this.failures++;
    const delay = Math.min(30_000, 1_000 * 2 ** Math.min(this.failures - 1, 5)) + Math.random() * 500;

    this.update({ connection: "reconnecting" });
    if (this.timer) clearTimeout(this.timer);
    this.timer = setTimeout(() => {
      this.timer = null;
      void this.connect();
    }, delay);
  }

  private handle(message: ConsoleServerMessage) {
    for (const listener of this.messageListeners) listener(message);

    switch (message.event) {
      case "history":
        this.lines = (message.data ?? []).slice(-MAX_LINES);
        this.emit({ type: "reset", lines: this.lines });
        break;

      case "console":
        this.push(message.data);
        break;

      case "install-output":
        this.push(ansi.install(message.data));
        break;

      case "install-started":
        this.update({ installing: true });
        this.push(ansi.system("Installation started"));
        break;

      case "install-completed":
        this.update({ installing: false });
        // Older demons send null, only an explicit false means the script failed
        this.push(message.data === false ? ansi.error("Installation failed") : ansi.system("Installation completed"));
        this.handlers.onInstallCompleted?.();
        break;

      case "status":
        this.update({ state: message.data, installing: message.data === "Installing" });
        break;

      case "stats":
        this.update({
          stats: message.data,
          samples: [...this.snapshot.samples, { time: Date.now(), stats: message.data }].slice(-MAX_SAMPLES),
        });
        break;

      case "notice":
        this.push(ansi.notice(message.data));
        break;

      case "error":
        this.push(ansi.error(message.data));
        this.handlers.onError?.(message.data);
        break;

      case "backup-restore-started":
        this.push(ansi.system("Restoring a backup"));
        break;

      case "backup-restore-completed":
        this.push(message.data?.successful === false ? ansi.error("Backup restore failed") : ansi.system("Backup restored"));
        break;
    }
  }
}
