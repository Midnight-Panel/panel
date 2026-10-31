import { useState } from "react";
import { PoweredByFooter } from "@/components/shared/powered-by-footer";
import type { FooterLinkItem } from "@/api/types";
import {
  SunIcon,
  MoonIcon,
  LayoutIcon,
  LockIcon,
  MailIcon,
  BoxIcon,
  ChipIcon,
  DatabaseIcon,
} from "@/icons";

interface LivePreviewProps {
  panelName: string;
  slogan?: string | null;
  primaryColor: string;
  accentColor: string;
  loginBackgroundType: "none" | "gradient" | "image";
  loginBackgroundValue?: string | null;
  logoLightUrl?: string | null;
  logoDarkUrl?: string | null;
  footerLinks?: FooterLinkItem[];
}

export function LivePreview({
  panelName,
  slogan,
  primaryColor,
  accentColor,
  loginBackgroundType,
  loginBackgroundValue,
  logoLightUrl,
  logoDarkUrl,
  footerLinks = [],
}: LivePreviewProps) {
  const [viewMode, setViewMode] = useState<"dashboard" | "login" | "email">("dashboard");
  const [theme, setTheme] = useState<"dark" | "light">("dark");

  const isDark = theme === "dark";
  const activeLogo = isDark ? logoDarkUrl || logoLightUrl : logoLightUrl || logoDarkUrl;

  const displaySlogan = slogan || "Game Server Management";

  return (
    <div className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-[#141416] p-4 lg:p-5 backdrop-blur-sm shadow-theme-xs">
      {/* Header bar with mode tabs & theme toggle */}
      <div className="flex flex-wrap items-center justify-between gap-3 pb-3.5 border-b border-gray-200 dark:border-gray-800">
        <div className="flex items-center gap-2">
          <div className="flex items-center gap-1.5 mr-1.5">
            <span className="size-2.5 rounded-full bg-red-500/80" />
            <span className="size-2.5 rounded-full bg-amber-500/80" />
            <span className="size-2.5 rounded-full bg-emerald-500/80" />
          </div>
          <span className="font-outfit text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400">
            Live preview
          </span>
        </div>

        {/* View Switcher */}
        <div className="flex items-center gap-1 rounded-lg bg-gray-100 dark:bg-white/[0.04] p-0.5 text-xs">
          <button
            type="button"
            onClick={() => setViewMode("dashboard")}
            className={`flex items-center gap-1.5 px-2.5 py-1 rounded-md font-medium transition-all duration-150 ${
              viewMode === "dashboard"
                ? "bg-white dark:bg-white/10 text-gray-900 dark:text-white shadow-xs"
                : "text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white"
            }`}
          >
            <LayoutIcon className="size-3.5" />
            <span className="hidden sm:inline">Dashboard</span>
          </button>
          <button
            type="button"
            onClick={() => setViewMode("login")}
            className={`flex items-center gap-1.5 px-2.5 py-1 rounded-md font-medium transition-all duration-150 ${
              viewMode === "login"
                ? "bg-white dark:bg-white/10 text-gray-900 dark:text-white shadow-xs"
                : "text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white"
            }`}
          >
            <LockIcon className="size-3.5" />
            <span className="hidden sm:inline">Login</span>
          </button>
          <button
            type="button"
            onClick={() => setViewMode("email")}
            className={`flex items-center gap-1.5 px-2.5 py-1 rounded-md font-medium transition-all duration-150 ${
              viewMode === "email"
                ? "bg-white dark:bg-white/10 text-gray-900 dark:text-white shadow-xs"
                : "text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white"
            }`}
          >
            <MailIcon className="size-3.5" />
            <span className="hidden sm:inline">Email</span>
          </button>
        </div>

        {/* Dark/Light Toggle */}
        <button
          type="button"
          onClick={() => setTheme((prev) => (prev === "dark" ? "light" : "dark"))}
          className="flex items-center gap-1.5 px-2.5 py-1 rounded-lg border border-gray-200 dark:border-gray-700 text-xs font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors"
          title={isDark ? "Switch to light mode" : "Switch to dark mode"}
        >
          {isDark ? (
            <>
              <MoonIcon className="size-3.5 text-indigo-400" />
              <span>Dark</span>
            </>
          ) : (
            <>
              <SunIcon className="size-3.5 text-amber-500" />
              <span>Light</span>
            </>
          )}
        </button>
      </div>

      {/* Screen container */}
      <div
        className={`mt-4 rounded-xl border overflow-hidden min-h-[440px] flex flex-col justify-between transition-colors duration-200 ${
          isDark
            ? "bg-[#08080a] border-gray-800 text-gray-200"
            : "bg-gray-50 border-gray-200 text-gray-800"
        }`}
      >
        {/* VIEW: DASHBOARD */}
        {viewMode === "dashboard" && (
          <div className="flex flex-col flex-1">
            {/* Topbar */}
            <div
              className={`flex items-center justify-between px-3.5 py-2.5 border-b text-xs ${
                isDark ? "border-gray-800/80 bg-[#161619]/60" : "border-gray-200 bg-white"
              }`}
            >
              <div className="flex items-center gap-2.5 overflow-hidden">
                {activeLogo ? (
                  <img
                    src={activeLogo}
                    alt={panelName}
                    className="h-5 w-auto object-contain max-w-[120px]"
                  />
                ) : (
                  <div className="flex items-center gap-1.5 font-outfit font-semibold text-xs overflow-hidden">
                    <div
                      className="size-5 rounded-md flex items-center justify-center text-white text-[10px] font-bold shrink-0"
                      style={{ backgroundColor: primaryColor }}
                    >
                      {panelName.slice(0, 1)}
                    </div>
                    <span className="truncate max-w-[120px]">{panelName}</span>
                  </div>
                )}
              </div>

              <div className="flex items-center gap-2">
                <div
                  className={`text-[10px] px-2 py-0.5 rounded border ${
                    isDark ? "border-gray-800 text-gray-400" : "border-gray-200 text-gray-500"
                  }`}
                >
                  Search (⌘K)
                </div>
                <div
                  className="size-5 rounded-full flex items-center justify-center text-[9px] font-bold text-white shrink-0"
                  style={{ backgroundColor: accentColor }}
                >
                  AD
                </div>
              </div>
            </div>

            {/* Sidebar + Main Content */}
            <div className="flex flex-1">
              <div
                className={`w-28 border-r p-2 flex flex-col justify-between text-xs hidden sm:flex ${
                  isDark ? "border-gray-800/80 bg-[#0e0e10]" : "border-gray-200 bg-white/70"
                }`}
              >
                <div className="space-y-1">
                  <div
                    className="px-2 py-1.5 rounded-md font-medium text-white text-[10px] flex items-center gap-1.5"
                    style={{ backgroundColor: primaryColor }}
                  >
                    <LayoutIcon className="size-3" />
                    <span>Server</span>
                  </div>
                  <div
                    className={`px-2 py-1.5 rounded-md text-[10px] flex items-center gap-1.5 ${
                      isDark ? "text-gray-400 hover:text-white" : "text-gray-600 hover:text-black"
                    }`}
                  >
                    <BoxIcon className="size-3" />
                    <span>Shards</span>
                  </div>
                  <div
                    className={`px-2 py-1.5 rounded-md text-[10px] flex items-center gap-1.5 ${
                      isDark ? "text-gray-400 hover:text-white" : "text-gray-600 hover:text-black"
                    }`}
                  >
                    <DatabaseIcon className="size-3" />
                    <span>Nodes</span>
                  </div>
                </div>

                <div className="pt-2 border-t border-gray-800/40 text-[9px] text-gray-500 font-mono">
                  ● v0.1.0
                </div>
              </div>

              {/* Content Canvas */}
              <div className="flex-1 p-3.5 space-y-3">
                <div className="flex items-center justify-between">
                  <div>
                    <h5 className="font-outfit font-semibold text-xs">Server overview</h5>
                    <p className="text-[10px] text-gray-500 dark:text-gray-400">
                      {displaySlogan}
                    </p>
                  </div>
                  <button
                    type="button"
                    className="px-2.5 py-1 rounded-md text-[11px] font-medium text-white shadow-xs"
                    style={{ backgroundColor: primaryColor }}
                  >
                    New server
                  </button>
                </div>

                {/* Stat Cards */}
                <div className="grid grid-cols-2 gap-2">
                  <div
                    className={`p-2.5 rounded-lg border ${
                      isDark ? "bg-[#161619] border-gray-800/80" : "bg-white border-gray-200"
                    }`}
                  >
                    <div className="flex items-center justify-between text-[10px] text-gray-400">
                      <span>Nodes</span>
                      <ChipIcon className="size-3" />
                    </div>
                    <div className="text-base font-outfit font-bold mt-0.5">4</div>
                    <span
                      className="text-[9px] font-semibold px-1 py-0.2 rounded text-white inline-block mt-0.5"
                      style={{ backgroundColor: accentColor }}
                    >
                      100% ready
                    </span>
                  </div>

                  <div
                    className={`p-2.5 rounded-lg border ${
                      isDark ? "bg-[#161619] border-gray-800/80" : "bg-white border-gray-200"
                    }`}
                  >
                    <div className="flex items-center justify-between text-[10px] text-gray-400">
                      <span>Shards</span>
                      <BoxIcon className="size-3" />
                    </div>
                    <div className="text-base font-outfit font-bold mt-0.5">18</div>
                    <span className="text-[9px] font-medium text-emerald-400 inline-block mt-0.5">
                      ● 18 active
                    </span>
                  </div>
                </div>

                {/* Sample Server Card */}
                <div
                  className={`p-2.5 rounded-lg border flex items-center justify-between ${
                    isDark ? "bg-[#161619] border-gray-800/80" : "bg-white border-gray-200"
                  }`}
                >
                  <div className="flex items-center gap-2">
                    <div
                      className="size-7 rounded-md flex items-center justify-center text-white"
                      style={{ backgroundColor: `${primaryColor}25`, color: primaryColor }}
                    >
                      <BoxIcon className="size-3.5" />
                    </div>
                    <div>
                      <div className="text-[11px] font-semibold">Minecraft Paper</div>
                      <div className="text-[9px] text-gray-500">12/30 players • Online</div>
                    </div>
                  </div>
                  <span
                    className="text-[10px] font-semibold px-1.5 py-0.5 rounded text-white"
                    style={{ backgroundColor: accentColor }}
                  >
                    Accent
                  </span>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* VIEW: LOGIN */}
        {viewMode === "login" && (
          <div
            className="flex-1 flex flex-col items-center justify-center p-5 relative overflow-hidden"
            style={{
              background:
                loginBackgroundType === "gradient" && loginBackgroundValue
                  ? loginBackgroundValue
                  : loginBackgroundType === "image" && loginBackgroundValue
                    ? `linear-gradient(rgba(11, 15, 25, 0.75), rgba(11, 15, 25, 0.85)), url('${loginBackgroundValue}') center / cover no-repeat`
                    : isDark
                      ? "#08080a"
                      : "#F8FAFC",
            }}
          >
            <div
              className={`w-full max-w-[260px] p-4 rounded-xl border backdrop-blur-md shadow-theme-lg space-y-3 ${
                isDark
                  ? "bg-black/60 border-white/10 text-white"
                  : "bg-white/90 border-black/10 text-gray-900"
              }`}
            >
              <div className="text-center space-y-0.5">
                {activeLogo ? (
                  <img
                    src={activeLogo}
                    alt={panelName}
                    className="h-6 w-auto mx-auto object-contain"
                  />
                ) : (
                  <div
                    className="size-8 rounded-lg flex items-center justify-center text-white font-bold mx-auto text-sm"
                    style={{ backgroundColor: primaryColor }}
                  >
                    {panelName.slice(0, 1)}
                  </div>
                )}
                <h6 className="font-outfit font-semibold text-xs pt-1">{panelName}</h6>
                <p className="text-[10px] text-gray-400">{displaySlogan}</p>
              </div>

              <div className="space-y-1.5">
                <div
                  className={`h-6 rounded border px-2 flex items-center text-[10px] text-gray-400 ${
                    isDark ? "border-gray-700 bg-gray-900/60" : "border-gray-200 bg-white"
                  }`}
                >
                  admin@example.com
                </div>
                <div
                  className={`h-6 rounded border px-2 flex items-center text-[10px] text-gray-400 ${
                    isDark ? "border-gray-700 bg-gray-900/60" : "border-gray-200 bg-white"
                  }`}
                >
                  ••••••••••••
                </div>
              </div>

              <button
                type="button"
                className="w-full py-1.5 rounded text-[11px] font-semibold text-white shadow-xs"
                style={{ backgroundColor: primaryColor }}
              >
                Sign in
              </button>
            </div>
          </div>
        )}

        {/* VIEW: EMAIL */}
        {viewMode === "email" && (
          <div
            className={`flex-1 p-4 flex flex-col items-center justify-center ${
              isDark ? "bg-[#08080a]" : "bg-gray-100"
            }`}
          >
            <div
              className={`w-full max-w-xs rounded-xl border p-4 shadow-theme-xs space-y-3 ${
                isDark ? "bg-[#161619] border-gray-800 text-white" : "bg-white border-gray-200 text-gray-900"
              }`}
            >
              <div className="flex items-center justify-between border-b border-gray-800/40 pb-2">
                <div className="font-outfit font-semibold text-xs">{panelName}</div>
                <span
                  className="text-[9px] font-semibold px-1.5 py-0.2 rounded text-white"
                  style={{ backgroundColor: accentColor }}
                >
                  Notification
                </span>
              </div>

              <p className="text-[11px] text-gray-400 leading-relaxed">
                Hi Administrator, your game server has been set up and is online.
              </p>

              <button
                type="button"
                className="w-full py-1.5 rounded text-xs font-semibold text-white shadow-xs"
                style={{ backgroundColor: primaryColor }}
              >
                Open server
              </button>
            </div>
          </div>
        )}

        {/* MANDATORY FOOTER: built into every view and layout */}
        <div
          className={`px-3 py-2.5 border-t text-xs shrink-0 select-none flex flex-wrap items-center justify-between gap-2 ${
            isDark ? "border-gray-800/80 bg-[#0e0e10]" : "border-gray-200 bg-white"
          }`}
        >
          <div className="flex items-center gap-2 text-[10px] text-gray-500 overflow-hidden">
            <span className="truncate">© {panelName}</span>
            {footerLinks && footerLinks.length > 0 && (
              <span className="hidden sm:inline text-gray-400 dark:text-gray-500 truncate">
                · {footerLinks.map((l) => l.label).join(" · ")}
              </span>
            )}
          </div>
          <PoweredByFooter tone={isDark ? "dark" : "light"} />
        </div>
      </div>
    </div>
  );
}
