import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api, apiFetch, type HealthResponse } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { Card, CardHeader, CardContent, Button, Badge } from "../components/ui";
import { formatInr } from "../utils/formatters";

interface ComplianceAgg {
  dueThisWeek: number;
  overdueOpen: number;
  filedThisMonth: number;
  inProgress: number;
}
interface BillingAgg {
  outstandingInr: number;
  overdueInr: number;
  revenueThisFyInr: number;
}

export default function HomePage() {
  const { userName, roles, permissions, token, hasPermission } = useAuth();
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [healthError, setHealthError] = useState(false);
  const [compliance, setCompliance] = useState<ComplianceAgg | null>(null);
  const [billing, setBilling] = useState<BillingAgg | null>(null);

  useEffect(() => {
    api.health().then(setHealth).catch(() => setHealthError(true));
    if (token && hasPermission("compliance.read")) {
      apiFetch<ComplianceAgg>("/api/v1/compliance/aggregates").then(setCompliance).catch(() => void 0);
    }
    if (token && hasPermission("billing.read")) {
      apiFetch<BillingAgg>("/api/v1/billing/invoices/aggregates").then(setBilling).catch(() => void 0);
    }
  }, [token, hasPermission]);

  return (
    <div className="space-y-6">
      {/* Enterprise Hero Banner */}
      <section className="relative overflow-hidden rounded-2xl bg-gradient-to-r from-navy-950 via-navy-900 to-navy-800 text-white shadow-xl border border-navy-700/50">
        <div className="absolute inset-y-0 right-0 w-1/3 bg-gradient-to-l from-brand-500/10 to-transparent pointer-events-none" />
        <div className="relative px-6 py-6 sm:px-8 sm:py-8">
          <div className="inline-flex items-center gap-2 rounded-full bg-brand-500/15 border border-brand-500/30 px-3 py-1 text-xs font-semibold text-brand-400 uppercase tracking-widest mb-3">
            <span>Chartered Accountants</span> · <span>Practice Suite</span>
          </div>
          <h1 className="text-2xl sm:text-3xl md:text-4xl font-bold leading-tight tracking-tight text-white">
            Client Practice Management, Compliance & Billing
          </h1>
          <p className="mt-3 max-w-3xl text-slate-300 text-xs sm:text-sm leading-relaxed">
            Role-based governance across clients, branch networks, Indian tax registrations (GSTIN/TAN/ESIC),
            multi-period engagements, staff allocations, and automated GST e-invoicing with an append-only audit trail.
          </p>

          <div className="mt-6 flex flex-wrap gap-3">
            {token ? (
              <>
                <Link to="/dashboard">
                  <Button variant="primary" size="md">
                    Open Dashboard →
                  </Button>
                </Link>
                {hasPermission("clients.read") && (
                  <Link to="/admin/clients">
                    <Button
                      variant="secondary"
                      size="md"
                      className="border border-navy-600 bg-navy-800/90 text-slate-100 hover:bg-navy-700 hover:text-white"
                    >
                      Browse Clients
                    </Button>
                  </Link>
                )}
              </>
            ) : (
              <Link to="/login">
                <Button variant="primary" size="md">
                  Sign In to Practice Console →
                </Button>
              </Link>
            )}
          </div>
        </div>
      </section>

      {/* Quick Access Feature Cards */}
      <section className="grid grid-cols-1 md:grid-cols-3 gap-5">
        <FeatureCard
          eyebrow="CLIENT ONBOARDING"
          title="Clients & Multi-Branch Structure"
          body="8-digit permanent client codes, primary branch management, address verification, and GSTIN/TAN registrations."
          to="/admin/clients"
          enabled={hasPermission("clients.read")}
        />
        <FeatureCard
          eyebrow="SERVICE LIFECYCLE"
          title="Service Files & Engagements"
          body="Manage annual recurring engagements (Q1–Q4, H1–H2), dynamic checklists, and append-only staff assignments."
          to="/admin/clients"
          enabled={hasPermission("clients.read")}
        />
        <FeatureCard
          eyebrow="BULK DATA INGESTION"
          title="Excel Data Import Pipeline"
          body="SHA-256 deduplication, staging validation, PAN matching, and one-click atomic database commits."
          to="/admin/imports"
          enabled={hasPermission("imports.read")}
        />
      </section>

      {/* Compliance Overview */}
      {compliance && (
        <Card>
          <CardHeader
            title="Compliance at a glance"
            subtitle="Upcoming filing deadlines & statutory obligations"
            action={
              <Link to="/admin/compliance" className="text-xs font-semibold text-brand-600 hover:text-brand-700">
                Open compliance calendar →
              </Link>
            }
          />
          <div className="grid grid-cols-2 md:grid-cols-4 divide-y md:divide-y-0 md:divide-x divide-slate-100">
            <StatBlock label="Due this week" value={compliance.dueThisWeek} tone="text-amber-800" />
            <StatBlock label="Overdue" value={compliance.overdueOpen} tone="text-rose-700 font-bold" />
            <StatBlock label="In progress" value={compliance.inProgress} tone="text-sky-700" />
            <StatBlock label="Filed this month" value={compliance.filedThisMonth} tone="text-emerald-700" />
          </div>
        </Card>
      )}

      {/* Billing Overview */}
      {billing && (
        <Card>
          <CardHeader
            title="Billing at a glance"
            subtitle="Practice receivables, outstanding ledger balances & revenue"
            action={
              <Link to="/admin/billing" className="text-xs font-semibold text-brand-600 hover:text-brand-700">
                Open billing ledger →
              </Link>
            }
          />
          <div className="grid grid-cols-1 md:grid-cols-3 divide-y md:divide-y-0 md:divide-x divide-slate-100">
            <StatBlock label="Outstanding" value={formatInr(billing.outstandingInr, 0)} tone="text-amber-800" />
            <StatBlock label="Overdue" value={formatInr(billing.overdueInr, 0)} tone="text-rose-700 font-bold" />
            <StatBlock label="Revenue this FY" value={formatInr(billing.revenueThisFyInr, 0)} tone="text-emerald-700 font-bold" />
          </div>
        </Card>
      )}

      {/* Session Details & API Health */}
      <section className="grid grid-cols-1 md:grid-cols-2 gap-5">
        <Card>
          <CardHeader
            title="Your session"
            subtitle="Active identity and permission scopes"
            action={token ? <Badge tone="success" dot>Signed in</Badge> : <Badge tone="neutral">Guest</Badge>}
          />
          <CardContent>
            {token ? (
              <dl className="grid grid-cols-3 gap-y-2.5 text-xs">
                <dt className="text-slate-500 font-medium">User</dt>
                <dd className="col-span-2 font-mono text-slate-800 font-semibold break-all">{userName}</dd>
                <dt className="text-slate-500 font-medium">Roles</dt>
                <dd className="col-span-2">
                  <div className="flex flex-wrap gap-1">
                    {roles.length > 0 ? (
                      roles.map((r) => (
                        <span key={r} className="rounded bg-slate-100 text-slate-700 px-1.5 py-0.5 text-[10px] font-mono">
                          {r}
                        </span>
                      ))
                    ) : (
                      <span className="text-slate-400">—</span>
                    )}
                  </div>
                </dd>
                <dt className="text-slate-500 font-medium">Permissions</dt>
                <dd className="col-span-2 font-mono text-[11px] text-slate-500 leading-relaxed max-h-24 overflow-y-auto">
                  {permissions.join(", ") || "—"}
                </dd>
              </dl>
            ) : (
              <p className="text-slate-500 text-xs">Not signed in. Use the Login link above.</p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader
            title="API health"
            subtitle="Real-time backend service connectivity"
            action={
              health ? (
                <Badge tone="success" dot>{health.status}</Badge>
              ) : healthError ? (
                <Badge tone="danger" dot>unreachable</Badge>
              ) : null
            }
          />
          <CardContent>
            {health ? (
              <dl className="grid grid-cols-3 gap-y-2.5 text-xs">
                <dt className="text-slate-500 font-medium">Service</dt>
                <dd className="col-span-2 font-mono text-slate-800 font-semibold">{health.service}</dd>
                <dt className="text-slate-500 font-medium">Version</dt>
                <dd className="col-span-2 font-mono text-slate-800">{health.version}</dd>
                <dt className="text-slate-500 font-medium">UTC</dt>
                <dd className="col-span-2 font-mono text-slate-600">{health.utc}</dd>
              </dl>
            ) : healthError ? (
              <p className="text-xs text-rose-600 font-medium">Could not reach the API.</p>
            ) : (
              <p className="text-slate-500 text-xs">Checking…</p>
            )}
          </CardContent>
        </Card>
      </section>
    </div>
  );
}

function StatBlock({ label, value, tone }: { label: string; value: number | string; tone: string }) {
  return (
    <div className="p-4 sm:p-5 text-center">
      <div className={`text-xl sm:text-2xl font-mono ${tone}`}>{value}</div>
      <div className="text-xs text-slate-500 mt-1 font-medium">{label}</div>
    </div>
  );
}

function FeatureCard({
  eyebrow,
  title,
  body,
  to,
  enabled,
}: {
  eyebrow: string;
  title: string;
  body: string;
  to: string;
  enabled: boolean;
}) {
  const cardContent = (
    <div className="h-full rounded-xl border border-slate-200/90 bg-white p-5 shadow-card hover:shadow-card-hover transition-all border-l-4 border-l-brand-500 flex flex-col justify-between">
      <div>
        <p className="text-brand-600 text-[10px] uppercase tracking-wider font-bold">{eyebrow}</p>
        <h3 className="mt-1 text-sm font-semibold text-slate-900">{title}</h3>
        <p className="mt-1.5 text-xs text-slate-600 leading-relaxed">{body}</p>
      </div>
      {enabled && (
        <span className="mt-4 inline-flex items-center text-brand-600 text-xs font-semibold hover:text-brand-700">
          Open Module →
        </span>
      )}
    </div>
  );

  return enabled ? <Link to={to} className="block">{cardContent}</Link> : <div className="opacity-60">{cardContent}</div>;
}
