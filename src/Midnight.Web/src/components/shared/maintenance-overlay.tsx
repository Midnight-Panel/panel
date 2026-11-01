import { useBranding } from "@/contexts/BrandingContext";
import { isPanelHealthy, onPanelUnavailable } from "@/lib/maintenance";
import { useEffect, useRef, useState } from "react";

const CHECK_INTERVAL_SECONDS = 5;

/**
 * Full screen notice for pages that are already open while the panel is updated or restarted.
 * Shown once a request failed and the health check confirms the panel is gone; reloads when it is back.
 */
export function MaintenanceOverlay() {
  const { branding } = useBranding();
  const [visible, setVisible] = useState(false);
  const [back, setBack] = useState(false);
  const [remaining, setRemaining] = useState(CHECK_INTERVAL_SECONDS);
  const checking = useRef(false);

  // A failed request alone may be a hiccup, the health endpoint decides
  useEffect(
    () =>
      onPanelUnavailable(async () => {
        if (checking.current) return;

        checking.current = true;
        if (!(await isPanelHealthy())) setVisible(true);
        checking.current = false;
      }),
    [],
  );

  useEffect(() => {
    if (!visible || back) return;

    const timer = window.setInterval(() => {
      setRemaining((current) => {
        if (current > 1) return current - 1;

        void isPanelHealthy().then((healthy) => {
          if (!healthy) return;

          setBack(true);
          window.setTimeout(() => window.location.reload(), 600);
        });

        return CHECK_INTERVAL_SECONDS;
      });
    }, 1000);

    return () => window.clearInterval(timer);
  }, [visible, back]);

  if (!visible) return null;

  const panelName = branding?.panelName || "Midnight Panel";

  return (
    <div
      role="alertdialog"
      aria-modal="true"
      aria-labelledby="maintenance-title"
      className="fixed inset-0 z-[100000] flex items-center justify-center bg-gray-50/80 p-4 backdrop-blur-sm dark:bg-gray-950/80"
    >
      <div className="w-full max-w-md rounded-2xl border border-gray-200 bg-white px-8 pt-10 pb-8 text-center shadow-theme-xl dark:border-gray-800 dark:bg-gray-900">
        <div className="relative mx-auto mb-6 grid size-16 place-items-center rounded-2xl bg-brand-500/10 text-brand-500 dark:text-brand-400">
          <span className="absolute -inset-1.5 animate-ping rounded-[22px] border-2 border-brand-500/40 [animation-duration:2.4s] motion-reduce:hidden" />
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth={2}
            strokeLinecap="round"
            strokeLinejoin="round"
            className="size-7 animate-[spin_6s_linear_infinite] motion-reduce:animate-none"
            aria-hidden="true"
          >
            <path d="M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z" />
            <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9c.26.6.85 1 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1Z" />
          </svg>
        </div>

        <h2 id="maintenance-title" className="font-outfit text-2xl font-bold text-gray-900 dark:text-white">
          Maintenance in progress
        </h2>
        <p className="mt-2.5 text-[15px] leading-relaxed text-gray-500 dark:text-gray-400">
          {panelName} is being updated right now. It will be back in a few minutes – your servers keep running in the
          meantime.
        </p>

        <div
          role="status"
          aria-live="polite"
          className="mt-7 inline-flex items-center gap-2.5 rounded-full border border-gray-200 px-3.5 py-2 text-[13px] text-gray-500 dark:border-gray-800 dark:text-gray-400"
        >
          <span
            className={
              back ? "size-2 rounded-full bg-emerald-500" : "size-2 animate-pulse rounded-full bg-amber-500"
            }
          />
          {back ? "Back online – reloading…" : `Checking again in ${remaining}s`}
        </div>

        <p className="mt-6 text-xs text-gray-400 dark:text-gray-500">
          This page reloads by itself as soon as the panel is back.
        </p>
      </div>
    </div>
  );
}
