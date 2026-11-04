import { useEffect, useState, useCallback } from "react";
import { Link } from "react-router-dom";
import { dashboardApi, type Breakdown, type Kpis } from "../api/dashboard";
import { HorizontalBarChart, DonutChart, RankedBarChart } from "../components/charts";
import { PageHeader, MetricCard, Button, LoadingSpinner, ErrorState } from "../components/ui";

export default function DashboardPage() {
  const [kpis, setKpis] = useState<Kpis | null>(null);
  const [byEntity, setByEntity] = useState<Breakdown[]>([]);
  const [byState, setByState] = useState<Breakdown[]>([]);
  const [byService, setByService] = useState<Breakdown[]>([]);
  const [byStaff, setByStaff] = useState<Breakdown[] | null>([]);
  const [staffForbidden, setStaffForbidden] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [k, e, s, sv, st] = await Promise.all([
        dashboardApi.kpis(),
        dashboardApi.clientsByEntityType(),
        dashboardApi.clientsByState(),
        dashboardApi.serviceFilesByService(),
        dashboardApi.staffWorkloadTop(10),
      ]);
      setKpis(k);
      setByEntity(e);
      setByState(s);
      setByService(sv);
      if (st === null) {
        setStaffForbidden(true);
        setByStaff(null);
      } else {
        setStaffForbidden(false);
        setByStaff(st);
      }
    } catch (ex: unknown) {
      setError(ex instanceof Error ? ex.message : "Failed to load dashboard metrics.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  if (loading) {
    return (
      <div className="py-24 flex justify-center">
        <LoadingSpinner size="lg" label="Loading practice dashboard metrics…" />
      </div>
    );
  }

  if (error) {
    return <ErrorState title="Failed to load dashboard" message={error} onRetry={loadData} />;
  }

  return (
    <div className="space-y-6">
      {/* Page Header with Quick Action Buttons */}
      <PageHeader
        title="Dashboard"
        description="Real-time practice metrics, client distributions, service files and staff capacity."
        actions={
          <div className="flex flex-wrap items-center gap-2.5">
            <button
              type="button"
              onClick={() => void loadData()}
              title="Refresh live metrics"
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-slate-300 bg-white hover:bg-slate-50 text-slate-700 text-xs font-semibold shadow-2xs transition-colors"
            >
              <svg className="w-3.5 h-3.5 text-slate-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
              <span>Refresh</span>
            </button>
            <Link to="/admin/billing">
              <Button variant="outline" size="sm">
                Invoices & Billing
              </Button>
            </Link>
            <Link to="/admin/clients/new">
              <Button variant="primary" size="sm">
                + Onboard Client
              </Button>
            </Link>
          </div>
        }
      />

      {/* KPI Metric Cards */}
      {kpis && (
        <section className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-3.5 sm:gap-4">
          <MetricCard
            label="Active Clients"
            value={kpis.activeClients}
            subtext={`${kpis.inactiveClients.toLocaleString()} inactive clients`}
            tone="brand"
            icon={
              <svg className="w-4 h-4 text-navy-800" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
              </svg>
            }
          />
          <MetricCard
            label="Open Service Files"
            value={kpis.activeServiceFiles}
            subtext={`${kpis.onHoldServiceFiles.toLocaleString()} on hold · ${kpis.closedServiceFiles.toLocaleString()} closed`}
            tone="info"
            icon={
              <svg className="w-4 h-4 text-sky-700" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
            }
          />
          <MetricCard
            label="Engagements in Progress"
            value={kpis.inProgressEngagements}
            subtext={`${kpis.openEngagements.toLocaleString()} planned · ${kpis.completedEngagements.toLocaleString()} completed`}
            tone="warning"
            icon={
              <svg className="w-4 h-4 text-amber-700" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            }
          />
          <MetricCard
            label="Active Branches"
            value={kpis.activeBranches}
            subtext="Registered practice locations"
            tone="purple"
            icon={
              <svg className="w-4 h-4 text-purple-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
              </svg>
            }
          />
          <MetricCard
            label="Active Staff"
            value={kpis.activeStaff}
            subtext="Partners, managers & associates"
            tone="success"
            icon={
              <svg className="w-4 h-4 text-emerald-700" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5.121 17.804A13.937 13.937 0 0112 16c2.5 0 4.847.655 6.879 1.804M15 10a3 3 0 11-6 0 3 3 0 016 0zm6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            }
          />
        </section>
      )}

      {/* Visual Analytics Grid */}
      <section className="grid grid-cols-1 lg:grid-cols-2 gap-5 sm:gap-6 items-stretch">
        {/* 1. Clients by Entity Type */}
        <HorizontalBarChart
          title="Clients by Entity Type"
          subtitle="Corporate, LLP, Partnership, Individual & Trust breakdown"
          rows={byEntity}
          totalLabel="Clients"
          maxInitialRows={6}
          emptyTitle="No Client Entity Data"
          emptyDescription="Active clients categorized by statutory legal structure will appear here."
        />

        {/* 2. Clients by State (Place of Supply) */}
        <DonutChart
          title="Clients by State (Place of Supply)"
          subtitle="Distribution of primary branch locations across India"
          rows={byState}
          centerLabel="Clients"
          maxLegendItems={5}
          emptyTitle="No State of Supply Data"
          emptyDescription="No primary branch locations registered yet. Primary branches added to client profiles will show state-wise place of supply distribution here."
        />

        {/* 3. Open Service Files by Service */}
        <HorizontalBarChart
          title="Open Service Files by Service"
          subtitle="Income tax, GST, statutory audit, ROC and advisory files"
          rows={byService}
          totalLabel="Files"
          maxInitialRows={6}
          emptyTitle="No Open Service Files"
          emptyDescription="Active client service files categorized by service type will appear here."
        />

        {/* 4. Top Staff by Active Assignments */}
        <RankedBarChart
          title="Top Staff by Active Assignments"
          subtitle="Current active case assignments per team member"
          rows={byStaff ?? []}
          isRestricted={staffForbidden}
          emptyTitle="No Active Staff Assignments"
          emptyDescription="There are currently no active case assignments delegated to team members. As service files and engagements are assigned to staff, team capacity distribution will appear here."
        />
      </section>
    </div>
  );
}
