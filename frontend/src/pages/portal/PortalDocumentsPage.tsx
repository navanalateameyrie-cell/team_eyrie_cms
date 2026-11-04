import { useEffect, useState } from "react";
import { apiFetch } from "../../api/client";
import { Card, PageHeader, Button, Badge, EmptyState, LoadingSpinner } from "../../components/ui";

interface D {
  id: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  category: string | null;
  description: string | null;
  uploadedAtUtc: string;
}

function fmtBytes(n: number): string {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`;
  if (n < 1024 * 1024 * 1024) return `${(n / 1024 / 1024).toFixed(1)} MB`;
  return `${(n / 1024 / 1024 / 1024).toFixed(2)} GB`;
}

function getFileIcon(fileName: string, contentType: string) {
  const ext = fileName.split(".").pop()?.toLowerCase();
  if (ext === "pdf" || contentType.includes("pdf")) {
    return (
      <div className="w-9 h-9 rounded-lg bg-red-50 text-red-600 flex items-center justify-center shrink-0 border border-red-200">
        <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 21h10a2 2 0 002-2V9.414a1 1 0 00-.293-.707l-5.414-5.414A1 1 0 0012.586 3H7a2 2 0 00-2 2v14a2 2 0 002 2z" />
        </svg>
      </div>
    );
  }
  if (["xls", "xlsx", "csv"].includes(ext || "") || contentType.includes("sheet") || contentType.includes("csv")) {
    return (
      <div className="w-9 h-9 rounded-lg bg-emerald-50 text-emerald-600 flex items-center justify-center shrink-0 border border-emerald-200">
        <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
        </svg>
      </div>
    );
  }
  return (
    <div className="w-9 h-9 rounded-lg bg-brand-50 text-brand-600 flex items-center justify-center shrink-0 border border-brand-200">
      <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
      </svg>
    </div>
  );
}

export default function PortalDocumentsPage() {
  const [rows, setRows] = useState<D[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  useEffect(() => {
    apiFetch<D[]>("/api/v1/portal/documents")
      .then(setRows)
      .catch((e) => setError(String(e)));
  }, []);

  async function download(doc: D) {
    setDownloadingId(doc.id);
    try {
      const token = (() => {
        try {
          return JSON.parse(localStorage.getItem("cms.auth.v1") ?? "null")?.token ?? null;
        } catch {
          return null;
        }
      })();
      const r = await fetch(`/api/v1/portal/documents/${doc.id}/download`, {
        headers: token ? { Authorization: `Bearer ${token}` } : undefined,
      });
      if (!r.ok) {
        alert(`Download failed (${r.status})`);
        return;
      }
      const blob = await r.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = doc.originalFileName;
      document.body.appendChild(a);
      a.click();
      a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 2000);
    } finally {
      setDownloadingId(null);
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Shared Documents Repository"
        subtitle="Access audit reports, tax acknowledgments, financial statements, and supporting schedules shared with your organization."
      />

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 text-sm text-red-700 p-4">
          <p className="font-semibold">Unable to load documents</p>
          <p className="text-xs mt-0.5">{error}</p>
        </div>
      )}

      <Card>
        {rows === null ? (
          <div className="py-16">
            <LoadingSpinner size="md" label="Loading shared documents…" />
          </div>
        ) : rows.length === 0 ? (
          <EmptyState
            title="No documents shared yet"
            description="Your Chartered Accountant practice has not uploaded or shared any files with your account."
          />
        ) : (
          <div className="divide-y divide-slate-100">
            {rows.map((d) => (
              <div
                key={d.id}
                className="p-4 sm:p-5 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 hover:bg-slate-50/80 transition-colors"
              >
                <div className="flex items-start gap-3.5 min-w-0 flex-1">
                  {getFileIcon(d.originalFileName, d.contentType)}
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2 flex-wrap">
                      <button
                        type="button"
                        onClick={() => download(d)}
                        className="text-sm font-semibold text-slate-900 hover:text-brand-600 transition text-left break-all"
                      >
                        {d.originalFileName}
                      </button>
                      {d.category && (
                        <Badge tone="brand" size="sm">
                          {d.category}
                        </Badge>
                      )}
                    </div>
                    {d.description && (
                      <p className="text-xs text-slate-600 mt-1 line-clamp-2">{d.description}</p>
                    )}
                    <div className="flex items-center gap-3 text-[11px] text-slate-400 mt-1.5 flex-wrap">
                      <span className="font-medium text-slate-500">{fmtBytes(d.sizeBytes)}</span>
                      <span>·</span>
                      <span className="font-mono text-slate-500">{d.contentType}</span>
                      <span>·</span>
                      <span>Uploaded {new Date(d.uploadedAtUtc).toLocaleDateString()}</span>
                    </div>
                  </div>
                </div>

                <Button
                  variant="outline"
                  size="sm"
                  busy={downloadingId === d.id}
                  onClick={() => download(d)}
                  icon={
                    <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                    </svg>
                  }
                >
                  Download
                </Button>
              </div>
            ))}
          </div>
        )}
      </Card>
    </div>
  );
}
