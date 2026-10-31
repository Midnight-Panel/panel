import { ApiError } from "@/api/client";
import { serverFilesApi } from "@/api/server-files";
import { useQueryClient } from "@tanstack/react-query";
import { useRef, useState } from "react";
import { filesQueryKey } from "./utils";

export type UploadStatus = "queued" | "uploading" | "done" | "error" | "cancelled";

export interface UploadItem {
  id: number;
  name: string;
  directory: string;
  size: number;
  loaded: number;
  status: UploadStatus;
  error?: string;
}

const CONCURRENCY = 3;

interface QueuedUpload {
  id: number;
  file: File;
  directory: string;
}

/** Upload queue with per file progress (3 parallel uploads) */
export function useUploads(uuid: string) {
  const queryClient = useQueryClient();
  const [items, setItems] = useState<UploadItem[]>([]);
  const queue = useRef<QueuedUpload[]>([]);
  const controllers = useRef(new Map<number, AbortController>());
  const active = useRef(0);
  const nextId = useRef(1);

  const update = (id: number, patch: Partial<UploadItem>) =>
    setItems((list) => list.map((item) => (item.id === id ? { ...item, ...patch } : item)));

  const run = async ({ id, file, directory }: QueuedUpload) => {
    const controller = new AbortController();
    controllers.current.set(id, controller);
    update(id, { status: "uploading" });

    let lastUpdate = 0;

    try {
      await serverFilesApi.upload(
        uuid,
        directory,
        file,
        (loaded) => {
          // Keep re-renders cheap on fast connections
          const now = performance.now();
          if (now - lastUpdate < 100 && loaded < file.size) return;
          lastUpdate = now;
          update(id, { loaded });
        },
        controller.signal,
      );

      update(id, { status: "done", loaded: file.size });
      void queryClient.invalidateQueries({ queryKey: filesQueryKey(uuid, directory) });
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") update(id, { status: "cancelled" });
      else update(id, { status: "error", error: error instanceof ApiError || error instanceof Error ? error.message : String(error) });
    } finally {
      controllers.current.delete(id);
    }
  };

  const pump = () => {
    while (active.current < CONCURRENCY && queue.current.length > 0) {
      const next = queue.current.shift()!;
      active.current++;
      void run(next).finally(() => {
        active.current--;
        pump();
      });
    }
  };

  const add = (files: File[], directory: string) => {
    if (files.length === 0) return;

    const added = files.map((file) => ({ id: nextId.current++, file, directory }));
    queue.current.push(...added);
    setItems((list) => [
      ...list,
      ...added.map(({ id, file }) => ({ id, name: file.name, directory, size: file.size, loaded: 0, status: "queued" as const })),
    ]);
    pump();
  };

  const cancel = (id: number) => {
    const queued = queue.current.findIndex((item) => item.id === id);

    if (queued >= 0) {
      queue.current.splice(queued, 1);
      update(id, { status: "cancelled" });
    } else controllers.current.get(id)?.abort();
  };

  const cancelAll = () => {
    for (const item of queue.current) update(item.id, { status: "cancelled" });
    queue.current = [];
    for (const controller of controllers.current.values()) controller.abort();
  };

  const clearFinished = () => setItems((list) => list.filter((item) => item.status === "queued" || item.status === "uploading"));

  return { items, add, cancel, cancelAll, clearFinished };
}
