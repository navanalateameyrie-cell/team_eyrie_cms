import { useCallback, useEffect, useRef, useState } from "react";
import { API_BASE_URL, apiFetch, apiRawFetch, ApiError } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { Button, Input, Badge, LoadingSpinner } from "./ui";

export type DocumentOwnerType =
  | "Client"
  | "Branch"
  | "ServiceFile"
  | "ComplianceObligation"
  | "Engagement";

interface OwnerRef {
  ownerType: number | DocumentOwnerType;
  ownerId: string;
}

interface DocumentDto {
  id: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  sha256: string;
  category: string | null;
  description: string | null;
  uploaderName: string | null;
  uploadedAtUtc: string;
  owners: OwnerRef[];
}

interface Props {
  ownerType: DocumentOwnerType;
  ownerId: string;
  title?: string;
}

export default function DocumentsPanel({ ownerType, ownerId, title = "Documents" }: Props) {
  const { hasPermission } = useAuth();
  const canRead = hasPermission("documents.read");
  const canWrite = hasPermission("documents.write");
  const canDelete = hasPermission("documents.delete");
  const canApprove = hasPermission("documents.approve");
  const [docs, setDocs] = useState<DocumentDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [category, setCategory] = useState("");
  const [description, setDescription] = useState("");
  const fileInput = useRef<HTMLInputElement>(null);
  const [expandedDocId, setExpandedDocId] = useState<string | null>(null);
  const [versions, setVersions] = useState<Record<string, DocVersion[]>>({});
  const [approvals, setApprovals] = useState<Record<string, DocApproval[]>>({});

  const load = useCallback(async () => {
    if (!canRead) return;
    try {
      const rows = await apiFetch<DocumentDto[]>(
        `/api/v1/documents?ownerType=${ownerType}&ownerId=${ownerId}`
      );
      setDocs(rows);
    } catch (e) {
      setError(String(e));
      setDocs([]);
    }
  }, [ownerType, ownerId, canRead]);

  useEffect(() => {
    void load();
  }, [load]);

  async function onUpload(e: React.FormEvent) {
    e.preventDefault();
    const file = fileInput.current?.files?.[0];
    if (!file) return;
    setUploading(true);
    setError(null);
    try {
      const form = new FormData();
      form.append("File", file);
      form.append("OwnerType", ownerType);
      form.append("OwnerId", ownerId);
      if (category) form.append("Category", category);
      if (description) form.append("Description", description);
      const res = await apiRawFetch("/api/v1/documents/upload", {
        method: "POST",
        body: form,
      });
      if (!res.ok) {
        const text = await res.text().catch(() => "");
        throw new ApiError(res.status, text);
      }
      if (fileInput.current) fileInput.current.value = "";
      setCategory("");
      setDescription("");
      await load();
    } catch (e) {
      setError(String(e));
    } finally {
      setUploading(false);
    }
  }

  async function download(doc: DocumentDto) {
    const res = await apiRawFetch(`/api/v1/documents/${doc.id}/download`);
    if (!res.ok) {
      setError(`Download failed (${res.status})`);
      return;
    }
    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = doc.originalFileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 2000);
  }

  async function copySignedUrl(doc: DocumentDto) {
    try {
      const res = await apiFetch<{ url: string; expiresAtUtc: string }>(
        `/api/v1/documents/${doc.id}/signed-url?ttlSeconds=600`
      );
      const absolute = (API_BASE_URL || window.location.origin) + res.url;
      await navigator.clipboard.writeText(absolute);
      alert(`Shareable access link copied (valid until ${new Date(res.expiresAtUtc).toLocaleTimeString()}).`);
    } catch (e) {
      setError(String(e));
    }
  }

  async function detach(doc: DocumentDto) {
    if (!canWrite) return;
    if (!confirm(`Detach "${doc.originalFileName}" from this ${ownerType.toLowerCase()}?`)) return;
    try {
      await apiFetch(
        `/api/v1/documents/${doc.id}/owners?ownerType=${ownerType}&ownerId=${ownerId}`,
        { method: "DELETE" }
      );
      await load();
    } catch (e) {
      setError(String(e));
    }
  }

  async function loadDetails(docId: string) {
    try {
      const [vs, as] = await Promise.all([
        apiFetch<DocVersion[]>(`/api/v1/documents/${docId}/versions`),
        apiFetch<DocApproval[]>(`/api/v1/documents/${docId}/approvals`),
      ]);
      setVersions((m) => ({ ...m, [docId]: vs }));
      setApprovals((m) => ({ ...m, [docId]: as }));
    } catch (e) {
      setError(String(e));
    }
  }

  function toggleExpand(docId: string) {
    if (expandedDocId === docId) {
      setExpandedDocId(null);
      return;
    }
    setExpandedDocId(docId);
    if (!versions[docId] || !approvals[docId]) void loadDetails(docId);
  }

  async function uploadNewVersion(docId: string) {
    const input = document.createElement("input");
    input.type = "file";
    input.onchange = async () => {
      const f = input.files?.[0];
      if (!f) return;
      const form = new FormData();
      form.append("File", f);
      const res = await apiRawFetch(`/api/v1/documents/${docId}/versions`, {
        method: "POST",
        body: form,
      });
      if (!res.ok) {
        setError(`Version upload failed (${res.status})`);
        return;
      }
      await Promise.all([load(), loadDetails(docId)]);
    };
    input.click();
  }

  async function downloadVersion(docId: string, v: DocVersion) {
    const res = await apiRawFetch(`/api/v1/documents/${docId}/versions/${v.id}/download`);
    if (!res.ok) {
      setError(`Download failed (${res.status})`);
      return;
    }
    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = v.originalFileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 2000);
  }

  async function requestApproval(docId: string) {
    const note = prompt("Note for partner / manager reviewer (optional):") ?? undefined;
    try {
      await apiFetch(`/api/v1/documents/${docId}/approvals`, {
        method: "POST",
        body: JSON.stringify({ note }),
      });
      await loadDetails(docId);
    } catch (e) {
      setError(String(e));
    }
  }

  async function decideApproval(docId: string, approvalId: string, decision: "approve" | "reject") {
    const note = prompt(`${decision === "approve" ? "Approval" : "Rejection"} rationale note:`) ?? undefined;
    try {
      await apiFetch(`/api/v1/documents/${docId}/approvals/${approvalId}/${decision}`, {
        method: "POST",
        body: JSON.stringify({ note }),
      });
      await loadDetails(docId);
    } catch (e) {
      setError(String(e));
    }
  }

  async function deleteDoc(doc: DocumentDto) {
    if (!canDelete) return;
    if (!confirm(`Permanently remove "${doc.originalFileName}" from all repository links?`)) return;
    try {
      await apiFetch(`/api/v1/documents/${doc.id}`, { method: "DELETE" });
      await load();
    } catch (e) {
      setError(String(e));
    }
  }

  if (!canRead) return null;

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-700">{title}</h4>
        {docs && (
          <span className="text-[11px] font-medium text-slate-500">
            {docs.length} attached file{docs.length === 1 ? "" : "s"}
          </span>
        )}
      </div>

      {canWrite && (
        <form
          onSubmit={onUpload}
          className="rounded-xl border border-slate-200 bg-white p-3 flex flex-wrap items-end gap-2.5 shadow-2xs"
        >
          <div className="flex-1 min-w-[180px]">
            <label className="block text-[11px] font-semibold text-slate-600 mb-1">
              Select File *
            </label>
            <input
              ref={fileInput}
              type="file"
              required
              className="block w-full text-xs text-slate-500 file:mr-2 file:py-1 file:px-2.5 file:rounded-md file:border-0 file:text-xs file:font-medium file:bg-slate-100 file:text-slate-700 hover:file:bg-slate-200 cursor-pointer"
            />
          </div>

          <div className="w-40">
            <label className="block text-[11px] font-semibold text-slate-600 mb-1">
              Category
            </label>
            <Input
              value={category}
              onChange={(e) => setCategory(e.target.value)}
              placeholder="e.g. Notice, Challan"
              className="py-1 text-xs"
            />
          </div>

          <div className="flex-1 min-w-[180px]">
            <label className="block text-[11px] font-semibold text-slate-600 mb-1">
              Description / Tags
            </label>
            <Input
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Optional notes or audit cross-reference"
              className="py-1 text-xs"
            />
          </div>

          <Button
            type="submit"
            variant="primary"
            size="sm"
            loading={uploading}
          >
            {uploading ? "Uploading…" : "Upload"}
          </Button>
        </form>
      )}

      {error && (
        <div className="rounded-lg bg-red-50 border border-red-200 p-2.5 text-xs text-red-700">
          {error}
        </div>
      )}

      {docs === null ? (
        <div className="py-4">
          <LoadingSpinner text="Fetching attachments…" size="sm" />
        </div>
      ) : docs.length === 0 ? (
        <div className="rounded-xl border border-dashed border-slate-200 bg-slate-50/50 p-6 text-center text-xs text-slate-400 italic">
          No files attached to this record yet.
        </div>
      ) : (
        <div className="divide-y divide-slate-100 rounded-xl border border-slate-200 bg-white overflow-hidden shadow-2xs">
          {docs.map((d) => (
            <div key={d.id} className="p-3 hover:bg-slate-50/50 transition-colors">
              <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-2 text-xs">
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      onClick={() => download(d)}
                      className="font-semibold text-slate-900 hover:text-brand-600 transition-colors text-left truncate"
                    >
                      {d.originalFileName}
                    </button>
                    {d.category && (
                      <Badge variant="primary" size="sm">
                        {d.category}
                      </Badge>
                    )}
                  </div>

                  <div className="text-[11px] text-slate-500 flex items-center gap-2 flex-wrap mt-1">
                    <span className="font-mono">{formatBytes(d.sizeBytes)}</span>
                    <span className="text-slate-300">·</span>
                    <span className="text-slate-400 truncate max-w-[120px]">{d.contentType}</span>
                    {d.uploaderName && (
                      <>
                        <span className="text-slate-300">·</span>
                        <span>by {d.uploaderName}</span>
                      </>
                    )}
                    <span className="text-slate-300">·</span>
                    <span>{new Date(d.uploadedAtUtc).toLocaleDateString()}</span>
                  </div>

                  {d.description && (
                    <p className="text-[11px] text-slate-600 mt-1 italic">{d.description}</p>
                  )}
                </div>

                <div className="flex items-center gap-1.5 shrink-0 self-start">
                  <Button
                    variant="outline"
                    size="xs"
                    onClick={() => toggleExpand(d.id)}
                  >
                    {expandedDocId === d.id ? "Hide History" : "Versions & Review"}
                  </Button>
                  <Button
                    variant="ghost"
                    size="xs"
                    onClick={() => copySignedUrl(d)}
                    title="Generate a 10-minute temporary signed link"
                  >
                    Share
                  </Button>
                  {canWrite && (
                    <Button
                      variant="ghost"
                      size="xs"
                      onClick={() => detach(d)}
                    >
                      Detach
                    </Button>
                  )}
                  {canDelete && (
                    <Button
                      variant="ghost"
                      size="xs"
                      onClick={() => deleteDoc(d)}
                      className="text-rose-600 hover:text-rose-700 hover:bg-rose-50"
                    >
                      Delete
                    </Button>
                  )}
                </div>
              </div>

              {expandedDocId === d.id && (
                <div className="mt-3 pt-3 border-t border-slate-100 grid grid-cols-1 md:grid-cols-2 gap-4 bg-slate-50/50 p-3 rounded-lg animate-fadeIn text-xs">
                  <div>
                    <div className="flex items-center justify-between mb-1.5">
                      <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-600">
                        Version History
                      </span>
                      {canWrite && (
                        <button
                          type="button"
                          onClick={() => uploadNewVersion(d.id)}
                          className="text-[11px] font-semibold text-brand-600 hover:text-brand-800"
                        >
                          + Upload Revision
                        </button>
                      )}
                    </div>
                    <div className="divide-y divide-slate-200/60 rounded border border-slate-200 bg-white text-[11px]">
                      <div className="p-2 flex items-center justify-between">
                        <span className="font-semibold text-slate-800">Current Active</span>
                        <span className="text-slate-400 font-mono">{d.originalFileName}</span>
                      </div>
                      {(versions[d.id] ?? []).map((v) => (
                        <div key={v.id} className="p-2 flex items-center justify-between">
                          <button
                            type="button"
                            onClick={() => downloadVersion(d.id, v)}
                            className="text-brand-700 hover:underline font-medium text-left"
                          >
                            v{v.versionNumber} · {v.originalFileName}
                          </button>
                          <span className="text-slate-400 font-mono text-[10px]">
                            {new Date(v.uploadedAtUtc).toLocaleDateString()}
                          </span>
                        </div>
                      ))}
                      {(versions[d.id] ?? []).length === 0 && (
                        <div className="p-2 text-slate-400 italic">No prior versions recorded.</div>
                      )}
                    </div>
                  </div>

                  <div>
                    <div className="flex items-center justify-between mb-1.5">
                      <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-600">
                        Approval & Sign-off State
                      </span>
                      {canWrite && (
                        <button
                          type="button"
                          onClick={() => requestApproval(d.id)}
                          className="text-[11px] font-semibold text-brand-600 hover:text-brand-800"
                        >
                          + Request Review
                        </button>
                      )}
                    </div>
                    <div className="divide-y divide-slate-200/60 rounded border border-slate-200 bg-white text-[11px]">
                      {(approvals[d.id] ?? []).map((a) => (
                        <div key={a.id} className="p-2 flex items-center justify-between gap-2">
                          <div className="min-w-0 flex-1">
                            <Badge
                              tone={a.status === 2 ? "success" : a.status === 3 ? "danger" : "warning"}
                              size="sm"
                            >
                              {a.status === 1 ? "Pending" : a.status === 2 ? "Approved" : "Rejected"}
                            </Badge>
                            <span className="text-slate-600 ml-1.5">
                              by {a.requesterName ?? "Staff"}
                            </span>
                          </div>
                          {a.status === 1 && canApprove && (
                            <div className="flex items-center gap-1">
                              <button
                                type="button"
                                onClick={() => decideApproval(d.id, a.id, "approve")}
                                className="px-1.5 py-0.5 rounded text-[10px] font-semibold text-emerald-800 bg-emerald-50 border border-emerald-200 hover:bg-emerald-100"
                              >
                                Approve
                              </button>
                              <button
                                type="button"
                                onClick={() => decideApproval(d.id, a.id, "reject")}
                                className="px-1.5 py-0.5 rounded text-[10px] font-semibold text-rose-800 bg-rose-50 border border-rose-200 hover:bg-rose-100"
                              >
                                Reject
                              </button>
                            </div>
                          )}
                        </div>
                      ))}
                      {(approvals[d.id] ?? []).length === 0 && (
                        <div className="p-2 text-slate-400 italic">No approval requests logged.</div>
                      )}
                    </div>
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

interface DocVersion {
  id: string;
  versionNumber: number;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  sha256: string;
  uploaderName: string | null;
  uploadedAtUtc: string;
}

interface DocApproval {
  id: string;
  documentId: string;
  status: number;
  requesterUserId: string;
  requesterName: string | null;
  requesterNote: string | null;
  approverUserId: string | null;
  approverName: string | null;
  decidedAtUtc: string | null;
  decisionNote: string | null;
  createdAtUtc: string;
}

function formatBytes(n: number): string {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`;
  if (n < 1024 * 1024 * 1024) return `${(n / 1024 / 1024).toFixed(1)} MB`;
  return `${(n / 1024 / 1024 / 1024).toFixed(2)} GB`;
}
