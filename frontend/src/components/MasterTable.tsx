import type { ReactNode } from "react";
import { Button, Input, StatusBadge, LoadingSpinner } from "./ui";

export interface Column<T> {
  header: string;
  cell: (row: T) => ReactNode;
  className?: string;
}

interface Props<T extends { id: string; isActive: boolean }> {
  title: string;
  rows: T[];
  columns: Column<T>[];
  canWrite: boolean;
  onAdd?: () => void;
  onEdit?: (row: T) => void;
  onDeactivate?: (row: T) => void;
  search?: string;
  onSearch?: (q: string) => void;
  showInactive?: boolean;
  onToggleShowInactive?: (v: boolean) => void;
  loading?: boolean;
  error?: string | null;
  extraActions?: ReactNode;
}

export function MasterTable<T extends { id: string; isActive: boolean }>({
  title,
  rows,
  columns,
  canWrite,
  onAdd,
  onEdit,
  onDeactivate,
  search,
  onSearch,
  showInactive,
  onToggleShowInactive,
  loading,
  error,
  extraActions,
}: Props<T>) {
  return (
    <div className="space-y-4">
      <div className="rounded-xl border border-slate-200/90 bg-white shadow-card overflow-hidden">
        <header className="flex flex-wrap items-center justify-between gap-3 px-5 py-4 border-b border-slate-100">
          <div>
            <h2 className="text-base font-semibold text-slate-900 leading-tight">{title}</h2>
            <p className="text-xs text-slate-500 mt-0.5">
              Practice master definitions · {rows.length} records configured
            </p>
          </div>
          <div className="flex items-center gap-3">
            {onSearch && (
              <Input
                type="search"
                value={search ?? ""}
                onChange={(e) => onSearch(e.target.value)}
                placeholder="Search master records…"
                className="py-1 text-xs w-48 sm:w-60"
              />
            )}
            {onToggleShowInactive && (
              <label className="flex items-center gap-1.5 text-xs text-slate-500 cursor-pointer">
                <input
                  type="checkbox"
                  checked={!!showInactive}
                  onChange={(e) => onToggleShowInactive(e.target.checked)}
                  className="rounded border-slate-300 text-brand-600 focus:ring-brand-500"
                />
                <span>Show Inactive</span>
              </label>
            )}
            {extraActions}
            {canWrite && onAdd && (
              <Button
                variant="primary"
                size="sm"
                onClick={onAdd}
              >
                + Add Record
              </Button>
            )}
          </div>
        </header>

        {error && (
          <div className="px-5 py-3 text-xs text-red-700 bg-red-50 border-b border-red-100 flex items-center gap-2">
            <svg className="w-4 h-4 text-red-600 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <span>{error}</span>
          </div>
        )}

        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50 font-medium text-slate-600">
              <tr>
                {columns.map((c) => (
                  <th
                    key={c.header}
                    className={`px-4 py-3 text-left ${c.className ?? ""}`}
                  >
                    {c.header}
                  </th>
                ))}
                <th className="px-4 py-3 text-left w-24">Status</th>
                {canWrite && <th className="px-4 py-3 text-right w-28">Actions</th>}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {loading && (
                <tr>
                  <td
                    colSpan={columns.length + (canWrite ? 2 : 1)}
                    className="px-4 py-12 text-center"
                  >
                    <LoadingSpinner text="Loading master records…" />
                  </td>
                </tr>
              )}
              {!loading && rows.length === 0 && (
                <tr>
                  <td
                    colSpan={columns.length + (canWrite ? 2 : 1)}
                    className="px-4 py-12 text-center text-slate-400 italic"
                  >
                    No records found matching query.
                  </td>
                </tr>
              )}
              {!loading &&
                rows.map((r) => (
                  <tr
                    key={r.id}
                    className={`hover:bg-slate-50/60 transition-colors ${r.isActive ? "" : "text-slate-400 opacity-75"
                      }`}
                  >
                    {columns.map((c) => (
                      <td key={c.header} className={`px-4 py-3 ${c.className ?? ""}`}>
                        {c.cell(r)}
                      </td>
                    ))}
                    <td className="px-4 py-3 whitespace-nowrap">
                      <StatusBadge status={r.isActive} size="sm" />
                    </td>
                    {canWrite && (
                      <td className="px-4 py-3 text-right whitespace-nowrap">
                        <div className="flex items-center justify-end gap-2">
                          {onEdit && (
                            <button
                              type="button"
                              onClick={() => onEdit(r)}
                              className="text-xs font-semibold text-brand-600 hover:text-brand-800 transition-colors"
                            >
                              Edit
                            </button>
                          )}
                          {r.isActive && onDeactivate && (
                            <>
                              <span className="text-slate-200">|</span>
                              <button
                                type="button"
                                onClick={() => onDeactivate(r)}
                                className="text-xs font-semibold text-rose-600 hover:text-rose-800 transition-colors"
                              >
                                Deactivate
                              </button>
                            </>
                          )}
                        </div>
                      </td>
                    )}
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
