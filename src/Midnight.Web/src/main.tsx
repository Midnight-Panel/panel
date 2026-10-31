import { AuthProvider } from "@/auth/AuthContext";
import { TooltipProvider } from "@/components/ui/tooltip";
import { ThemeProvider, useTheme } from "@/contexts/theme-context";
import { I18nProvider } from "@/i18n";
import { BrandingProvider } from "@/contexts/BrandingContext";
import { ConfirmProvider } from "@/lib/confirm";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { HelmetProvider } from "react-helmet-async";
import { BrowserRouter } from "react-router";
import { Toaster } from "sonner";
import App from "./App.tsx";
import "./index.css";

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false, staleTime: 5_000 } },
});

function ThemedToaster() {
  const { theme } = useTheme();
  return <Toaster theme={theme} position="bottom-right" closeButton />;
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <BrowserRouter>
      <ThemeProvider>
        <HelmetProvider>
          <I18nProvider>
            <BrandingProvider>
              <QueryClientProvider client={queryClient}>
                <TooltipProvider>
                  <AuthProvider>
                    <ConfirmProvider>
                      <App />
                    </ConfirmProvider>
                  </AuthProvider>
                </TooltipProvider>
              </QueryClientProvider>
              <ThemedToaster />
            </BrandingProvider>
          </I18nProvider>
        </HelmetProvider>
      </ThemeProvider>
    </BrowserRouter>
  </StrictMode>,
);
