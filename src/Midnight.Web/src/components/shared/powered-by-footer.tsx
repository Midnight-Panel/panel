import { useEffect, useRef } from "react";
import { useBranding } from "@/contexts/BrandingContext";
import { cn } from "@/utils";

interface PoweredByFooterProps {
  className?: string;
}

/**
 * Unveränderbarer, einheitlicher "Powered by Midnight Panel" Footer-Hinweis.
 * Durch Inline-Styles und einen MutationObserver gegen clientseitiges Entfernen geschützt.
 */
export function PoweredByFooter({ className }: PoweredByFooterProps) {
  const { branding } = useBranding();
  const containerRef = useRef<HTMLDivElement>(null);

  const poweredBy = branding?.poweredBy || {
    text: "Powered by Midnight Panel",
    url: "https://midnightpanel.com",
    enforced: true,
  };

  useEffect(() => {
    const el = containerRef.current;
    if (!el || !el.parentElement) return;

    const observer = new MutationObserver(() => {
      if (!document.body.contains(el)) {
        console.warn("[Midnight Panel Security] Unerlaubter Entfernungsversuch des Pflicht-Footers detektiert.");
        window.location.reload();
      }

      const style = window.getComputedStyle(el);
      if (
        style.display === "none" ||
        style.visibility === "hidden" ||
        parseFloat(style.opacity) < 0.1 ||
        el.offsetWidth === 0 ||
        el.offsetHeight === 0
      ) {
        el.style.setProperty("display", "inline-flex", "important");
        el.style.setProperty("visibility", "visible", "important");
        el.style.setProperty("opacity", "1", "important");
      }
    });

    observer.observe(el.parentElement, { childList: true, subtree: true, attributes: true });
    return () => observer.disconnect();
  }, []);

  return (
    <div
      ref={containerRef}
      id="mp-immutable-footer"
      data-mp-enforced="true"
      className={cn("mp-immutable-footer inline-flex items-center justify-center select-none py-1", className)}
      style={{
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        userSelect: "none",
        zIndex: 10,
      }}
    >
      <a
        href={poweredBy.url}
        target="_blank"
        rel="noopener noreferrer"
        className="group inline-flex items-center gap-2 px-3 py-1 rounded-full border border-gray-200/90 dark:border-white/10 bg-white/70 dark:bg-white/[0.03] shadow-theme-xs backdrop-blur-md transition-all duration-200 hover:border-brand-500/40 hover:bg-white dark:hover:bg-white/[0.06] hover:shadow-theme-sm"
        title="Midnight Panel - Modern Game Server Management"
      >
        {/* Midnight Monogram Badge */}
        <span className="size-3.5 rounded-full bg-linear-to-tr from-brand-600 to-indigo-500 flex items-center justify-center text-[9px] font-bold text-white shadow-2xs shrink-0 leading-none">
          M
        </span>

        {/* Text */}
        <span className="text-[11px] font-medium text-gray-500 dark:text-gray-400 group-hover:text-gray-700 dark:group-hover:text-gray-300 transition-colors leading-normal flex items-center gap-1">
          <span>Powered by</span>
          <span className="font-semibold text-gray-900 dark:text-white group-hover:text-brand-500 dark:group-hover:text-brand-400 transition-colors">
            Midnight Panel
          </span>
        </span>

        {/* Subtle Arrow Icon */}
        <svg
          className="size-2.5 text-gray-400 dark:text-gray-500 opacity-60 group-hover:opacity-100 group-hover:text-brand-500 dark:group-hover:text-brand-400 transition-all -ml-0.5 group-hover:translate-x-0.5 group-hover:-translate-y-0.5 shrink-0"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          strokeWidth={2.5}
        >
          <path strokeLinecap="round" strokeLinejoin="round" d="M4.5 19.5l15-15m0 0H8.25m11.25 0v11.25" />
        </svg>
      </a>
    </div>
  );
}
