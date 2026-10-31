import { useState } from "react";
import { PoweredByFooter } from "@/components/shared/powered-by-footer";
import { Badge } from "@/components/ui/badge";
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
  primaryColor: string;
  accentColor: string;
  loginBackgroundType: "none" | "gradient" | "image";
  loginBackgroundValue?: string | null;
  logoLightUrl?: string | null;
  logoDarkUrl?: string | null;
}

export function LivePreview({
  panelName,
  primaryColor,
  accentColor,
  loginBackgroundType,
  loginBackgroundValue,
  logoLightUrl,
  logoDarkUrl,
}: LivePreviewProps) {
  const [viewMode, setViewMode] = useState<"dashboard" | "login" | "email">("dashboard");
  const [theme, setTheme] = useState<"dark" | "light">("dark");

  const isDark = theme === "dark";
  const activeLogo = isDark ? logoDarkUrl || logoLightUrl : logoLightUrl || logoDarkUrl;

  return (
    <div className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-gray-50/50 dark:bg-white/[0.02] p-4 lg:p-5 backdrop-blur-sm shadow-theme-xs">
      {/* Header bar with Mode Tabs & Theme Switch */}
      <div className="flex flex-wrap items-center justify-between gap-3 pb-4 border-b border-gray-200 dark:border-gray-800">
        <div className="flex items-center gap-2">
          {/* macOS window dots */}
          <div className="flex items-center gap-1.5 mr-2">
            <span className="size-2.5 rounded-full bg-red-500/80" />
            <span className="size-2.5 rounded-full bg-amber-500/80" />
            <span className="size-2.5 rounded-full bg-emerald-500/80" />
          </div>
          <span className="font-outfit text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400">
            Live-Vorschau
          </span>
        </div>

        {/* View mode toggle */}
        <div className="flex items-center gap-1 rounded-lg bg-gray-200/60 dark:bg-gray-800/80 p-0.5 text-xs">
          <button
            type="button"
            onClick={() => setViewMode("dashboard")}
            className={`flex items-center gap-1.5 px-2.5 py-1 rounded-md font-medium transition-all duration-150 ${
              viewMode === "dashboard"
                ? "bg-white dark:bg-gray-700 text-gray-900 dark:text-white shadow-xs"
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
                ? "bg-white dark:bg-gray-700 text-gray-900 dark:text-white shadow-xs"
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
                ? "bg-white dark:bg-gray-700 text-gray-900 dark:text-white shadow-xs"
                : "text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white"
            }`}
          >
            <MailIcon className="size-3.5" />
            <span className="hidden sm:inline">E-Mail</span>
          </button>
        </div>

        {/* Dark / Light toggle */}
        <button
          type="button"
          onClick={() => setTheme((prev) => (prev === "dark" ? "light" : "dark"))}
          className="flex items-center gap-1.5 px-2.5 py-1 rounded-lg border border-gray-200 dark:border-gray-700 text-xs font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors"
          title={isDark ? "Zu hellem Modus wechseln" : "Zu dunklem Modus wechseln"}
        >
          {isDark ? (
            <>
              <MoonIcon className="size-3.5 text-indigo-400" />
              <span>Dunkel</span>
            </>
          ) : (
            <>
              <SunIcon className="size-3.5 text-amber-500" />
              <span>Hell</span>
            </>
          )}
        </button>
      </div>

      {/* Screen container */}
      <div
        className={`mt-4 rounded-xl border overflow-hidden min-h-[460px] flex flex-col justify-between transition-colors duration-200 ${
          isDark
            ? "bg-[#0B0F19] border-gray-800 text-gray-200"
            : "bg-slate-50 border-gray-200 text-gray-800"
        }`}
      >
        {/* VIEW: DASHBOARD */}
        {viewMode === "dashboard" && (
          <div className="flex flex-col flex-1">
            {/* Topbar */}
            <div
              className={`flex items-center justify-between px-4 py-2.5 border-b text-xs ${
                isDark ? "border-gray-800/80 bg-[#111827]/60" : "border-gray-200 bg-white"
              }`}
            >
              <div className="flex items-center gap-3">
                {activeLogo ? (
                  <img
                    src={activeLogo}
                    alt={panelName}
                    className="h-6 w-auto object-contain max-w-[110px]"
                  />
                ) : (
                  <div className="flex items-center gap-1.5 font-outfit font-semibold text-sm">
                    <div
                      className="size-6 rounded-md flex items-center justify-center text-white text-xs font-bold"
                      style={{ backgroundColor: primaryColor }}
                    >
                      {panelName.slice(0, 1)}
                    </div>
                    <span className="truncate max-w-[130px]">{panelName}</span>
                  </div>
                )}
              </div>

              <div className="flex items-center gap-2">
                <div
                  className={`hidden sm:block text-[11px] px-2 py-0.5 rounded border ${
                    isDark ? "border-gray-800 text-gray-400" : "border-gray-200 text-gray-500"
                  }`}
                >
                  Suche (⌘K)
                </div>
                <div
                  className="size-6 rounded-full flex items-center justify-center text-[10px] font-bold text-white"
                  style={{ backgroundColor: accentColor }}
                >
                  AD
                </div>
              </div>
            </div>

            {/* Main Area: Mini Sidebar + Content */}
            <div className="flex flex-1">
              {/* Mini Sidebar */}
              <div
                className={`w-32 border-r p-2 flex flex-col justify-between text-xs hidden sm:flex ${
                  isDark ? "border-gray-800/80 bg-[#0E131F]" : "border-gray-200 bg-white/70"
                }`}
              >
                <div className="space-y-1">
                  <div
                    className="px-2.5 py-1.5 rounded-md font-medium text-white text-[11px] flex items-center gap-1.5"
                    style={{ backgroundColor: primaryColor }}
                  >
                    <LayoutIcon className="size-3" />
                    <span>Server</span>
                  </div>
                  <div
                    className={`px-2.5 py-1.5 rounded-md text-[11px] flex items-center gap-1.5 ${
                      isDark ? "text-gray-400 hover:text-white" : "text-gray-600 hover:text-black"
                    }`}
                  >
                    <BoxIcon className="size-3" />
                    <span>Shards</span>
                  </div>
                  <div
                    className={`px-2.5 py-1.5 rounded-md text-[11px] flex items-center gap-1.5 ${
                      isDark ? "text-gray-400 hover:text-white" : "text-gray-600 hover:text-black"
                    }`}
                  >
                    <DatabaseIcon className="size-3" />
                    <span>Nodes</span>
                  </div>
                </div>

                <div className="pt-2 border-t border-gray-800/50 text-[10px] text-gray-500 truncate">
                  v2.0 • Online
                </div>
              </div>

              {/* Content Canvas */}
              <div className="flex-1 p-4 space-y-3.5">
                <div className="flex items-center justify-between">
                  <div>
                    <h5 className="font-outfit font-semibold text-sm">Server-Übersicht</h5>
                    <p className="text-[11px] text-gray-500 dark:text-gray-400">
                      Aktuelle Leistungsdaten und Status
                    </p>
                  </div>
                  <button
                    type="button"
                    className="px-3 py-1.5 rounded-lg text-xs font-medium text-white shadow-xs transition-opacity hover:opacity-90"
                    style={{ backgroundColor: primaryColor }}
                  >
                    Neuer Server
                  </button>
                </div>

                {/* Stat Cards */}
                <div className="grid grid-cols-2 gap-2.5">
                  <div
                    className={`p-3 rounded-xl border ${
                      isDark ? "bg-[#131927] border-gray-800/80" : "bg-white border-gray-200"
                    }`}
                  >
                    <div className="flex items-center justify-between text-xs">
                      <span className="text-gray-500 dark:text-gray-400 text-[11px]">Nodes</span>
                      <ChipIcon className="size-3.5 text-gray-400" />
                    </div>
                    <div className="text-lg font-outfit font-bold mt-1">4</div>
                    <div className="flex items-center gap-1 mt-1">
                      <span
                        className="text-[10px] font-semibold px-1.5 py-0.2 rounded-full text-white"
                        style={{ backgroundColor: accentColor }}
                      >
                        100% Bereit
                      </span>
                    </div>
                  </div>

                  <div
                    className={`p-3 rounded-xl border ${
                      isDark ? "bg-[#131927] border-gray-800/80" : "bg-white border-gray-200"
                    }`}
                  >
                    <div className="flex items-center justify-between text-xs">
                      <span className="text-gray-500 dark:text-gray-400 text-[11px]">Shards</span>
                      <BoxIcon className="size-3.5 text-gray-400" />
                    </div>
                    <div className="text-lg font-outfit font-bold mt-1">18</div>
                    <div className="flex items-center gap-1 mt-1 text-[10px] text-emerald-400 font-medium">
                      ● 18 aktiv
                    </div>
                  </div>
                </div>

                {/* Sample Card with Badges */}
                <div
                  className={`p-3.5 rounded-xl border flex items-center justify-between ${
                    isDark ? "bg-[#131927] border-gray-800/80" : "bg-white border-gray-200"
                  }`}
                >
                  <div className="flex items-center gap-2.5">
                    <div
                      className="size-8 rounded-lg flex items-center justify-center text-white"
                      style={{ backgroundColor: `${primaryColor}22`, color: primaryColor }}
                    >
                      <BoxIcon className="size-4" />
                    </div>
                    <div>
                      <div className="text-xs font-semibold">Minecraft Survival #1</div>
                      <div className="text-[10px] text-gray-500">12/30 Spieler • Paper 1.21</div>
                    </div>
                  </div>
                  <span
                    className="text-[11px] font-medium px-2 py-0.5 rounded text-white"
                    style={{ backgroundColor: accentColor }}
                  >
                    Akzent-Farbe
                  </span>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* VIEW: LOGIN */}
        {viewMode === "login" && (
          <div
            className="flex-1 flex flex-col items-center justify-center p-6 relative overflow-hidden"
            style={{
              background:
                loginBackgroundType === "gradient" && loginBackgroundValue
                  ? loginBackgroundValue
                  : loginBackgroundType === "image" && loginBackgroundValue
                    ? `linear-gradient(rgba(11, 15, 25, 0.75), rgba(11, 15, 25, 0.85)), url('${loginBackgroundValue}') center / cover no-repeat`
                    : isDark
                      ? "#0B0F19"
                      : "#F8FAFC",
            }}
          >
            {/* Login Card */}
            <div
              className={`w-full max-w-xs p-5 rounded-2xl border backdrop-blur-md shadow-theme-lg space-y-4 ${
                isDark
                  ? "bg-black/60 border-white/10 text-white"
                  : "bg-white/90 border-black/10 text-gray-900"
              }`}
            >
              <div className="text-center space-y-1">
                {activeLogo ? (
                  <img
                    src={activeLogo}
                    alt={panelName}
                    className="h-8 w-auto mx-auto object-contain"
                  />
                ) : (
                  <div
                    className="size-9 rounded-xl flex items-center justify-center text-white font-bold mx-auto text-base"
                    style={{ backgroundColor: primaryColor }}
                  >
                    {panelName.slice(0, 1)}
                  </div>
                )}
                <h6 className="font-outfit font-semibold text-sm pt-1">{panelName}</h6>
                <p className="text-[11px] text-gray-400">Melde dich bei deinem Konto an</p>
              </div>

              <div className="space-y-2">
                <div>
                  <div className="text-[10px] font-medium mb-1 text-gray-400">E-Mail</div>
                  <div
                    className={`h-7 rounded-md border px-2 flex items-center text-xs text-gray-500 ${
                      isDark ? "border-gray-700 bg-gray-900/60" : "border-gray-200 bg-white"
                    }`}
                  >
                    admin@midnightpanel.io
                  </div>
                </div>

                <div>
                  <div className="text-[10px] font-medium mb-1 text-gray-400">Passwort</div>
                  <div
                    className={`h-7 rounded-md border px-2 flex items-center text-xs text-gray-500 ${
                      isDark ? "border-gray-700 bg-gray-900/60" : "border-gray-200 bg-white"
                    }`}
                  >
                    ••••••••••••
                  </div>
                </div>
              </div>

              <button
                type="button"
                className="w-full py-1.5 rounded-lg text-xs font-semibold text-white transition-opacity hover:opacity-95"
                style={{ backgroundColor: primaryColor }}
              >
                Anmelden
              </button>

              <div className="text-center">
                <span
                  className="text-[10px] font-medium hover:underline cursor-pointer"
                  style={{ color: accentColor }}
                >
                  Passwort vergessen?
                </span>
              </div>
            </div>
          </div>
        )}

        {/* VIEW: EMAIL */}
        {viewMode === "email" && (
          <div
            className={`flex-1 p-5 flex flex-col items-center justify-center ${
              isDark ? "bg-[#090D16]" : "bg-gray-100"
            }`}
          >
            {/* Email Envelope Box */}
            <div
              className={`w-full max-w-sm rounded-xl border p-5 shadow-theme-xs space-y-3.5 ${
                isDark ? "bg-[#111827] border-gray-800 text-white" : "bg-white border-gray-200 text-gray-900"
              }`}
            >
              {/* Email Branding Header */}
              <div className="flex items-center justify-between border-b border-gray-800/50 pb-3">
                {activeLogo ? (
                  <img
                    src={activeLogo}
                    alt={panelName}
                    className="h-6 w-auto object-contain max-w-[120px]"
                  />
                ) : (
                  <div className="font-outfit font-semibold text-sm flex items-center gap-1.5">
                    <span
                      className="size-5 rounded flex items-center justify-center text-white text-[10px] font-bold"
                      style={{ backgroundColor: primaryColor }}
                    >
                      {panelName.slice(0, 1)}
                    </span>
                    <span>{panelName}</span>
                  </div>
                )}
                <Badge variant="light" color="primary" size="sm">
                  Systemmeldung
                </Badge>
              </div>

              {/* Message */}
              <div className="space-y-2 text-xs">
                <p className="font-medium">Hallo Administrator,</p>
                <p className="text-gray-400 text-[11px] leading-relaxed">
                  Dein Game-Server <strong className="text-gray-200">Survival Shard #1</strong> wurde erfolgreich auf Node <strong>Node-Frankfurt-01</strong> provisioniert und ist jetzt betriebsbereit.
                </p>
              </div>

              {/* Call to action button */}
              <div className="pt-1 text-center">
                <button
                  type="button"
                  className="px-4 py-2 rounded-lg text-xs font-semibold text-white shadow-xs"
                  style={{ backgroundColor: primaryColor }}
                >
                  Server im Panel öffnen
                </button>
              </div>

              {/* Note */}
              <div className="text-[10px] text-gray-500 text-center pt-1 border-t border-gray-800/40">
                Diese Benachrichtigung wurde automatisch von {panelName} gesendet.
              </div>
            </div>
          </div>
        )}

        {/* NON-NEGOTIABLE FOOTER: Permanently visible across all views and never removable */}
        <div
          className={`px-4 py-2.5 border-t text-center text-xs shrink-0 select-none ${
            isDark ? "border-gray-800/80 bg-[#090D16]/90 text-gray-400" : "border-gray-200 bg-white text-gray-600"
          }`}
        >
          <PoweredByFooter />
        </div>
      </div>
    </div>
  );
}
