import type { PagedQuery, PagedResult } from "@/api/types";
import { Button } from "@/components/ui/button";
import { InputGroup, InputGroupAddon, InputGroupInput } from "@/components/ui/input-group";
import { Spinner } from "@/components/ui/spinner";
import { Table, TableBody, TableCell, TableHeader, TableRow } from "@/components/ui/table";
import { useT } from "@/i18n";
import { ChevronDownIcon, ChevronLeftIcon, ChevronRightIcon, ChevronUpDownIcon, ChevronUpIcon, SearchIcon } from "@/icons";
import { cn } from "@/utils";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useEffect, useState, type ReactNode } from "react";

export interface DataTableColumn<T> {
  key: string;
  title: string;
  /** Sort key the api understands, omit for unsortable columns */
  sortKey?: string;
  align?: "start" | "end";
  render: (item: T) => ReactNode;
}

const headerCell = "px-5 py-3 text-start text-xs font-medium text-gray-500 sm:px-6 dark:text-gray-400";
const bodyCell = "px-5 py-4 text-sm text-gray-500 sm:px-6 dark:text-gray-400";

/** Table with server side search, sorting and pagination */
export function DataTable<T>({ queryKey, load, columns, rowKey, title, description, toolbar, pageSize = 25 }: {
  queryKey: unknown[];
  load: (query: PagedQuery) => Promise<PagedResult<T>>;
  columns: DataTableColumn<T>[];
  rowKey: (item: T) => string | number;
  title?: string;
  description?: string;
  toolbar?: ReactNode;
  pageSize?: number;
}) {
  const { t } = useT();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [sort, setSort] = useState<{ key: string; descending: boolean } | null>(null);

  useEffect(() => {
    const timeout = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(timeout);
  }, [search]);

  const query: PagedQuery = { page, pageSize, search: debouncedSearch || undefined, sortBy: sort?.key, sortDescending: sort?.descending };

  const { data, isFetching } = useQuery({ queryKey: [...queryKey, query], queryFn: () => load(query), placeholderData: keepPreviousData });

  const toggleSort = (key: string) =>
    setSort((current) => (current?.key === key ? { key, descending: !current.descending } : { key, descending: false }));

  const totalPages = Math.max(1, data?.totalPages ?? 1);
  const first = data && data.totalCount > 0 ? (data.page - 1) * data.pageSize + 1 : 0;
  const last = data ? first + Math.max(0, data.items.length - 1) : 0;

  return (
    <div className="overflow-hidden rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.02]">
      <div className="flex flex-col gap-4 border-b border-gray-100 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6 dark:border-gray-800">
        {title ? (
          <div>
            <h3 className="text-lg font-semibold text-gray-800 dark:text-white/90">{title}</h3>
            {description && <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">{description}</p>}
          </div>
        ) : (
          <span />
        )}
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <InputGroup className="sm:w-72">
            <InputGroupAddon align="inline-start">
              <SearchIcon className="size-5 text-gray-500 dark:text-gray-400" />
            </InputGroupAddon>
            <InputGroupInput
              placeholder={t("Table.Search")}
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
                setPage(1);
              }}
            />
          </InputGroup>
          {toolbar}
        </div>
      </div>

      <div className="max-w-full overflow-x-auto">
        <Table>
          <TableHeader className="border-b border-gray-100 dark:border-white/5">
            <TableRow>
              {columns.map((column) => (
                <TableCell key={column.key} isHeader className={cn(headerCell, column.align === "end" && "text-end")}>
                  {column.sortKey ? (
                    <button type="button" onClick={() => toggleSort(column.sortKey!)} className="inline-flex items-center gap-1 hover:text-gray-700 dark:hover:text-gray-200">
                      {column.title}
                      {sort?.key !== column.sortKey ? (
                        <ChevronUpDownIcon className="size-4" />
                      ) : sort.descending ? (
                        <ChevronDownIcon className="size-4" />
                      ) : (
                        <ChevronUpIcon className="size-4" />
                      )}
                    </button>
                  ) : (
                    column.title
                  )}
                </TableCell>
              ))}
            </TableRow>
          </TableHeader>

          <TableBody className={cn("divide-y divide-gray-100 dark:divide-white/5", isFetching && data && "opacity-60")}>
            {!data ? (
              <TableRow>
                <TableCell colSpan={columns.length} className="py-12 text-center text-brand-500">
                  <Spinner className="mx-auto size-6" />
                </TableCell>
              </TableRow>
            ) : data.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={columns.length} className="py-12 text-center text-sm text-gray-500 dark:text-gray-400">
                  {t("Table.Empty")}
                </TableCell>
              </TableRow>
            ) : (
              data.items.map((item) => (
                <TableRow key={rowKey(item)}>
                  {columns.map((column) => (
                    <TableCell key={column.key} className={cn(bodyCell, column.align === "end" && "text-end")}>
                      {column.render(item)}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {data && data.totalCount > 0 && (
        <div className="flex flex-col items-center justify-between gap-3 border-t border-gray-100 px-5 py-4 sm:flex-row sm:px-6 dark:border-gray-800">
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("Table.Showing", first, last, data.totalCount)}</p>
          <div className="flex items-center gap-1">
            <Button variant="outline" size="icon-sm" disabled={page <= 1} onClick={() => setPage(page - 1)} aria-label={t("Table.Previous")}>
              <ChevronLeftIcon />
            </Button>
            {Array.from({ length: totalPages }, (_, index) => index + 1)
              .filter((p) => Math.abs(p - page) <= 2 || p === 1 || p === totalPages)
              .map((p) => (
                <Button key={p} variant={p === page ? "default" : "ghost"} size="icon-sm" onClick={() => setPage(p)}>
                  {p}
                </Button>
              ))}
            <Button variant="outline" size="icon-sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)} aria-label={t("Table.Next")}>
              <ChevronRightIcon />
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

/** Shared table cell styles for tables that don't need paging */
export const tableStyles = { headerCell, bodyCell };
