import { useEffect, useRef } from "react";
import { useBranding } from "@/contexts/BrandingContext";
import { cn } from "@/utils";

interface PoweredByFooterProps {
  className?: string;
}

/**
 * Pflicht-Branding: Unveränderbarer, geschützter "Powered by Midnight Panel" Footer-Badge.
 * - Höhe ca. 32 px, Schriftgröße 12-13 px mit hohem Kontrast.
 * - Dezenter Blau-nach-Violett-Verlauf-Rand mit sanftem Glow bei Hover.
 * - Logo-Mark mit Verlauf und diagonal animiertes Pfeil-Icon.
 * - Barrierefrei (aria-label, rel, sichtbarer Fokus-Ring) und manipulationssicher geschützt.
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
      className={cn("mp-immutable-footer inline-flex items-center justify-center select-none", className)}
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
        aria-label="Powered by Midnight Panel - Game Server Management"
        className="group relative p-[1px] rounded-full bg-linear-to-r from-blue-500/40 via-indigo-500/40 to-purple-500/40 hover:from-blue-400 hover:via-indigo-400 hover:to-purple-400 transition-all duration-300 shadow-xs hover:shadow-[0_0_16px_rgba(99,102,241,0.28)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-2 focus-visible:ring-offset-[#0B0F19]"
      >
        <span className="h-8 px-3 rounded-full bg-[#0E131F]/95 hover:bg-[#131927] text-slate-200 flex items-center gap-2 backdrop-blur-md transition-colors duration-200">
          {/* Logo-Mark mit feinem Farbverlauf & Ring */}
          <span className="size-4 rounded-full bg-linear-to-tr from-blue-600 via-indigo-600 to-purple-600 flex items-center justify-center text-[10px] font-bold text-white shadow-2xs shrink-0 ring-1 ring-white/20 leading-none">
            M
          </span>

          {/* Typografie: 12.5px Kontrastreich */}
          <span className="text-[12.5px] font-medium text-slate-300 flex items-center gap-1 leading-none">
            <span className="text-slate-400 group-hover:text-slate-300 transition-colors">Powered by</span>
            <span className="font-semibold text-white tracking-tight group-hover:text-indigo-200 transition-colors">
              Midnight Panel
            </span>
          </span>

          {/* Diagonaler Micro-Pfeil mit Hover-Transition */}
          <svg
            className="size-3 text-slate-400 group-hover:text-indigo-300 group-hover:translate-x-0.5 group-hover:-translate-y-0.5 transition-transform duration-200 shrink-0"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth={2.5}
            aria-hidden="true"
          >
            <path strokeLinecap="round" strokeLinejoin="round" d="M4.5 19.5l15-15m0 0H8.25m11.25 0v11.25" />
          </svg>
        </span>
      </a>
    </div>
  );
}
