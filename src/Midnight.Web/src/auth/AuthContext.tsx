import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { api, ApiError, onUnauthorized, tokens } from "@/api/client";
import type { CurrentUser, ImpersonationResponse, LoginResponse, PublicSettings } from "@/api/types";
import { hasPermission } from "@/lib/permissions";

interface AuthContextValue {
  user: CurrentUser | null;
  settings: PublicSettings;
  ready: boolean;
  isImpersonating: boolean;
  completeLogin: (response: LoginResponse) => Promise<void>;
  logout: () => Promise<void>;
  reload: () => Promise<void>;
  reloadSettings: () => Promise<void>;
  startImpersonation: (response: ImpersonationResponse) => Promise<void>;
  stopImpersonation: () => Promise<void>;
  /** For showing or hiding ui only, the api enforces permissions itself */
  can: (permission: string) => boolean;
}

const defaultSettings: PublicSettings = { appName: "Midnight", version: "", registrationEnabled: false, setupRequired: false };

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [settings, setSettings] = useState<PublicSettings>(defaultSettings);
  const [ready, setReady] = useState(false);
  const [isImpersonating, setIsImpersonating] = useState(false);

  const reload = useCallback(async () => {
    try {
      const token = await tokens.getAccessToken();
      setUser(token ? await api.get<CurrentUser>("/api/account") : null);
    } catch (error) {
      if (error instanceof ApiError && (error.status === 401 || error.status === 403)) setUser(null);
      else throw error;
    } finally {
      setIsImpersonating(tokens.isImpersonating);
    }
  }, []);

  const reloadSettings = useCallback(async () => {
    try {
      setSettings(await api.get<PublicSettings>("/api/auth/settings"));
    } catch {
      // Not critical, defaults are used
    }
  }, []);

  useEffect(() => {
    onUnauthorized(() => {
      tokens.clear();
      setUser(null);
      setIsImpersonating(false);
    });

    tokens.onImpersonationExpired = () => void reload();

    Promise.all([reloadSettings(), reload()]).finally(() => setReady(true));

    return () => onUnauthorized(null);
  }, [reload, reloadSettings]);

  const completeLogin = useCallback(
    async (response: LoginResponse) => {
      tokens.setAccessToken(response);
      await reload();
    },
    [reload]
  );

  const stopImpersonation = useCallback(async () => {
    tokens.stopImpersonation();
    await reload();
  }, [reload]);

  const logout = useCallback(async () => {
    if (tokens.isImpersonating) {
      await stopImpersonation();
      return;
    }

    try {
      await api.post("/api/auth/logout");
    } finally {
      tokens.clear();
      setUser(null);
    }
  }, [stopImpersonation]);

  const startImpersonation = useCallback(
    async (response: ImpersonationResponse) => {
      tokens.startImpersonation(response.accessToken, response.expiresAt);
      await reload();
    },
    [reload]
  );

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      settings,
      ready,
      isImpersonating,
      completeLogin,
      logout,
      reload,
      reloadSettings,
      startImpersonation,
      stopImpersonation,
      can: (permission: string) => !!user && hasPermission(user.permissions, permission)
    }),
    [user, settings, ready, isImpersonating, completeLogin, logout, reload, reloadSettings, startImpersonation, stopImpersonation]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used inside AuthProvider");
  return context;
}
