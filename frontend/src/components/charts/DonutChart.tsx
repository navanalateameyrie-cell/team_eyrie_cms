import { useState, useId } from "react";
import type { Breakdown } from "../../api/dashboard";
import { Card, CardHeader, CardContent } from "../ui";

export interface DonutChartProps {
  title: string;
  subtitle?: string;
  rows: Breakdown[];
  emptyTitle?: string;
  emptyDescription?: string;
  centerLabel?: string;
  maxLegendItems?: number;
  className?: string;
}

const DONUT_COLORS = [
  "#102747", // Deep Corporate Navy
  "#2563EB", // Royal Sapphire Blue
  "#0D9488", // Deep Teal
  "#C86A24", // Warm Terracotta Orange
  "#6366F1", // Indigo
  "#059669", // Emerald
  "#D97706", // Amber
  "#475569", // Slate
  "#E11D48", // Rose
  "#8B5CF6", // Violet
];

export default function DonutChart({
  title,
  subtitle,
  rows,
  emptyTitle = "No State of Supply Data",
  emptyDescription = "No primary branch locations registered yet. Branches added to client profiles will show state-wise place of supply distribution here.",
  centerLabel = "Total Clients",
  maxLegendItems = 5,
  className = "",
}: DonutChartProps) {
  const [hoveredIndex, setHoveredIndex] = useState<number | null>(null);
  const [showAllLegend, setShowAllLegend] = useState(false);
  const chartId = useId();

  // Sort descending by count
  const sorted = [...rows].sort((a, b) => b.count - a.count);
  const total = sorted.reduce((sum, r) => sum + r.count, 0);

  const canExpand = sorted.length > maxLegendItems;
  const displayedLegend = showAllLegend || !canExpand ? sorted : sorted.slice(0, maxLegendItems);

  // SVG parameters
  const radius = 64;
  const strokeWidth = 18;
  const circumference = 2 * Math.PI * radius; // ~402.12

  let cumulativeOffset = 0;
  const slices = sorted.map((item, index) => {
    const fraction = total > 0 ? item.count / total : 0;
    const strokeDash = fraction * circumference;
    const gap = sorted.length > 1 ? 2 : 0;
    const currentOffset = cumulativeOffset;
    cumulativeOffset += strokeDash;

    return {
      ...item,
      color: DONUT_COLORS[index % DONUT_COLORS.length],
      fraction,
      percentage: (fraction * 100).toFixed(1),
      dashArray: `${Math.max(0, strokeDash - gap)} ${circumference}`,
      dashOffset: -currentOffset,
    };
  });

  const activeItem = hoveredIndex !== null && slices[hoveredIndex] ? slices[hoveredIndex] : null;

  return (
    <Card className={`flex flex-col h-full bg-white border border-[#E0E6EF] rounded-2xl shadow-xs hover:shadow-md transition-shadow duration-200 overflow-hidden ${className}`}>
      <CardHeader
        variant="navy"
        title={title}
        subtitle={subtitle}
        icon={
          <svg className="w-4 h-4 text-[#FDBA74]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 3.055A9.001 9.001 0 1020.945 13H11V3.055z" />
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M20.488 9H15V3.512A9.025 9.025 0 0120.488 9z" />
          </svg>
        }
        action={
          total > 0 ? (
            <span className="inline-flex items-center px-2.5 py-1 rounded-md text-xs font-semibold bg-white/10 text-white border border-white/20 font-mono tabular-nums">
              {total.toLocaleString()} total
            </span>
          ) : undefined
        }
      />

      <CardContent className="flex-1 flex flex-col justify-between pt-2 pb-5">
        {sorted.length === 0 ? (
          <div className="py-12 flex flex-col items-center justify-center text-center">
            {/* Elegant SVG location emblem */}
            <div className="h-12 w-12 rounded-xl bg-slate-50 border border-slate-200/70 flex items-center justify-center text-slate-400 mb-3 shadow-2xs">
              <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={1.5}
                  d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"
                />
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={1.5}
                  d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"
                />
              </svg>
            </div>
            <h4 className="text-xs font-semibold text-slate-900">{emptyTitle}</h4>
            <p className="mt-1 text-[11px] text-slate-500 max-w-xs">{emptyDescription}</p>
            <div className="mt-3">
              <a
                href="/admin/clients"
                className="text-xs font-semibold text-navy-800 hover:text-navy-950 underline decoration-gold-400 underline-offset-4"
              >
                Open Clients Directory &rarr;
              </a>
            </div>
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-12 gap-6 items-center">
            {/* SVG Donut Visual */}
            <div className="sm:col-span-5 flex justify-center items-center py-2">
              <div className="relative w-44 h-44">
                <svg
                  viewBox="0 0 200 200"
                  className="w-full h-full transform -rotate-90 select-none overflow-visible"
                  aria-label={`${title} chart`}
                  role="img"
                >
                  {/* Background Track */}
                  <circle
                    cx="100"
                    cy="100"
                    r={radius}
                    fill="none"
                    stroke="#f1f5f9"
                    strokeWidth={strokeWidth}
                  />

                  {/* Slices */}
                  {slices.map((s, idx) => {
                    const isHovered = hoveredIndex === idx;
                    return (
                      <circle
                        key={s.label}
                        cx="100"
                        cy="100"
                        r={radius}
                        fill="none"
                        stroke={s.color}
                        strokeWidth={isHovered ? strokeWidth + 4 : strokeWidth}
                        strokeDasharray={s.dashArray}
                        strokeDashoffset={s.dashOffset}
                        className="transition-all duration-200 cursor-pointer"
                        onMouseEnter={() => setHoveredIndex(idx)}
                        onMouseLeave={() => setHoveredIndex(null)}
                      />
                    );
                  })}
                </svg>

                {/* Center Label & Total */}
                <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none text-center px-4">
                  <span className="font-mono font-extrabold text-2xl text-navy-950 tabular-nums leading-none">
                    {activeItem ? activeItem.count.toLocaleString() : total.toLocaleString()}
                  </span>
                  <span className="text-[10px] font-bold uppercase tracking-wider text-slate-500 mt-1 max-w-[90px] truncate leading-tight">
                    {activeItem ? activeItem.label : centerLabel}
                  </span>
                  {activeItem && (
                    <span className="text-[11px] font-mono font-bold text-navy-800 mt-0.5">
                      {activeItem.percentage}%
                    </span>
                  )}
                </div>
              </div>
            </div>

            {/* State Legend List */}
            <div className="sm:col-span-7 space-y-2" id={chartId}>
              {displayedLegend.map((s, idx) => {
                const sliceData = slices[idx];
                const isHovered = hoveredIndex === idx;

                return (
                  <div
                    key={s.label}
                    onMouseEnter={() => setHoveredIndex(idx)}
                    onMouseLeave={() => setHoveredIndex(null)}
                    className={`flex items-center justify-between p-2 rounded-lg text-xs cursor-pointer transition-colors border ${isHovered
                      ? "bg-slate-50 border-slate-200 shadow-2xs"
                      : "border-transparent hover:bg-slate-50/70"
                      }`}
                  >
                    <div className="flex items-center gap-2.5 min-w-0">
                      <span
                        className="w-2.5 h-2.5 rounded-full shrink-0 shadow-2xs"
                        style={{ backgroundColor: sliceData?.color ?? DONUT_COLORS[idx % DONUT_COLORS.length] }}
                      />
                      <span className="font-semibold text-navy-950 truncate max-w-[140px] sm:max-w-[170px]" title={s.label}>
                        {s.label}
                      </span>
                    </div>

                    <div className="flex items-center gap-2 shrink-0">
                      <span className="font-mono font-bold text-navy-950 tabular-nums">
                        {s.count.toLocaleString()}
                      </span>
                      <span className="inline-flex items-center justify-center font-mono text-[11px] font-semibold text-slate-600 bg-slate-100/90 px-1.5 py-0.5 rounded border border-slate-200/60 tabular-nums min-w-[44px] text-center">
                        {sliceData?.percentage}%
                      </span>
                    </div>
                  </div>
                );
              })}

              {/* Show more / show less link */}
              {canExpand && (
                <div className="pt-2 text-right">
                  <button
                    type="button"
                    onClick={() => setShowAllLegend(!showAllLegend)}
                    className="text-xs font-semibold text-navy-700 hover:text-navy-900 transition-colors"
                  >
                    {showAllLegend ? "Show less" : `View all ${sorted.length} states &rarr;`}
                  </button>
                </div>
              )}
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

