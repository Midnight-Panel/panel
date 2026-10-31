import { toast } from "sonner";
import { ApiError } from "@/api/client";

export function notifySuccess(message: string) {
  toast.success(message);
}

export function notifyError(error: unknown) {
  const message = error instanceof ApiError || error instanceof Error ? error.message : String(error);
  toast.error(message);
}
