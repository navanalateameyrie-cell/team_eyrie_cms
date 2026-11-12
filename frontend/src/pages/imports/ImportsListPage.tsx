import { useCallback, useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { importsApi, type BatchDto, type ImportBatchStatus } from "../../api/imports";
import {
  PageHeader,
  Card,
  Badge,
  LoadingSpinner,
  EmptyState,
} from "../../components/ui";

const statusTone: Record<ImportBatchStatus, "neutral" | "brand" | "warning" | "success" | "danger"> = {
  Received: "neutral",
  Parsed: "brand",
  Validated: "warning",
  Committed: "success",
  Failed: "danger",
  Cancelled: "neutral",
};

function getStatusName(status: ImportBatchStatus | number | string): ImportBatchStatus {
  if (typeof status === "string") {
    const s = status.trim();
    if (s === "1") return "Received";
    if (s === "2") return "Parsed";
    if (s === "3") return "Validated";
    if (s === "4") return "Committed";
    if (s === "5") return "Failed";
    if (s === "6") return "Cancelled";
    return status as ImportBatchStatus;
  }
  switch (status) {
    case 1: return "Received";
    case 2: return "Parsed";
    case 3: return "Validated";
    case 4: return "Committed";
    case 5: return "Failed";
    case 6: return "Cancelled";
    default: return "Received";
  }
}

export default function ImportsListPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("imports.write");

  const [rows, setRows] = useState<BatchDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const fileRef = useRef<HTMLInputElement>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await importsApi.list());
    } catch (ex: unknown) {
      setError(ex instanceof Error ? ex.message : "Failed to load import batches");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function onUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setBusy(true);
    setError(null);
    try {
      const resp = await importsApi.upload(file);
      if (resp.wasDuplicate) {
        alert("File already uploaded (SHA-256 fingerprint match). Navigating to existing batch.");
      }
      window.location.href = `/admin/imports/${resp.batchId}`;
    } catch (ex: unknown) {
      setError(ex instanceof Error ? ex.message : "Upload failed");
    } finally {
      setBusy(false);
      if (fileRef.current) fileRef.current.value = "";
    }
  }

  async function onDelete(b: BatchDto) {
    if (!confirm(`Delete import batch "${b.fileName}"?`)) return;
    setError(null);
    try {
      await importsApi.remove(b.id);
      await load();
    } catch (ex: unknown) {
      setError(ex instanceof Error ? ex.message : "Delete failed");
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        breadcrumbs={[
          { label: "Settings", to: "/admin/settings" },
          { label: "Data Imports" },
        ]}
        title="Client & Engagement Bulk Imports"
        subtitle="Multi-step Excel (.xlsx) ingestion pipeline with staging preview, schema validation, and audit tracking."
        actions={
          canWrite ? (
            <label
              className={`cursor-pointer inline-flex items-center px-4 py-2 rounded-lg bg-brand-500 text-white text-xs font-semibold hover:bg-brand-600 transition-colors shadow-xs ${busy ? "opacity-60 cursor-not-allowed" : ""
                }`}
            >
              <svg className="w-4 h-4 mr-1.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
              </svg>
              {busy ? "Uploading Spreadsheet…" : "+ Upload Excel (.xlsx)"}
              <input
                ref={fileRef}
                type="file"
                accept=".xlsx"
                className="hidden"
                onChange={onUpload}
                disabled={busy}
              />
            </label>
          ) : undefined
        }
      />

      {error && (
        <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-xs text-red-700">
          {error}
        </div>
      )}

      <Card>
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50 font-medium text-slate-500">
              <tr>
                <th className="px-4 py-3 text-left">Workbook File</th>
                <th className="px-4 py-3 text-left">Sheet Tab</th>
                <th className="px-4 py-3 text-right">Parsed Rows</th>
                <th className="px-4 py-3 text-right">Validation Errors</th>
                <th className="px-4 py-3 text-right">Committed (Clients / Files)</th>
                <th className="px-4 py-3 text-left">Pipeline State</th>
                <th className="px-4 py-3 text-left">Ingested At</th>
                {canWrite && <th className="px-4 py-3 text-right">Actions</th>}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {loading && rows.length === 0 ? (
                <tr>
                  <td colSpan={canWrite ? 8 : 7} className="px-4 py-12 text-center">
                    <LoadingSpinner text="Loading import history…" />
                  </td>
                </tr>
              ) : rows.length === 0 ? (
                <tr>
                  <td colSpan={canWrite ? 8 : 7} className="px-4 py-12">
                    <EmptyState
                      title="No import batches uploaded yet"
                      description="Upload an Excel workbook with client records and service files to begin batch validation."
                    />
                  </td>
                </tr>
              ) : (
                rows.map((b) => (
                  <tr key={b.id} className="hover:bg-slate-50/50 transition-colors">
                    <td className="px-4 py-3">
                      <Link
                        to={`/admin/imports/${b.id}`}
                        className="font-semibold text-brand-700 hover:text-brand-900 transition-colors block"
                      >
                        {b.fileName}
                      </Link>
                      <div className="text-[10px] text-slate-400 font-mono truncate max-w-xs mt-0.5">
                        SHA: {b.fileSha256.slice(0, 16)}…
                      </div>
                    </td>

                    <td className="px-4 py-3 text-slate-700">
                      {b.sourceSheet ?? <span className="text-slate-400">—</span>}
                    </td>

                    <td className="px-4 py-3 text-right font-mono font-medium text-slate-800">
                      {b.rowCount.toLocaleString()}
                    </td>

                    <td className="px-4 py-3 text-right font-mono">
                      {b.errorCount > 0 ? (
                        <span className="font-bold text-rose-600 bg-rose-50 px-1.5 py-0.5 rounded">
                          {b.errorCount}
                        </span>
                      ) : (
                        <span className="text-slate-400">0</span>
                      )}
                    </td>

                    <td className="px-4 py-3 text-right font-mono text-slate-700">
                      <strong className="text-emerald-700">{b.committedClientCount}</strong> clients /{" "}
                      <strong className="text-emerald-700">{b.committedServiceFileCount}</strong> files
                    </td>

                    <td className="px-4 py-3 whitespace-nowrap">
                      {(() => {
                        const st = getStatusName(b.status);
                        return (
                          <Badge tone={statusTone[st]} size="sm">
                            {st}
                          </Badge>
                        );
                      })()}
                    </td>

                    <td className="px-4 py-3 text-slate-500 whitespace-nowrap">
                      {new Date(b.uploadedAtUtc).toLocaleString()}
                    </td>
                    {canWrite && (
                      <td className="px-4 py-3 text-right whitespace-nowrap">
                        {b.committedClientCount === 0 && b.committedServiceFileCount === 0 ? (
                          <button
                            type="button"
                            onClick={() => void onDelete(b)}
                            className="text-xs font-medium text-rose-600 hover:text-rose-800 hover:underline"
                          >
                            Delete
                          </button>
                        ) : (
                          <span className="text-slate-300">—</span>
                        )}
                      </td>
                    )}
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </Card>
    </div>
  );
}
