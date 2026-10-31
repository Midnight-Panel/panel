import React, { useEffect, useRef } from "react";
import { useBranding } from "@/contexts/BrandingContext";

/**
 * Unveränderbarer Footer-Hinweis "Powered by Midnight Panel".
 * Ist durch Inline-Styles und einen MutationObserver gegen clientseitiges Entfernen geschützt.
 */
export const PoweredByFooter: React.FC = () => {
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
        el.style.setProperty("display", "flex", "important");
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
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: "0.75rem 1rem",
        fontSize: "0.8125rem",
        fontWeight: 500,
        color: "#94A3B8",
        borderTop: "1px solid rgba(255, 255, 255, 0.08)",
        background: "rgba(15, 23, 42, 0.4)",
        userSelect: "none",
        zIndex: 10,
      }}
    >
      <span>
        {poweredBy.text.includes("Midnight Panel") ? (
          <>
            {poweredBy.text.split("Midnight Panel")[0]}
            <a
              href={poweredBy.url}
              target="_blank"
              rel="noopener noreferrer"
              style={{
                color: "var(--mp-primary, #465FFF)",
                fontWeight: 600,
                textDecoration: "none",
                marginLeft: "0.25rem",
              }}
              onMouseEnter={(e) => (e.currentTarget.style.textDecoration = "underline")}
              onMouseLeave={(e) => (e.currentTarget.style.textDecoration = "none")}
            >
              Midnight Panel
            </a>
          </>
        ) : (
          <a
            href={poweredBy.url}
            target="_blank"
            rel="noopener noreferrer"
            style={{
              color: "var(--mp-primary, #465FFF)",
              fontWeight: 600,
              textDecoration: "none",
            }}
          >
            {poweredBy.text}
          </a>
        )}
      </span>
    </div>
  );
};
