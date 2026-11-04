import { useEffect, useState, useCallback, useRef } from "react";
import { Link, useNavigate } from "react-router-dom";
import { dashboardApi, type Breakdown, type Kpis } from "../api/dashboard";
import { HorizontalBarChart, DonutChart, RankedBarChart } from "../components/charts";
import { MetricCard, LoadingSpinner, ErrorState } from "../components/ui";

export default function DashboardPage() {
  const navigate = useNavigate();
  const [kpis, setKpis] = useState<Kpis | null>(null);
  const [byEntity, setByEntity] = useState<Breakdown[]>([]);
  const [byState, setByState] = useState<Breakdown[]>([]);
  const [byService, setByService] = useState<Breakdown[]>([]);
  const [byStaff, setByStaff] = useState<Breakdown[] | null>([]);
  const [staffForbidden, setStaffForbidden] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  // Dashboard Controls
  const [financialYear, setFinancialYear] = useState("FY 2024-25");
  const [quickActionsOpen, setQuickActionsOpen] = useState(false);
  const quickActionsRef = useRef<HTMLDivElement>(null);

  // Close quick actions dropdown on outside click
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (quickActionsRef.current && !quickActionsRef.current.contains(event.target as Node)) {
        setQuickActionsOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

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
      {/* Dashboard Top Header Section */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-[#E3E8F0]/80">
        <div className="flex items-center gap-3">
          <div className="h-10 w-10 rounded-xl bg-[#102747] text-[#C49A52] border border-[#C49A52]/30 flex items-center justify-center shadow-xs shrink-0">
            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
            </svg>
          </div>
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-[#172A45] leading-tight">Dashboard</h1>
            <p className="text-xs text-[#728096] mt-0.5">Your practice performance at a glance.</p>
          </div>
        </div>

        {/* Right-side Actions: FY selector, Refresh, Quick Actions Dropdown */}
        <div className="flex flex-wrap items-center gap-2.5">
          {/* Financial Year Selector */}
          <div className="relative">
            <select
              value={financialYear}
              onChange={(e) => setFinancialYear(e.target.value)}
              className="appearance-none bg-white border border-[#E3E8F0] hover:border-slate-300 text-xs font-semibold text-[#172A45] py-2 pl-3 pr-8 rounded-lg shadow-2xs focus:outline-none focus:ring-1 focus:ring-[#C49A52] transition-colors cursor-pointer font-mono"
            >
              <option value="FY 2024-25">FY 2024-25</option>
              <option value="FY 2023-24">FY 2023-24</option>
              <option value="FY 2025-26">FY 2025-26</option>
            </select>
            <span className="pointer-events-none absolute right-2.5 top-2.5 text-[10px] text-slate-400">▾</span>
          </div>

          {/* Refresh live metrics button */}
          <button
            type="button"
            onClick={() => void loadData()}
            title="Refresh practice data"
            className="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg border border-[#E3E8F0] bg-white hover:bg-slate-50 text-[#172A45] text-xs font-semibold shadow-2xs transition-colors"
          >
            <svg className="w-3.5 h-3.5 text-slate-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            <span>Refresh</span>
          </button>

          {/* Quick Actions Dropdown with Gold Background */}
          <div className="relative" ref={quickActionsRef}>
            <button
              type="button"
              onClick={() => setQuickActionsOpen(!quickActionsOpen)}
              className="inline-flex items-center gap-2 px-3.5 py-2 rounded-lg bg-gradient-to-r from-[#C49A52] to-[#B08642] hover:brightness-105 text-white text-xs font-semibold shadow-sm transition-all"
            >
              <svg className="w-3.5 h-3.5 stroke-[2.5]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
              </svg>
              <span>Quick Actions</span>
              <span className="text-[10px] opacity-80">▾</span>
            </button>

            {quickActionsOpen && (
              <div className="absolute right-0 mt-2 w-52 rounded-xl border border-[#E3E8F0] bg-white shadow-xl z-50 py-1 divide-y divide-slate-100 animate-in fade-in duration-150">
                <div className="py-1">
                  <Link
                    to="/admin/clients/new"
                    onClick={() => setQuickActionsOpen(false)}
                    className="flex items-center gap-2.5 px-3.5 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50 hover:text-[#172A45]"
                  >
                    <span className="text-[#C49A52] font-bold text-sm">+</span>
                    <span>Onboard Client</span>
                  </Link>
                  <Link
                    to="/admin/billing"
                    onClick={() => setQuickActionsOpen(false)}
                    className="flex items-center gap-2.5 px-3.5 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50 hover:text-[#172A45]"
                  >
                    <span className="text-[#328CE0] font-bold text-sm">₹</span>
                    <span>Create Invoice</span>
                  </Link>
                </div>
                <div className="py-1">
                  <Link
                    to="/admin/compliance"
                    onClick={() => setQuickActionsOpen(false)}
                    className="flex items-center gap-2.5 px-3.5 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50 hover:text-[#172A45]"
                  >
                    <span className="text-[#D49A43]">📅</span>
                    <span>Compliance Calendar</span>
                  </Link>
                  <Link
                    to="/admin/imports"
                    onClick={() => setQuickActionsOpen(false)}
                    className="flex items-center gap-2.5 px-3.5 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50 hover:text-[#172A45]"
                  >
                    <span className="text-emerald-600">📥</span>
                    <span>Data Imports</span>
                  </Link>
                  <Link
                    to="/admin/timesheet"
                    onClick={() => setQuickActionsOpen(false)}
                    className="flex items-center gap-2.5 px-3.5 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50 hover:text-[#172A45]"
                  >
                    <span className="text-purple-600">⏱️</span>
                    <span>Staff Timesheet</span>
                  </Link>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* 5 KPI Metric Cards Row */}
      {kpis && (
        <section className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-3.5 sm:gap-4">
          <MetricCard
            label="Active Clients"
            value={kpis.activeClients}
            subtext={`${kpis.inactiveClients.toLocaleString()} inactive clients`}
            tone="navy"
            onClick={() => navigate("/admin/clients")}
            icon={
              <svg className="w-4 h-4 text-[#C49A52]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
              </svg>
            }
          />
          <MetricCard
            label="Open Service Files"
            value={kpis.activeServiceFiles}
            subtext={`${kpis.onHoldServiceFiles.toLocaleString()} on hold · ${kpis.closedServiceFiles.toLocaleString()} closed`}
            tone="info"
            onClick={() => navigate("/admin/clients")}
            icon={
              <svg className="w-4 h-4 text-[#328CE0]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
            }
          />
          <MetricCard
            label="Upcoming Compliance"
            value={kpis.inProgressEngagements}
            subtext={`${kpis.openEngagements.toLocaleString()} planned · ${kpis.completedEngagements.toLocaleString()} completed`}
            tone="warning"
            onClick={() => navigate("/admin/compliance")}
            icon={
              <svg className="w-4 h-4 text-[#D49A43]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            }
          />
          <MetricCard
            label="Active Branches"
            value={kpis.activeBranches}
            subtext="Registered practice locations"
            tone="purple"
            onClick={() => navigate("/admin/clients")}
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
            onClick={() => navigate("/admin/staff-workload")}
            icon={
              <svg className="w-4 h-4 text-[#1AA879]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5.121 17.804A13.937 13.937 0 0112 16c2.5 0 4.847.655 6.879 1.804M15 10a3 3 0 11-6 0 3 3 0 016 0zm6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            }
          />
        </section>
      )}

      {/* Visual Analytics Grid (Two Columns on Desktop) */}
      <section className="grid grid-cols-1 lg:grid-cols-2 gap-5 sm:gap-6 items-stretch">
        {/* 1. Clients by Entity Type - Donut Chart */}
        <DonutChart
          title="Clients by Entity Type"
          subtitle="Corporate, LLP, Partnership, Individual & Trust breakdown"
          rows={byEntity}
          centerLabel="Clients"
          maxLegendItems={5}
          emptyTitle="No Client Entity Data"
          emptyDescription="Active clients categorized by statutory legal structure will appear here."
        />

        {/* 2. Clients by State (Place of Supply) - Horizontal Progress Bars */}
        <HorizontalBarChart
          title="Clients by State (Place of Supply)"
          subtitle="Distribution of primary branch locations across India"
          rows={byState}
          totalLabel="States"
          maxInitialRows={6}
          emptyTitle="No State of Supply Data"
          emptyDescription="No primary branch locations registered yet. Primary branches added to client profiles will show state-wise place of supply distribution here."
        />

        {/* 3. Open Service Files by Service - Ranking Progress Bars */}
        <HorizontalBarChart
          title="Open Service Files by Service"
          subtitle="Income tax, GST, statutory audit, ROC and advisory files"
          rows={byService}
          totalLabel="Files"
          maxInitialRows={6}
          emptyTitle="No Open Service Files"
          emptyDescription="Active client service files categorized by service type will appear here."
        />

        {/* 4. Top Staff by Active Assignments - Staff Capacity Ranking */}
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
