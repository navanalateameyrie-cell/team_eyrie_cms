import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import {
  importsApi,
  type BatchDto,
  type ImportBatchStatus,
  type ImportDecision,
  type ReconciliationDto,
  type StagingClientDto,
  type StagingServiceFileDto,
} from "../../api/imports";
import {
  PageHeader,
  Card,
  CardHeader,
  MetricCard,
  Button,
  Badge,
  LoadingSpinner,
} from "../../components/ui";

const statusTone: Record<ImportBatchStatus, "neutral" | "brand" | "warning" | "success" | "danger"> = {
  Received: "neutral",
  Parsed: "brand",
  Validated: "warning",
  Committed: "success",
  Failed: "danger",
  Cancelled: "neutral",
};

const decisionTone: Record<ImportDecision, "neutral" | "success" | "brand" | "warning"> = {
  Pending: "neutral",
  CreateNew: "success",
  UpdateExisting: "brand",
  Skip: "warning",
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

function getDecisionName(decision: ImportDecision | number | string): ImportDecision {
  if (typeof decision === "string") {
    const d = decision.trim();
    if (d === "0") return "Pending";
    if (d === "1") return "CreateNew";
    if (d === "2") return "UpdateExisting";
    if (d === "3") return "Skip";
    return decision as ImportDecision;
  }
  switch (decision) {
    case 0: return "Pending";
    case 1: return "CreateNew";
    case 2: return "UpdateExisting";
    case 3: return "Skip";
    default: return "Pending";
  }
}

export default function ImportBatchPage() {
  const { id } = useParams<{ id: string }>();
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("imports.write");

  const [batch, setBatch] = useState<BatchDto | null>(null);
  const [rows, setRows] = useState<StagingClientDto[]>([]);
  const [sfRows, setSfRows] = useState<StagingServiceFileDto[]>([]);
  const [reco, setReco] = useState<ReconciliationDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!id) return;
    try {
      const [b, r, sf, rc] = await Promise.all([
        importsApi.get(id),
        importsApi.rows(id),
        importsApi.serviceFileRows(id),
        importsApi.reconciliation(id),
      ]);
      setBatch(b);
      setRows(r);
      setSfRows(sf);
      setReco(rc);
    } catch (ex: unknown) {
      setError(ex instanceof Error ? ex.message : "Failed to load import batch details");
    }
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  async function act(fn: () => Promise<unknown>, label: string) {
    setBusy(true);
    setError(null);
    try {
      await fn();
      await load();
    } catch (ex: unknown) {
      setError(`${label} failed: ${ex instanceof Error ? ex.message : "unknown error"}`);
    } finally {
      setBusy(false);
    }
  }

  if (!batch) {
    return (
      <div className="py-16 flex justify-center">
        <LoadingSpinner text="Loading import staging batch…" size="lg" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        breadcrumbs={[
          { label: "Imports", to: "/admin/imports" },
          { label: batch.fileName },
        ]}
        title={batch.fileName}
        subtitle={`Sheet: ${batch.sourceSheet ?? "Default"} · SHA-256: ${batch.fileSha256.slice(0, 24)}…`}
        badge={
          <Badge tone={statusTone[getStatusName(batch.status)]} size="md">
            {getStatusName(batch.status)}
          </Badge>
        }
        actions={
          canWrite ? (
            <div className="flex items-center gap-2">
              {getStatusName(batch.status) === "Parsed" && (
                <Button
                  variant="primary"
                  size="sm"
                  loading={busy}
                  onClick={() => act(() => importsApi.validate(batch.id), "Validate")}
                >
                  Validate Staging Rows
                </Button>
              )}
              {getStatusName(batch.status) === "Validated" && (
                <>
                  <Button
                    variant="outline"
                    size="sm"
                    loading={busy}
                    onClick={() => act(() => importsApi.autoResolve(batch.id), "Auto-resolve")}
                  >
                    Auto-Resolve Matches
                  </Button>
                  <Button
                    variant="primary"
                    size="sm"
                    loading={busy}
                    onClick={() => {
                      if (confirm(`Commit ${batch.rowCount} staging rows into Master Practice Records?`)) {
                        void act(() => importsApi.commit(batch.id), "Commit");
                      }
                    }}
                  >
                    Commit to Master
                  </Button>
                </>
              )}
            </div>
          ) : undefined
        }
      />

      {error && (
        <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-xs text-red-700 flex items-center gap-2">
          <svg className="w-4 h-4 shrink-0 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
          </svg>
          <span>{error}</span>
        </div>
      )}

      {/* Import Ingestion Workflow Status Banner */}
      {getStatusName(batch.status) === "Parsed" && (
        <div className="rounded-xl bg-navy-800 border border-navy-700 text-white p-4 shadow-md flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="flex items-start gap-3">
            <div className="h-9 w-9 rounded-lg bg-gold-500/20 text-gold-400 border border-gold-500/30 flex items-center justify-center shrink-0">
              <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
              </svg>
            </div>
            <div>
              <h4 className="font-semibold text-sm text-white">Staging Ingestion Ready for Normalization</h4>
              <p className="text-xs text-slate-300 mt-0.5">
                {batch.rowCount} raw rows parsed from the worksheet. Run validation to extract and normalize Legal Names, CBDT-compliant PANs, and detect matches against existing clients.
              </p>
            </div>
          </div>
          {canWrite && (
            <Button
              variant="gold"
              size="sm"
              loading={busy}
              onClick={() => act(() => importsApi.validate(batch.id), "Validate")}
              className="shrink-0 shadow font-semibold"
            >
              Validate & Normalize Rows ({batch.rowCount})
            </Button>
          )}
        </div>
      )}

      {getStatusName(batch.status) === "Validated" && (
        <div className="rounded-xl bg-emerald-900/40 border border-emerald-700/60 text-white p-4 shadow-sm flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="flex items-start gap-3">
            <div className="h-9 w-9 rounded-lg bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 flex items-center justify-center shrink-0">
              <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </div>
            <div>
              <h4 className="font-semibold text-sm text-white">Validation & Deduplication Complete</h4>
              <p className="text-xs text-slate-300 mt-0.5">
                Review matched entities below. You can auto-resolve exact PAN matches or commit verified clients directly into Practice Master records.
              </p>
            </div>
          </div>
          {canWrite && (
            <div className="flex items-center gap-2 shrink-0">
              <Button
                variant="outline"
                size="sm"
                loading={busy}
                onClick={() => act(() => importsApi.autoResolve(batch.id), "Auto-resolve")}
                className="bg-white/10 text-white border-white/20 hover:bg-white/20"
              >
                Auto-Resolve Matches
              </Button>
              <Button
                variant="primary"
                size="sm"
                loading={busy}
                onClick={() => {
                  if (confirm(`Commit ${batch.rowCount} staging rows into Master Practice Records?`)) {
                    void act(() => importsApi.commit(batch.id), "Commit");
                  }
                }}
              >
                Commit to Master
              </Button>
            </div>
          )}
        </div>
      )}

      {getStatusName(batch.status) === "Committed" && (
        <div className="rounded-xl bg-slate-900 border border-slate-700 text-white p-4 shadow-sm flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="h-8 w-8 rounded-lg bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 flex items-center justify-center shrink-0">
              ✓
            </div>
            <div>
              <h4 className="font-semibold text-sm text-white">Batch Committed to Master Records</h4>
              <p className="text-xs text-slate-300">
                Created {batch.committedClientCount} clients and {batch.committedServiceFileCount} recurring service files.
              </p>
            </div>
          </div>
          <Link
            to="/admin/clients"
            className="text-xs font-medium text-gold-400 hover:text-gold-300 underline"
          >
            Open Clients Directory →
          </Link>
        </div>
      )}

      {reco && (
        <div className="grid grid-cols-2 md:grid-cols-6 gap-3">
          <MetricCard label="Total Ingested" value={reco.batch.rowCount} hint="Rows in sheet" />
          <MetricCard label="Pending Review" value={reco.pending} tone="warning" hint="Awaiting decision" />
          <MetricCard label="Create New" value={reco.createNew} tone="success" hint="New master entities" />
          <MetricCard label="Update Existing" value={reco.updateExisting} tone="brand" hint="Matched records" />
          <MetricCard label="Skipped" value={reco.skip} hint="Bypassed rows" />
          <MetricCard
            label="Validation Errors"
            value={reco.clientRowsWithErrors}
            tone={reco.clientRowsWithErrors > 0 ? "danger" : "default"}
            hint={reco.clientRowsWithErrors > 0 ? "Requires resolution" : "Clean staging"}
          />
        </div>
      )}

      {/* Client Rows Staging Table */}
      <Card>
        <CardHeader
          title="Staging Client Entity Rows"
          subtitle="Preview of parsed legal names, PAN duplicates, and commit decisions"
        />
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50 font-medium text-slate-500">
              <tr>
                <th className="px-3 py-3 text-left w-12">Row</th>
                <th className="px-3 py-3 text-left">Normalized Legal Name</th>
                <th className="px-3 py-3 text-left">PAN</th>
                <th className="px-3 py-3 text-left">Legacy ID</th>
                <th className="px-3 py-3 text-left">Entity Type</th>
                <th className="px-3 py-3 text-left">Ingestion Decision</th>
                <th className="px-3 py-3 text-left">Validation Warnings</th>
                <th className="px-3 py-3 text-left">Master Link</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {rows.map((r) => {
                const dec = getDecisionName(r.decision);
                return (
                  <tr key={r.id} className="hover:bg-slate-50/50">
                    <td className="px-3 py-2 font-mono text-slate-500">{r.rowNumber}</td>
                    <td className="px-3 py-2 font-medium text-slate-900">
                      {r.normalizedLegalName ? (
                        r.normalizedLegalName
                      ) : getStatusName(batch.status) === "Parsed" ? (
                        <span className="text-slate-400 italic text-[11px]">Pending validation</span>
                      ) : (
                        "—"
                      )}
                    </td>
                    <td className="px-3 py-2 font-mono text-slate-700">
                      {r.normalizedPan ?? "—"}
                    </td>
                    <td className="px-3 py-2 font-mono text-slate-600">
                      {r.normalizedLegacyClientId ?? "—"}
                    </td>
                    <td className="px-3 py-2 text-slate-700">
                      {r.entityTypeCode ?? "—"}
                    </td>
                    <td className="px-3 py-2 whitespace-nowrap">
                      <Badge tone={decisionTone[dec]} size="sm">
                        {dec}
                      </Badge>
                    </td>
                    <td className="px-3 py-2 text-rose-600 max-w-xs truncate font-mono text-[11px]">
                      {r.validationErrorsJson && r.validationErrorsJson !== "[]"
                        ? r.validationErrorsJson
                        : "—"}
                    </td>
                    <td className="px-3 py-2 whitespace-nowrap">
                      {r.committedClientId ? (
                        <Link
                          to={`/admin/clients/${r.committedClientId}`}
                          className="font-mono text-xs text-brand-600 hover:underline"
                        >
                          View Client
                        </Link>
                      ) : (
                        <span className="text-slate-400">—</span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </Card>

      {/* Service File Rows Staging Table */}
      {sfRows.length > 0 && (
        <Card>
          <CardHeader
            title="Service File Rows (Final Engagement Sheet)"
            subtitle="Engagements, recurring statutory service definitions, and legacy file numbers"
          />
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-slate-200 text-xs">
              <thead className="bg-slate-50 font-medium text-slate-500">
                <tr>
                  <th className="px-3 py-3 text-left w-12">Row</th>
                  <th className="px-3 py-3 text-left">Service Code</th>
                  <th className="px-3 py-3 text-left">Legacy File Ref</th>
                  <th className="px-3 py-3 text-left">Decision</th>
                  <th className="px-3 py-3 text-left">Errors</th>
                  <th className="px-3 py-3 text-left">Committed</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 bg-white">
                {sfRows.map((r) => {
                  const dec = getDecisionName(r.decision);
                  return (
                    <tr key={r.id} className="hover:bg-slate-50/50">
                      <td className="px-3 py-2 font-mono text-slate-500">{r.rowNumber}</td>
                      <td className="px-3 py-2 font-mono font-semibold text-slate-800">
                        {r.serviceCode}
                      </td>
                      <td className="px-3 py-2 font-mono text-slate-700">
                        {r.legacyFileNumber ?? "—"}
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap">
                        <Badge tone={decisionTone[dec]} size="sm">
                          {dec}
                        </Badge>
                      </td>
                      <td className="px-3 py-2 text-rose-600 max-w-xs truncate font-mono text-[11px]">
                        {r.validationErrorsJson && r.validationErrorsJson !== "[]"
                          ? r.validationErrorsJson
                          : "—"}
                      </td>
                      <td className="px-3 py-2 text-slate-700">
                        {r.committedServiceFileId ? (
                          <span className="text-emerald-700 font-medium">Committed</span>
                        ) : (
                          "Pending"
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </Card>
      )}
    </div>
  );
}
