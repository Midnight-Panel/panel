import { useState, useEffect } from "react";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { AlertIcon, RegenerateIcon } from "@/icons";

export type ResetScope = "all" | "colors" | "logos" | "css";

interface ResetModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (scope: ResetScope) => Promise<void>;
  initialScope?: ResetScope;
}

export function ResetModal({ open, onOpenChange, onConfirm, initialScope = "all" }: ResetModalProps) {
  const [scope, setScope] = useState<ResetScope>(initialScope);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (open && initialScope) {
      setScope(initialScope);
    }
  }, [open, initialScope]);

  const handleReset = async () => {
    setLoading(true);
    try {
      await onConfirm(scope);
      onOpenChange(false);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <div className="size-11 rounded-full bg-red-500/10 text-red-500 flex items-center justify-center mb-2">
            <AlertIcon className="size-6" />
          </div>
          <DialogTitle className="text-lg font-outfit">Reset settings</DialogTitle>
          <DialogDescription className="text-xs text-gray-500 dark:text-gray-400">
            Choose what to reset. This restores the Midnight Panel defaults.
          </DialogDescription>
        </DialogHeader>

        {/* Scope Selection */}
        <div className="space-y-2.5 py-3">
          <label
            className={`flex items-start gap-3 p-3 rounded-xl border cursor-pointer transition-colors ${
              scope === "all"
                ? "border-brand-500/60 bg-brand-500/5 text-white"
                : "border-gray-200 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-white/[0.02]"
            }`}
          >
            <input
              type="radio"
              name="reset-scope"
              checked={scope === "all"}
              onChange={() => setScope("all")}
              className="mt-0.5 text-brand-500 focus:ring-brand-500"
            />
            <div className="text-xs">
              <span className="font-semibold text-gray-900 dark:text-white block">
                Reset everything
              </span>
              <span className="text-gray-500 dark:text-gray-400 mt-0.5 block">
                Restores colors, gradient, logos, favicon and custom CSS to factory defaults.
              </span>
            </div>
          </label>

          <label
            className={`flex items-start gap-3 p-3 rounded-xl border cursor-pointer transition-colors ${
              scope === "colors"
                ? "border-brand-500/60 bg-brand-500/5 text-white"
                : "border-gray-200 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-white/[0.02]"
            }`}
          >
            <input
              type="radio"
              name="reset-scope"
              checked={scope === "colors"}
              onChange={() => setScope("colors")}
              className="mt-0.5 text-brand-500 focus:ring-brand-500"
            />
            <div className="text-xs">
              <span className="font-semibold text-gray-900 dark:text-white block">
                Colors & background only
              </span>
              <span className="text-gray-500 dark:text-gray-400 mt-0.5 block">
                Resets primary color (#465FFF), accent color (#7C3AED) and the login gradient. Logos are kept.
              </span>
            </div>
          </label>

          <label
            className={`flex items-start gap-3 p-3 rounded-xl border cursor-pointer transition-colors ${
              scope === "logos"
                ? "border-brand-500/60 bg-brand-500/5 text-white"
                : "border-gray-200 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-white/[0.02]"
            }`}
          >
            <input
              type="radio"
              name="reset-scope"
              checked={scope === "logos"}
              onChange={() => setScope("logos")}
              className="mt-0.5 text-brand-500 focus:ring-brand-500"
            />
            <div className="text-xs">
              <span className="font-semibold text-gray-900 dark:text-white block">
                Logos & favicon only
              </span>
              <span className="text-gray-500 dark:text-gray-400 mt-0.5 block">
                Removes uploaded logos and the favicon and restores the default Midnight graphics.
              </span>
            </div>
          </label>

          <label
            className={`flex items-start gap-3 p-3 rounded-xl border cursor-pointer transition-colors ${
              scope === "css"
                ? "border-brand-500/60 bg-brand-500/5 text-white"
                : "border-gray-200 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-white/[0.02]"
            }`}
          >
            <input
              type="radio"
              name="reset-scope"
              checked={scope === "css"}
              onChange={() => setScope("css")}
              className="mt-0.5 text-brand-500 focus:ring-brand-500"
            />
            <div className="text-xs">
              <span className="font-semibold text-gray-900 dark:text-white block">
                Clear custom CSS only
              </span>
              <span className="text-gray-500 dark:text-gray-400 mt-0.5 block">
                Empties the CSS editor completely.
              </span>
            </div>
          </label>
        </div>

        <DialogFooter className="flex items-center justify-end gap-2 pt-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => onOpenChange(false)}
            disabled={loading}
          >
            Cancel
          </Button>
          <Button
            type="button"
            variant="destructive"
            size="sm"
            onClick={handleReset}
            disabled={loading}
          >
            <RegenerateIcon className="size-3.5 mr-1" />
            {loading ? "Resetting..." : "Reset selected"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
