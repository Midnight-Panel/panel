import type { FileEntry } from "@/api/server-files";
import { ArchiveIcon, BoxIcon, CameraIcon, CodeIcon, FileIcon, FolderIcon, LinkIcon } from "@/icons";
import { cn } from "@/utils";
import { fileKind } from "./utils";

const styles = {
  folder: "bg-amber-500/10 text-amber-500 dark:text-amber-400",
  archive: "bg-violet-500/10 text-violet-500 dark:text-violet-400",
  image: "bg-pink-500/10 text-pink-500 dark:text-pink-400",
  code: "bg-brand-500/10 text-brand-500 dark:text-brand-400",
  binary: "bg-gray-500/10 text-gray-500 dark:text-gray-400",
  text: "bg-gray-500/10 text-gray-600 dark:text-gray-300",
} as const;

const icons = {
  folder: FolderIcon,
  archive: ArchiveIcon,
  image: CameraIcon,
  code: CodeIcon,
  binary: BoxIcon,
  text: FileIcon,
} as const;

export function FileTypeIcon({ entry, className }: { entry: Pick<FileEntry, "name" | "isDirectory" | "mimeType" | "isSymlink">; className?: string }) {
  const kind = fileKind(entry);
  const Icon = icons[kind];

  return (
    <span className={cn("relative flex size-8 shrink-0 items-center justify-center rounded-lg", styles[kind], className)}>
      <Icon className="size-4" />
      {entry.isSymlink && (
        <span className="absolute -inset-e-1 -bottom-1 flex size-3.5 items-center justify-center rounded-full border border-white bg-gray-700 text-white dark:border-gray-900 dark:bg-gray-600">
          <LinkIcon className="size-2" />
        </span>
      )}
    </span>
  );
}
