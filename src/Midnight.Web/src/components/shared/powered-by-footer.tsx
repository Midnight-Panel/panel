import { useEffect, useRef } from "react";
import { useBranding } from "@/contexts/BrandingContext";
import { cn } from "@/utils";

const DEFAULT_URL = "https://github.com/Midnight-Panel/panel";

const tones = {
  light: {
    pill: "border-gray-200 bg-white text-gray-500 shadow-theme-xs hover:border-brand-300",
    name: "text-gray-900",
    divider: "bg-gray-200",
  },
  dark: {
    pill: "border-white/10 bg-white/[0.03] text-gray-400 hover:border-brand-500/50 hover:bg-white/[0.06]",
    name: "text-white",
    divider: "bg-white/10",
  },
  auto: {
    pill: "border-gray-200 bg-white text-gray-500 shadow-theme-xs hover:border-brand-300 dark:border-white/10 dark:bg-white/[0.03] dark:text-gray-400 dark:shadow-none dark:hover:border-brand-500/50 dark:hover:bg-white/[0.06]",
    name: "text-gray-900 dark:text-white",
    divider: "bg-gray-200 dark:bg-white/10",
  },
};

interface PoweredByFooterProps {
  className?: string;
  /** "auto" follows the panel theme, light/dark force a look (e.g. inside the customizer preview) */
  tone?: keyof typeof tones;
}

/** "Powered by Midnight" badge shown in every footer. Custom CSS can't hide it (see CssSanitizer). */
export function PoweredByFooter({ className, tone = "auto" }: PoweredByFooterProps) {
  const { branding } = useBranding();
  const ref = useRef<HTMLAnchorElement>(null);
  const styles = tones[tone];

  // Only http(s) links, everything else falls back to the project page
  const configured = branding?.poweredBy?.url;
  const url = configured && /^https?:\/\//i.test(configured) ? configured : DEFAULT_URL;

  // Undo inline style changes that hide the badge (e.g. from browser extensions or scripts)
  useEffect(() => {
    const element = ref.current;
    if (!element) return;

    const observer = new MutationObserver(() => {
      if (element.style.display || element.style.visibility || element.style.opacity) element.removeAttribute("style");
    });

    observer.observe(element, { attributes: true, attributeFilter: ["style", "hidden"] });
    return () => observer.disconnect();
  }, []);

  return (
    <a
      ref={ref}
      id="mp-immutable-footer"
      data-mp-enforced="true"
      href={url}
      target="_blank"
      rel="noopener noreferrer"
      aria-label="Powered by Midnight Panel"
      className={cn(
        "mp-immutable-footer group relative inline-flex h-8 shrink-0 items-center gap-2 overflow-hidden rounded-full border py-1 ps-1 pe-3 text-xs whitespace-nowrap transition-all duration-300 select-none",
        "hover:shadow-[0_6px_24px_-8px_var(--mp-primary,#465fff)] focus-visible:ring-3 focus-visible:ring-brand-500/30 focus-visible:outline-none",
        styles.pill,
        className,
      )}
    >
      {/* Light sweep on hover */}
      <span
        aria-hidden="true"
        className="pointer-events-none absolute inset-y-0 -start-1/2 w-1/3 -skew-x-12 bg-linear-to-r from-transparent via-white/25 to-transparent opacity-0 transition-all duration-700 ease-out group-hover:start-[130%] group-hover:opacity-100"
      />

      <span className="relative grid size-6 shrink-0 place-items-center rounded-full bg-[linear-gradient(135deg,var(--mp-primary,#465fff),var(--mp-accent,#7c3aed))] shadow-[inset_0_1px_0_rgb(255_255_255/0.25)] transition-transform duration-300 group-hover:scale-105">
        <img src="/images/logo/logo-icon-dark.png" alt="" className="size-3.5 object-contain" draggable={false} />
      </span>

      <span className="relative font-medium">Powered by</span>
      <span aria-hidden="true" className={cn("relative h-3 w-px", styles.divider)} />
      <span className={cn("relative font-outfit text-[13px] font-semibold tracking-tight", styles.name)}>Midnight</span>

      <svg
        aria-hidden="true"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth={2.5}
        strokeLinecap="round"
        strokeLinejoin="round"
        className="relative -ms-0.5 size-3 opacity-50 transition-all duration-300 group-hover:translate-x-0.5 group-hover:-translate-y-0.5 group-hover:text-brand-500 group-hover:opacity-100"
      >
        <path d="M7 17 17 7M8 7h9v9" />
      </svg>
    </a>
  );
}
