import type { ReactNode } from "react";

export interface MetricCardProps {
  label: string;
  value: string | number;
  subtext?: string;
  hint?: string;
  icon?: ReactNode;
  tone?: "default" | "brand" | "gold" | "success" | "warning" | "danger" | "info" | "purple";
  trend?: {
    value?: string;
    label?: string;
    direction?: "up" | "down";
    isPositive?: boolean;
  };
  onClick?: () => void;
  className?: string;
}

interface GlassStyle {
  cardBg: string;
  borderColor: string;
  orbBg: string;
  topBar: string;
  labelColor: string;
  valueColor: string;
  iconBg: string;
  iconText: string;
  iconBorder: string;
  footerBorder: string;
  footerText: string;
  dotBg: string;
}

const toneGlassStyles: Record<string, GlassStyle> = {
  brand: {
    cardBg: "bg-gradient-to-br from-blue-500/[0.08] via-white/85 to-indigo-500/[0.06]",
    borderColor: "border-blue-200/80 hover:border-blue-400/90",
    orbBg: "bg-gradient-to-br from-blue-400 to-indigo-500",
    topBar: "from-blue-600 via-indigo-600 to-blue-500",
    labelColor: "text-blue-900/85",
    valueColor: "text-blue-950",
    iconBg: "bg-white/80 backdrop-blur-md shadow-[0_2px_10px_rgba(37,99,235,0.12)]",
    iconText: "text-blue-600",
    iconBorder: "border-blue-200/80",
    footerBorder: "border-blue-100/80",
    footerText: "text-blue-950/70",
    dotBg: "bg-blue-500 shadow-[0_0_8px_rgba(59,130,246,0.6)]",
  },
  info: {
    cardBg: "bg-gradient-to-br from-sky-500/[0.08] via-white/85 to-cyan-500/[0.06]",
    borderColor: "border-sky-200/80 hover:border-sky-400/90",
    orbBg: "bg-gradient-to-br from-sky-400 to-cyan-500",
    topBar: "from-sky-500 via-blue-500 to-cyan-400",
    labelColor: "text-sky-900/85",
    valueColor: "text-sky-950",
    iconBg: "bg-white/80 backdrop-blur-md shadow-[0_2px_10px_rgba(2,132,199,0.12)]",
    iconText: "text-sky-600",
    iconBorder: "border-sky-200/80",
    footerBorder: "border-sky-100/80",
    footerText: "text-sky-950/70",
    dotBg: "bg-sky-500 shadow-[0_0_8px_rgba(14,165,233,0.6)]",
  },
  warning: {
    cardBg: "bg-gradient-to-br from-amber-500/[0.09] via-white/85 to-orange-500/[0.06]",
    borderColor: "border-amber-200/80 hover:border-amber-400/90",
    orbBg: "bg-gradient-to-br from-amber-400 to-orange-500",
    topBar: "from-amber-500 via-orange-500 to-amber-400",
    labelColor: "text-amber-900/85",
    valueColor: "text-amber-950",
    iconBg: "bg-white/80 backdrop-blur-md shadow-[0_2px_10px_rgba(217,119,6,0.12)]",
    iconText: "text-amber-600",
    iconBorder: "border-amber-200/80",
    footerBorder: "border-amber-100/80",
    footerText: "text-amber-950/70",
    dotBg: "bg-amber-500 shadow-[0_0_8px_rgba(245,158,11,0.6)]",
  },
  purple: {
    cardBg: "bg-gradient-to-br from-purple-500/[0.08] via-white/85 to-indigo-500/[0.06]",
    borderColor: "border-purple-200/80 hover:border-purple-400/90",
    orbBg: "bg-gradient-to-br from-purple-400 to-indigo-500",
    topBar: "from-purple-600 via-indigo-600 to-purple-500",
    labelColor: "text-purple-900/85",
    valueColor: "text-purple-950",
    iconBg: "bg-white/80 backdrop-blur-md shadow-[0_2px_10px_rgba(147,51,234,0.12)]",
    iconText: "text-purple-600",
    iconBorder: "border-purple-200/80",
    footerBorder: "border-purple-100/80",
    footerText: "text-purple-950/70",
    dotBg: "bg-purple-500 shadow-[0_0_8px_rgba(168,85,247,0.6)]",
  },
  default: {
    cardBg: "bg-gradient-to-br from-indigo-500/[0.08] via-white/85 to-purple-500/[0.06]",
    borderColor: "border-indigo-200/80 hover:border-indigo-400/90",
    orbBg: "bg-gradient-to-br from-indigo-400 to-purple-500",
    topBar: "from-indigo-600 via-purple-600 to-indigo-500",
    labelColor: "text-indigo-900/85",
    valueColor: "text-indigo-950",
    iconBg: "bg-white/80 backdrop-blur-md shadow-[0_2px_10px_rgba(99,102,241,0.12)]",
    iconText: "text-indigo-600",
    iconBorder: "border-indigo-200/80",
    footerBorder: "border-indigo-100/80",
    footerText: "text-indigo-950/70",
    dotBg: "bg-indigo-500 shadow-[0_0_8px_rgba(99,102,241,0.6)]",
  },
  success: {
    cardBg: "bg-gradient-to-br from-emerald-500/[0.08] via-white/85 to-teal-500/[0.06]",
    borderColor: "border-emerald-200/80 hover:border-emerald-400/90",
    orbBg: "bg-gradient-to-br from-emerald-400 to-teal-500",
    topBar: "from-emerald-600 via-teal-600 to-emerald-500",
    labelColor: "text-emerald-900/85",
    valueColor: "text-emerald-950",
    iconBg: "bg-white/80 backdrop-blur-md shadow-[0_2px_10px_rgba(16,185,129,0.12)]",
    iconText: "text-emerald-600",
    iconBorder: "border-emerald-200/80",
    footerBorder: "border-emerald-100/80",
    footerText: "text-emerald-950/70",
    dotBg: "bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.6)]",
  },
  gold: {
    cardBg: "bg-gradient-to-br from-amber-500/[0.1] via-white/85 to-yellow-500/[0.06]",
    borderColor: "border-amber-300/80 hover:border-amber-400",
    orbBg: "bg-gradient-to-br from-amber-400 to-yellow-500",
    topBar: "from-amber-600 via-yellow-500 to-amber-400",
    labelColor: "text-amber-900/85",
    valueColor: "text-amber-950",
    iconBg: "bg-white/80 backdrop-blur-md shadow-[0_2px_10px_rgba(217,119,6,0.15)]",
    iconText: "text-amber-700",
    iconBorder: "border-amber-300/80",
    footerBorder: "border-amber-200/70",
    footerText: "text-amber-950/70",
    dotBg: "bg-amber-500 shadow-[0_0_8px_rgba(245,158,11,0.6)]",
  },
  danger: {
    cardBg: "bg-gradient-to-br from-rose-500/[0.08] via-white/85 to-red-500/[0.06]",
    borderColor: "border-rose-200/80 hover:border-rose-400/90",
    orbBg: "bg-gradient-to-br from-rose-400 to-red-500",
    topBar: "from-rose-600 via-red-600 to-rose-500",
    labelColor: "text-rose-900/85",
    valueColor: "text-rose-950",
    iconBg: "bg-white/80 backdrop-blur-md shadow-[0_2px_10px_rgba(225,29,72,0.12)]",
    iconText: "text-rose-600",
    iconBorder: "border-rose-200/80",
    footerBorder: "border-rose-100/80",
    footerText: "text-rose-950/70",
    dotBg: "bg-rose-500 shadow-[0_0_8px_rgba(244,63,94,0.6)]",
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
  const accent = toneGlassStyles[tone] ?? toneGlassStyles.default;

  return (
    <div
      onClick={onClick}
      className={`relative overflow-hidden rounded-2xl backdrop-blur-xl border ${accent.cardBg} ${accent.borderColor} shadow-[0_8px_30px_rgb(0,0,0,0.04),inset_0_1px_1px_0_rgba(255,255,255,0.9)] hover:shadow-lg transition-all duration-300 flex flex-col justify-between group ${
        onClick ? "cursor-pointer hover:-translate-y-1" : ""
      } ${className}`}
    >
      {/* Ambient glowing glass orb */}
      <div
        className={`pointer-events-none absolute -right-6 -top-6 w-28 h-28 rounded-full blur-2xl opacity-35 group-hover:opacity-60 transition-opacity duration-300 ${accent.orbBg}`}
      />

      {/* Top accent gradient stripe */}
      <div className={`h-1.5 w-full bg-gradient-to-r ${accent.topBar}`} />

      <div className="p-4 sm:p-5 flex-1 flex flex-col justify-between relative z-10">
        <div>
          <div className="flex items-center justify-between gap-3">
            <p className={`text-[11px] font-bold uppercase tracking-wider truncate ${accent.labelColor}`} title={label}>
              {label}
            </p>
            {icon && (
              <div
                className={`h-9 w-9 rounded-xl flex items-center justify-center shrink-0 border transition-transform duration-300 group-hover:scale-105 ${accent.iconBg} ${accent.iconText} ${accent.iconBorder}`}
              >
                {icon}
              </div>
            )}
          </div>

          <div className="mt-3 flex items-baseline gap-2">
            <span className={`text-2xl sm:text-3xl font-extrabold tracking-tight font-sans tabular-nums leading-none ${accent.valueColor}`}>
              {formattedValue}
            </span>
            {trendText && (
              <span
                className={`inline-flex items-center text-xs font-semibold px-2 py-0.5 rounded-full backdrop-blur-md ${
                  isPositive
                    ? "bg-emerald-500/15 text-emerald-800 border border-emerald-300/60"
                    : "bg-rose-500/15 text-rose-800 border border-rose-300/60"
                }`}
              >
                {isPositive ? "↑" : "↓"} {trendText}
              </span>
            )}
          </div>
        </div>

        {note && (
          <div className={`mt-3.5 pt-3 border-t flex items-center gap-2 text-xs ${accent.footerBorder}`}>
            <span className={`w-1.5 h-1.5 rounded-full shrink-0 ${accent.dotBg}`} />
            <p className={`truncate text-[11px] leading-tight font-medium ${accent.footerText}`} title={note}>
              {note}
            </p>
          </div>
        )}
      </div>
    </div>
  );
}

