import type { ReactNode } from "react";

export interface MetricCardProps {
  label: string;
  value: string | number;
  subtext?: string;
  hint?: string;
  icon?: ReactNode;
  tone?: "default" | "brand" | "navy" | "gold" | "success" | "warning" | "danger" | "info" | "purple";
  trend?: {
    value?: string;
    label?: string;
    direction?: "up" | "down";
    isPositive?: boolean;
  };
  onClick?: () => void;
  className?: string;
}

interface ToneStyle {
  cardBg: string;
  borderColor: string;
  topStripe: string;
  lowerAccent?: string;
  labelColor: string;
  valueColor: string;
  iconBg: string;
  iconText: string;
  iconBorder: string;
  footerBorder: string;
  footerText: string;
  dotBg: string;
  cardShadow: string;
  hasWatermark?: boolean;
}

const toneStyles: Record<string, ToneStyle> = {
  navy: {
    cardBg: "bg-gradient-to-br from-blue-100/95 via-indigo-50/60 to-white/95 backdrop-blur-md",
    borderColor: "border-blue-200/80 hover:border-blue-400",
    topStripe: "from-blue-600 via-indigo-500 to-blue-400",
    labelColor: "text-blue-900 font-bold",
    valueColor: "text-[#0F1E36]",
    iconBg: "bg-white/90 backdrop-blur-sm",
    iconText: "text-blue-600",
    iconBorder: "border-blue-200/80 shadow-2xs",
    footerBorder: "border-blue-100/80",
    footerText: "text-slate-600",
    dotBg: "bg-blue-600 shadow-[0_0_6px_rgba(37,99,235,0.6)]",
    cardShadow: "shadow-[0_6px_20px_rgba(59,130,246,0.08)] hover:shadow-[0_10px_26px_rgba(59,130,246,0.16)]",
  },
  brand: {
    cardBg: "bg-gradient-to-br from-blue-100/95 via-indigo-50/60 to-white/95 backdrop-blur-md",
    borderColor: "border-blue-200/80 hover:border-blue-400",
    topStripe: "from-blue-600 via-indigo-500 to-blue-400",
    labelColor: "text-blue-900 font-bold",
    valueColor: "text-[#0F1E36]",
    iconBg: "bg-white/90 backdrop-blur-sm",
    iconText: "text-blue-600",
    iconBorder: "border-blue-200/80 shadow-2xs",
    footerBorder: "border-blue-100/80",
    footerText: "text-slate-600",
    dotBg: "bg-blue-600 shadow-[0_0_6px_rgba(37,99,235,0.6)]",
    cardShadow: "shadow-[0_6px_20px_rgba(59,130,246,0.08)] hover:shadow-[0_10px_26px_rgba(59,130,246,0.16)]",
  },
  info: {
    cardBg: "bg-gradient-to-br from-sky-100/95 via-cyan-50/60 to-white/95 backdrop-blur-md",
    borderColor: "border-sky-200/80 hover:border-sky-400",
    topStripe: "from-cyan-500 via-sky-500 to-blue-400",
    labelColor: "text-sky-900 font-bold",
    valueColor: "text-[#0F1E36]",
    iconBg: "bg-white/90 backdrop-blur-sm",
    iconText: "text-sky-600",
    iconBorder: "border-sky-200/80 shadow-2xs",
    footerBorder: "border-sky-100/80",
    footerText: "text-slate-600",
    dotBg: "bg-sky-500 shadow-[0_0_6px_rgba(14,165,233,0.6)]",
    cardShadow: "shadow-[0_6px_20px_rgba(14,165,233,0.08)] hover:shadow-[0_10px_26px_rgba(14,165,233,0.16)]",
  },
  warning: {
    cardBg: "bg-gradient-to-br from-amber-100/95 via-orange-50/60 to-white/95 backdrop-blur-md",
    borderColor: "border-amber-200/80 hover:border-amber-400",
    topStripe: "from-amber-500 via-orange-400 to-amber-600",
    labelColor: "text-amber-900 font-bold",
    valueColor: "text-[#0F1E36]",
    iconBg: "bg-white/90 backdrop-blur-sm",
    iconText: "text-amber-600",
    iconBorder: "border-amber-200/80 shadow-2xs",
    footerBorder: "border-amber-100/80",
    footerText: "text-slate-600",
    dotBg: "bg-amber-500 shadow-[0_0_6px_rgba(245,158,11,0.6)]",
    cardShadow: "shadow-[0_6px_20px_rgba(245,158,11,0.08)] hover:shadow-[0_10px_26px_rgba(245,158,11,0.16)]",
  },
  purple: {
    cardBg: "bg-gradient-to-br from-purple-100/95 via-violet-50/60 to-white/95 backdrop-blur-md",
    borderColor: "border-purple-200/80 hover:border-purple-400",
    topStripe: "from-purple-500 via-violet-400 to-indigo-500",
    labelColor: "text-purple-900 font-bold",
    valueColor: "text-[#0F1E36]",
    iconBg: "bg-white/90 backdrop-blur-sm",
    iconText: "text-purple-600",
    iconBorder: "border-purple-200/80 shadow-2xs",
    footerBorder: "border-purple-100/80",
    footerText: "text-slate-600",
    dotBg: "bg-purple-500 shadow-[0_0_6px_rgba(168,85,247,0.6)]",
    cardShadow: "shadow-[0_6px_20px_rgba(168,85,247,0.08)] hover:shadow-[0_10px_26px_rgba(168,85,247,0.16)]",
  },
  success: {
    cardBg: "bg-gradient-to-br from-emerald-100/95 via-teal-50/60 to-white/95 backdrop-blur-md",
    borderColor: "border-emerald-200/80 hover:border-emerald-400",
    topStripe: "from-emerald-500 via-teal-400 to-green-500",
    labelColor: "text-emerald-900 font-bold",
    valueColor: "text-[#0F1E36]",
    iconBg: "bg-white/90 backdrop-blur-sm",
    iconText: "text-emerald-600",
    iconBorder: "border-emerald-200/80 shadow-2xs",
    footerBorder: "border-emerald-100/80",
    footerText: "text-slate-600",
    dotBg: "bg-emerald-500 shadow-[0_0_6px_rgba(16,185,129,0.6)]",
    cardShadow: "shadow-[0_6px_20px_rgba(16,185,129,0.08)] hover:shadow-[0_10px_26px_rgba(16,185,129,0.16)]",
  },
  gold: {
    cardBg: "bg-gradient-to-br from-amber-100/95 via-[#FDF9F2] to-white/95 backdrop-blur-md",
    borderColor: "border-[#E8C98F]/80 hover:border-[#C59A52]",
    topStripe: "from-[#C59A52] via-[#E8C98F] to-[#C59A52]",
    labelColor: "text-[#8C6420] font-bold",
    valueColor: "text-[#0F1E36]",
    iconBg: "bg-white/90 backdrop-blur-sm",
    iconText: "text-[#C59A52]",
    iconBorder: "border-[#F3E2C4] shadow-2xs",
    footerBorder: "border-[#F3E2C4]/80",
    footerText: "text-slate-600",
    dotBg: "bg-[#C59A52] shadow-[0_0_6px_rgba(197,154,82,0.6)]",
    cardShadow: "shadow-[0_6px_20px_rgba(197,154,82,0.08)] hover:shadow-[0_10px_26px_rgba(197,154,82,0.16)]",
  },
  danger: {
    cardBg: "bg-gradient-to-br from-rose-100/95 via-red-50/60 to-white/95 backdrop-blur-md",
    borderColor: "border-rose-200/80 hover:border-rose-400",
    topStripe: "from-[#D94D50] via-rose-400 to-[#D94D50]",
    labelColor: "text-rose-900 font-bold",
    valueColor: "text-[#0F1E36]",
    iconBg: "bg-white/90 backdrop-blur-sm",
    iconText: "text-[#D94D50]",
    iconBorder: "border-rose-200 shadow-2xs",
    footerBorder: "border-rose-100/80",
    footerText: "text-slate-600",
    dotBg: "bg-[#D94D50] shadow-[0_0_6px_rgba(217,77,80,0.6)]",
    cardShadow: "shadow-[0_6px_20px_rgba(217,77,80,0.08)] hover:shadow-[0_10px_26px_rgba(217,77,80,0.16)]",
  },
  default: {
    cardBg: "bg-gradient-to-br from-slate-100/95 via-slate-50/60 to-white/95 backdrop-blur-md",
    borderColor: "border-slate-200 hover:border-slate-400",
    topStripe: "from-slate-400 via-slate-300 to-slate-200",
    labelColor: "text-slate-800 font-bold",
    valueColor: "text-[#0F1E36]",
    iconBg: "bg-white/90 backdrop-blur-sm",
    iconText: "text-slate-600",
    iconBorder: "border-slate-200 shadow-2xs",
    footerBorder: "border-slate-100",
    footerText: "text-slate-600",
    dotBg: "bg-slate-400 shadow-[0_0_6px_rgba(148,163,184,0.6)]",
    cardShadow: "shadow-[0_6px_20px_rgba(15,23,42,0.05)] hover:shadow-[0_10px_26px_rgba(15,23,42,0.1)]",
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
  const accent = toneStyles[tone] ?? toneStyles.default;

  return (
    <div
      onClick={onClick}
      className={`relative overflow-hidden rounded-2xl border ${accent.cardBg} ${accent.borderColor} ${accent.cardShadow} transition-all duration-200 flex flex-col justify-between group ${
        onClick ? "cursor-pointer hover:-translate-y-0.5" : ""
      } ${className}`}
    >
      {/* Top Accent Stripe */}
      <div className={`h-1 w-full bg-gradient-to-r ${accent.topStripe}`} />

      {/* Glass shine gradient overlay */}
      <div className="pointer-events-none absolute inset-0 bg-gradient-to-b from-white/40 via-white/10 to-transparent opacity-70" />

      {/* Decorative subtle watermark circles for dark card */}
      {accent.hasWatermark && (
        <svg
          className="pointer-events-none absolute -right-6 -bottom-6 w-32 h-32 opacity-10 text-white select-none"
          viewBox="0 0 100 100"
          fill="none"
          stroke="currentColor"
        >
          <circle cx="50" cy="50" r="40" strokeWidth="2" strokeDasharray="4 4" />
          <circle cx="50" cy="50" r="28" strokeWidth="1.5" />
          <circle cx="50" cy="50" r="16" strokeWidth="1" />
        </svg>
      )}

      <div className="p-4 sm:p-5 flex-1 flex flex-col justify-between relative z-10">
        <div>
          <div className="flex items-center justify-between gap-3">
            <p className={`text-[11px] font-bold uppercase tracking-wider truncate ${accent.labelColor}`} title={label}>
              {label}
            </p>
            {icon && (
              <div
                className={`h-9 w-9 rounded-xl flex items-center justify-center shrink-0 border transition-transform duration-200 group-hover:scale-105 shadow-2xs ${accent.iconBg} ${accent.iconText} ${accent.iconBorder}`}
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
                className={`inline-flex items-center text-xs font-semibold px-2 py-0.5 rounded-full ${
                  isPositive
                    ? "bg-emerald-50 text-[#16A477] border border-emerald-200"
                    : "bg-rose-50 text-[#D94D50] border border-rose-200"
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

      {/* Gold Lower Accent for Card 1 */}
      {accent.lowerAccent && <div className={`h-0.5 w-full ${accent.lowerAccent}`} />}
    </div>
  );
}
