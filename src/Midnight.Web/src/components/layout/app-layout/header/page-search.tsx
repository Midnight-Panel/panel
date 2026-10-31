import { useAuth } from "@/auth/AuthContext";
import { Badge } from "@/components/ui/badge";
import { Field } from "@/components/ui/field";
import { InputGroup, InputGroupAddon, InputGroupInput } from "@/components/ui/input-group";
import { useT } from "@/i18n";
import { SearchIcon } from "@/icons";
import { cn } from "@/utils";
import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router";
import { adminItems, navItems } from "../sidebar/data";

/** Search over the pages of the panel. Ctrl/Cmd + K focuses it, Enter opens the selected result. */
export function PageSearch() {
  const { t } = useT();
  const { can } = useAuth();
  const navigate = useNavigate();
  const inputRef = useRef<HTMLInputElement>(null);
  const [query, setQuery] = useState("");
  const [focused, setFocused] = useState(false);
  const [selected, setSelected] = useState(0);

  const pages = useMemo(
    () => [
      ...[...navItems, ...adminItems]
        .filter((item) => item.path && (!item.permission || can(item.permission)))
        .map((item) => ({ label: t(item.key), path: item.path! })),
      { label: t("Nav.Account"), path: "/account" },
      { label: t("Nav.Security"), path: "/account/security" },
      { label: t("Nav.ApiKeys"), path: "/account/api-keys" },
      ...(can("admin.users.read") ? [{ label: t("AdminUsers.Sessions"), path: "/admin/users/sessions" }] : []),
      ...(can("admin.roles.read") ? [{ label: t("Nav.Admin.Roles"), path: "/admin/users/roles" }] : []),
    ],
    [t, can],
  );

  const results = pages.filter((page) => page.label.toLowerCase().includes(query.trim().toLowerCase()));

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if ((event.metaKey || event.ctrlKey) && event.key === "k") {
        event.preventDefault();
        inputRef.current?.focus();
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, []);

  const open = (path: string) => {
    navigate(path);
    setQuery("");
    inputRef.current?.blur();
  };

  return (
    <form
      className="relative"
      onSubmit={(event) => {
        event.preventDefault();
        if (results[selected]) open(results[selected].path);
      }}
    >
      <Field className="xl:w-107.5">
        <InputGroup className="border-gray-200 bg-transparent dark:border-gray-800 dark:bg-white/3">
          <InputGroupAddon align="inline-start">
            <SearchIcon className="size-5 text-gray-500 dark:text-gray-400" />
          </InputGroupAddon>
          <InputGroupInput
            ref={inputRef}
            type="text"
            value={query}
            onChange={(event) => {
              setQuery(event.target.value);
              setSelected(0);
            }}
            onFocus={() => setFocused(true)}
            onBlur={() => setTimeout(() => setFocused(false), 150)}
            onKeyDown={(event) => {
              if (event.key === "ArrowDown") setSelected((value) => Math.min(value + 1, results.length - 1));
              if (event.key === "ArrowUp") setSelected((value) => Math.max(value - 1, 0));
              if (event.key === "Escape") inputRef.current?.blur();
            }}
            placeholder={t("Header.Search")}
            className="text-gray-800 placeholder:text-gray-400 dark:text-white/90 dark:placeholder:text-white/30"
          />
          <InputGroupAddon align="inline-end">
            <Badge
              variant="light"
              color="gray"
              size="sm"
              className="gap-0.5 rounded-lg border border-gray-200 bg-gray-50 px-1.75 py-[4.5px] text-xs tracking-[-0.2px] text-gray-500 shadow-none dark:border-gray-800 dark:bg-white/3 dark:text-gray-400"
            >
              <span>⌘</span>
              <span>K</span>
            </Badge>
          </InputGroupAddon>
        </InputGroup>
      </Field>

      {focused && (
        <div className="absolute inset-x-0 top-full z-50 mt-2 overflow-hidden rounded-xl border border-gray-200 bg-white p-2 shadow-theme-lg dark:border-gray-800 dark:bg-gray-800">
          {results.length === 0 ? (
            <p className="px-3 py-2 text-sm text-gray-500 dark:text-gray-400">{t("Table.Empty")}</p>
          ) : (
            results.map((page, index) => (
              <button
                key={page.path}
                type="button"
                onMouseDown={(event) => event.preventDefault()}
                onClick={() => open(page.path)}
                className={cn(
                  "flex w-full items-center justify-between rounded-lg px-3 py-2 text-start text-sm",
                  index === selected
                    ? "bg-brand-50 text-brand-500 dark:bg-brand-500/12 dark:text-brand-400"
                    : "text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-white/5",
                )}
              >
                <span className="font-medium">{page.label}</span>
                <span className="text-xs text-gray-400">{page.path}</span>
              </button>
            ))
          )}
        </div>
      )}
    </form>
  );
}
