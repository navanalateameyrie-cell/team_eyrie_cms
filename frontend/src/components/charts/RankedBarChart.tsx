import type { Breakdown } from "../../api/dashboard";
import { Card, CardHeader, CardContent } from "../ui";

export interface RankedBarChartProps {
  title: string;
  subtitle?: string;
  rows: Breakdown[];
  isRestricted?: boolean;
  emptyTitle?: string;
  emptyDescription?: string;
  className?: string;
}

export default function RankedBarChart({
  title,
  subtitle,
  rows,
  isRestricted = false,
  emptyTitle = "No Active Staff Assignments",
  emptyDescription = "There are currently no active case assignments delegated to team members. As service files and statutory tasks are assigned, workload distribution will appear here.",
  className = "",
}: RankedBarChartProps) {
  // Sort descending by assignment count
  const sorted = [...rows].sort((a, b) => b.count - a.count);
  const max = sorted.reduce((m, r) => Math.max(m, r.count), 0) || 1;
  const totalAssignments = sorted.reduce((sum, r) => sum + r.count, 0);

  return (
    <Card className={`flex flex-col h-full bg-white border border-slate-200/90 rounded-xl shadow-xs hover:shadow-md transition-shadow duration-200 ${className}`}>
      <CardHeader
        title={title}
        subtitle={subtitle}
        action={
          totalAssignments > 0 ? (
            <span className="inline-flex items-center px-2.5 py-1 rounded-md text-xs font-semibold bg-navy-50 text-navy-900 border border-navy-200/60 font-mono tabular-nums">
              {totalAssignments.toLocaleString()} total active
            </span>
          ) : undefined
        }
      />

      <CardContent className="flex-1 flex flex-col justify-between pt-2 pb-5">
        {isRestricted ? (
          <div className="py-12 flex flex-col items-center justify-center text-center">
            <div className="h-12 w-12 rounded-xl bg-amber-50 border border-amber-200 flex items-center justify-center text-amber-700 mb-3 shadow-2xs">
              <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
              </svg>
            </div>
            <h4 className="text-xs font-semibold text-slate-900">Staff Workload Restricted</h4>
            <p className="mt-1 text-[11px] text-slate-500 max-w-xs">
              Viewing staff capacity requires the <code className="font-mono text-navy-700 bg-navy-50 px-1 py-0.5 rounded border border-navy-200/60">staff.read</code> permission.
            </p>
          </div>
        ) : sorted.length === 0 ? (
          <div className="py-12 flex flex-col items-center justify-center text-center">
            <div className="h-12 w-12 rounded-xl bg-slate-50 border border-slate-200/70 flex items-center justify-center text-slate-400 mb-3 shadow-2xs">
              <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
              </svg>
            </div>
            <h4 className="text-xs font-semibold text-slate-900">{emptyTitle}</h4>
            <p className="mt-1 text-[11px] text-slate-500 max-w-xs">{emptyDescription}</p>
            <div className="mt-3">
              <a
                href="/admin/staff-workload"
                className="text-xs font-semibold text-navy-800 hover:text-navy-950 underline decoration-gold-400 underline-offset-4"
              >
                Open Staff Workload Allocation &rarr;
              </a>
            </div>
          </div>
        ) : (
          <div className="space-y-3">
            {sorted.map((r, index) => {
              const rank = index + 1;
              const pctOfMax = Math.round((r.count / max) * 100);

              // Distinct ranking badge styling
              const rankBadge =
                rank === 1
                  ? "bg-amber-100 text-amber-900 border border-amber-300 font-bold shadow-2xs"
                  : rank === 2
                    ? "bg-slate-200 text-slate-800 border border-slate-300 font-semibold"
                    : rank === 3
                      ? "bg-amber-50 text-amber-800 border border-amber-200/80 font-medium"
                      : "bg-slate-100 text-slate-600 font-mono text-[10px] border border-slate-200";

              const barGradient =
                rank === 1
                  ? "from-navy-950 via-navy-900 to-navy-800 shadow-sm"
                  : rank === 2
                    ? "from-navy-900 via-navy-800 to-navy-700"
                    : "from-navy-800 to-slate-700";

              return (
                <div
                  key={r.label}
                  className="group p-2 rounded-lg hover:bg-slate-50/80 transition-colors border border-transparent hover:border-slate-200/50"
                >
                  <div className="flex items-center justify-between text-xs mb-2 gap-2">
                    <div className="flex items-center gap-2 min-w-0">
                      <span
                        className={`w-5 h-5 rounded-md flex items-center justify-center text-[10px] shrink-0 ${rankBadge}`}
                      >
                        #{rank}
                      </span>
                      <span
                        className="font-semibold text-navy-950 truncate group-hover:text-navy-700 transition-colors"
                        title={r.label}
                      >
                        {r.label}
                      </span>
                    </div>

                    <div className="flex items-center gap-1.5 shrink-0">
                      <span className="font-mono font-bold text-navy-950 text-xs sm:text-sm tabular-nums">
                        {r.count.toLocaleString()}
                      </span>
                      <span className="text-[11px] text-slate-500 font-medium">
                        {r.count === 1 ? "case" : "cases"}
                      </span>
                    </div>
                  </div>

                  {/* Track and Bar */}
                  <div
                    className="h-3 sm:h-3.5 rounded-full bg-slate-100/90 overflow-hidden relative border border-slate-200/50 p-[1px] shadow-inner"
                    role="progressbar"
                    aria-valuenow={r.count}
                    aria-valuemin={0}
                    aria-valuemax={max}
                    aria-label={`${r.label}: ${r.count} assignments`}
                  >
                    <div
                      className={`h-full rounded-full transition-all duration-700 ease-out bg-gradient-to-r ${barGradient}`}
                      style={{ width: `${Math.max(pctOfMax, 2)}%` }}
                    />
                  </div>
                </div>
              );
            })}
          </div>
        )}

        {/* Informative Governance Disclaimer */}
        <div className="mt-4 pt-3 border-t border-slate-100 text-[11px] text-slate-500 flex items-center gap-1.5 px-1">
          <svg className="w-3.5 h-3.5 text-slate-400 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <span className="leading-tight">
            Active assignment counts reflect case delegation capacity, not staff performance evaluation.
          </span>
        </div>
      </CardContent>
    </Card>
  );
}

