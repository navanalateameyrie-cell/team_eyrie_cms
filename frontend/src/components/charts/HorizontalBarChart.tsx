import { useState, useId } from "react";
import type { Breakdown } from "../../api/dashboard";
import { Card, CardHeader, CardContent } from "../ui";

export interface HorizontalBarChartProps {
  title: string;
  subtitle?: string;
  rows: Breakdown[];
  emptyTitle?: string;
  emptyDescription?: string;
  maxInitialRows?: number;
  showPercentages?: boolean;
  totalLabel?: string;
  barColor?: "brand" | "navy" | "slate" | "multi";
  className?: string;
}

// Executive corporate color gradients based on ranking
const RANK_GRADIENTS = [
  "from-[#102747] via-[#19385F] to-[#264A77] shadow-xs", // #1 dominant
  "from-[#19385F] via-[#264A77] to-[#3976B9]",          // #2
  "from-[#264A77] via-[#3976B9] to-[#5592D0]",         // #3
  "from-[#3976B9] via-[#5592D0] to-[#7BB2E5]",         // #4
  "from-[#5592D0] via-[#7BB2E5] to-[#A2CEF5]",         // #5
  "from-[#7BB2E5] via-[#A2CEF5] to-[#CBE5FB]",         // #6+
];

export default function HorizontalBarChart({
  title,
  subtitle,
  rows,
  emptyTitle = "No data available",
  emptyDescription = "No records currently match this metric breakdown.",
  maxInitialRows = 6,
  showPercentages = true,
  totalLabel = "Total",
  barColor = "multi",
  className = "",
}: HorizontalBarChartProps) {
  const [expanded, setExpanded] = useState(false);
  const chartId = useId();

  // Sort descending by count
  const sorted = [...rows].sort((a, b) => b.count - a.count);
  const total = sorted.reduce((sum, r) => sum + r.count, 0);
  const max = sorted.reduce((m, r) => Math.max(m, r.count), 0) || 1;

  const canExpand = sorted.length > maxInitialRows;
  const displayedRows = expanded || !canExpand ? sorted : sorted.slice(0, maxInitialRows);

  function getBarGradient(index: number) {
    if (barColor === "navy") {
      return "from-navy-900 via-navy-800 to-navy-700";
    }
    if (barColor === "slate") {
      return "from-slate-700 via-slate-600 to-slate-500";
    }
    if (barColor === "brand") {
      return "from-navy-900 via-navy-800 to-navy-700";
    }
    return RANK_GRADIENTS[index] ?? "from-slate-500 to-slate-400";
  }

  return (
    <Card className={`flex flex-col h-full bg-white border border-[#E0E6EF] rounded-2xl shadow-xs hover:shadow-md transition-shadow duration-200 overflow-hidden ${className}`}>
      <CardHeader
        variant="navy"
        title={title}
        subtitle={subtitle}
        icon={
          <svg className="w-4 h-4 text-[#FDBA74]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
          </svg>
        }
        action={
          total > 0 ? (
            <div className="flex items-center gap-2">
              <span className="inline-flex items-center px-2.5 py-1 rounded-md text-xs font-semibold bg-white/10 text-white border border-white/20 font-mono tabular-nums">
                {total.toLocaleString()} {totalLabel.toLowerCase()}
              </span>
              {canExpand && (
                <button
                  type="button"
                  onClick={() => setExpanded(!expanded)}
                  className="text-xs font-semibold text-[#FDBA74] hover:text-white transition-colors focus:outline-none focus:underline"
                  aria-expanded={expanded}
                  aria-controls={chartId}
                >
                  {expanded ? "Show less" : `View all (${sorted.length})`}
                </button>
              )}
            </div>
          ) : undefined
        }
      />

      <CardContent className="flex-1 flex flex-col justify-between pt-2 pb-5">
        {sorted.length === 0 ? (
          <div className="py-12 flex flex-col items-center justify-center text-center">
            <div className="h-12 w-12 rounded-xl bg-slate-50 border border-slate-200/70 flex items-center justify-center text-slate-400 mb-3 shadow-2xs">
              <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
              </svg>
            </div>
            <h4 className="text-xs font-semibold text-slate-800">{emptyTitle}</h4>
            <p className="mt-1 text-[11px] text-slate-500 max-w-xs">{emptyDescription}</p>
          </div>
        ) : (
          <div id={chartId} className="space-y-3">
            {displayedRows.map((r, index) => {
              const pctOfMax = Math.round((r.count / max) * 100);
              const pctOfTotal = total > 0 ? (Math.round((r.count / total) * 1000) / 10).toFixed(1) : "0";
              const gradient = getBarGradient(index);

              return (
                <div
                  key={r.label}
                  className="group p-2 rounded-lg hover:bg-slate-50/80 transition-colors border border-transparent hover:border-slate-200/50"
                >
                  <div className="flex items-center justify-between text-xs mb-2 gap-2">
                    <div className="flex items-center gap-2 min-w-0">
                      <span className="w-5 h-5 rounded-md bg-slate-100 text-slate-600 font-mono text-[10px] font-bold flex items-center justify-center shrink-0 border border-slate-200/60">
                        {index + 1}
                      </span>
                      <span
                        className="font-semibold text-navy-950 truncate group-hover:text-navy-700 transition-colors"
                        title={`${r.label}: ${r.count.toLocaleString()} (${pctOfTotal}%)`}
                      >
                        {r.label}
                      </span>
                    </div>

                    <div className="flex items-center gap-2 shrink-0">
                      <span className="font-mono font-bold text-navy-950 text-xs sm:text-sm tabular-nums">
                        {r.count.toLocaleString()}
                      </span>
                      {showPercentages && (
                        <span className="inline-flex items-center justify-center font-mono text-[11px] font-semibold text-slate-600 bg-slate-100/90 px-1.5 py-0.5 rounded border border-slate-200/60 tabular-nums min-w-[46px] text-center">
                          {pctOfTotal}%
                        </span>
                      )}
                    </div>
                  </div>

                  {/* Tactile Proportional Bar Track */}
                  <div
                    className="h-3 sm:h-3.5 rounded-full bg-slate-100/90 overflow-hidden relative border border-slate-200/50 p-[1px] shadow-inner"
                    role="progressbar"
                    aria-valuenow={r.count}
                    aria-valuemin={0}
                    aria-valuemax={max}
                    aria-label={`${r.label}: ${r.count}`}
                  >
                    <div
                      className={`h-full rounded-full transition-all duration-700 ease-out bg-gradient-to-r ${gradient}`}
                      style={{ width: `${Math.max(pctOfMax, 2)}%` }}
                    />
                  </div>
                </div>
              );
            })}
          </div>
        )}

        {/* Footer info when truncated */}
        {canExpand && !expanded && sorted.length > maxInitialRows && (
          <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between text-[11px] text-slate-500 px-1">
            <span>Showing top {maxInitialRows} of {sorted.length} categories</span>
            <button
              type="button"
              onClick={() => setExpanded(true)}
              className="font-semibold text-navy-700 hover:text-navy-900 transition-colors"
            >
              View all &rarr;
            </button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

