import type { ReactNode } from "react";
import { Card } from "./Card";

export interface MetricCardProps {
  label: string;
  value: string | number;
  subtext?: string;
  hint?: string;
  icon?: ReactNode;
  tone?: "default" | "brand" | "gold" | "success" | "warning" | "danger" | "info";
  trend?: {
    value?: string;
    label?: string;
    direction?: "up" | "down";
    isPositive?: boolean;
  };
  onClick?: () => void;
  className?: string;
}

const toneAccents: Record<string, { topBar: string; iconBg: string; iconText: string; ring: string }> = {
  default: {
    topBar: "from-slate-400 to-slate-500",
    iconBg: "bg-slate-50",
    iconText: "text-slate-700",
    ring: "ring-slate-200/80",
  },
  brand: {
    topBar: "from-navy-800 to-navy-600",
    iconBg: "bg-navy-50",
    iconText: "text-navy-800",
    ring: "ring-navy-200/70",
  },
  gold: {
    topBar: "from-gold-600 to-amber-500",
    iconBg: "bg-gold-50",
    iconText: "text-gold-700",
    ring: "ring-gold-300/60",
  },
  success: {
    topBar: "from-emerald-600 to-teal-500",
    iconBg: "bg-emerald-50",
    iconText: "text-emerald-700",
    ring: "ring-emerald-200/70",
  },
  warning: {
    topBar: "from-amber-500 to-orange-400",
    iconBg: "bg-amber-50",
    iconText: "text-amber-700",
    ring: "ring-amber-200/70",
  },
  danger: {
    topBar: "from-rose-600 to-red-500",
    iconBg: "bg-rose-50",
    iconText: "text-rose-700",
    ring: "ring-rose-200/70",
  },
  info: {
    topBar: "from-sky-600 to-blue-500",
    iconBg: "bg-sky-50",
    iconText: "text-sky-700",
    ring: "ring-sky-200/70",
  },
};

export function MetricCard({
  label,
  value,
  subtext,
  hint,
  icon,
  tone = "default",
  trend,
  onClick,
  className = "",
}: MetricCardProps) {
  const note = subtext ?? hint;
  const isPositive =
    trend?.isPositive !== undefined
      ? trend.isPositive
      : trend?.direction === "up";
  const trendText = trend?.value ?? trend?.label;
  const formattedValue = typeof value === "number" ? value.toLocaleString() : value;
  const accent = toneAccents[tone] ?? toneAccents.default;

  return (
    <Card
      onClick={onClick}
      className={`relative overflow-hidden bg-white border border-slate-200/90 rounded-xl shadow-[0_1px_3px_rgba(0,0,0,0.04)] hover:shadow-md transition-all duration-200 flex flex-col justify-between ${onClick ? "cursor-pointer hover:-translate-y-0.5" : ""
        } ${className}`}
    >
      {/* Subtle top accent gradient line */}
      <div className={`h-1 w-full bg-gradient-to-r ${accent.topBar}`} />

      <div className="p-4 sm:p-5 flex-1 flex flex-col justify-between">
        <div>
          <div className="flex items-center justify-between gap-3">
            <p className="text-[11px] font-bold uppercase tracking-wider text-slate-500 truncate" title={label}>
              {label}
            </p>
            {icon && (
              <div
                className={`h-8 w-8 rounded-lg flex items-center justify-center shrink-0 ring-1 shadow-2xs ${accent.iconBg} ${accent.iconText} ${accent.ring}`}
              >
                {icon}
              </div>
            )}
          </div>

          <div className="mt-3 flex items-baseline gap-2">
            <span className="text-2xl sm:text-3xl font-extrabold tracking-tight text-navy-950 font-sans tabular-nums leading-none">
              {formattedValue}
            </span>
            {trendText && (
              <span
                className={`inline-flex items-center text-xs font-semibold px-1.5 py-0.5 rounded ${isPositive
                  ? "bg-emerald-50 text-emerald-700 border border-emerald-200/60"
                  : "bg-rose-50 text-rose-700 border border-rose-200/60"
                  }`}
              >
                {isPositive ? "↑" : "↓"} {trendText}
              </span>
            )}
          </div>
        </div>

        {note && (
          <div className="mt-3.5 pt-3 border-t border-slate-100 flex items-center gap-1.5 text-xs text-slate-500">
            <span className="w-1.5 h-1.5 rounded-full bg-slate-300 shrink-0" />
            <p className="truncate text-[11px] leading-tight font-medium" title={note}>
              {note}
            </p>
          </div>
        )}
      </div>
    </Card>
  );
}

