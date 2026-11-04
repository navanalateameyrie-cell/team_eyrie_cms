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
  labelColor: string;
  valueColor: string;
  iconBg: string;
  iconText: string;
  iconBorder: string;
  footerBorder: string;
  footerText: string;
  dotBg: string;
  cardShadow: string;
}

const toneStyles: Record<string, ToneStyle> = {
  navy: {
    cardBg: "bg-[#102747]",
    borderColor: "border-[#142B49] hover:border-[#C49A52]/50",
    topStripe: "from-[#C49A52] via-[#E4C48A] to-[#C49A52]",
    labelColor: "text-[#E4C48A]",
    valueColor: "text-white",
    iconBg: "bg-white/10",
    iconText: "text-[#C49A52]",
    iconBorder: "border-[#C49A52]/30",
    footerBorder: "border-white/10",
    footerText: "text-slate-300",
    dotBg: "bg-[#C49A52] shadow-[0_0_8px_rgba(196,154,82,0.8)]",
    cardShadow: "shadow-sm hover:shadow-lg shadow-navy-950/20",
  },
  brand: {
    cardBg: "bg-[#102747]",
    borderColor: "border-[#142B49] hover:border-[#C49A52]/50",
    topStripe: "from-[#C49A52] via-[#E4C48A] to-[#C49A52]",
    labelColor: "text-[#E4C48A]",
    valueColor: "text-white",
    iconBg: "bg-white/10",
    iconText: "text-[#C49A52]",
    iconBorder: "border-[#C49A52]/30",
    footerBorder: "border-white/10",
    footerText: "text-slate-300",
    dotBg: "bg-[#C49A52] shadow-[0_0_8px_rgba(196,154,82,0.8)]",
    cardShadow: "shadow-sm hover:shadow-lg shadow-navy-950/20",
  },
  info: {
    cardBg: "bg-white",
    borderColor: "border-[#E3E8F0] hover:border-sky-300",
    topStripe: "from-[#328CE0] via-sky-400 to-sky-300",
    labelColor: "text-[#728096]",
    valueColor: "text-[#172A45]",
    iconBg: "bg-sky-50",
    iconText: "text-[#328CE0]",
    iconBorder: "border-sky-100",
    footerBorder: "border-slate-100",
    footerText: "text-[#728096]",
    dotBg: "bg-[#328CE0]",
    cardShadow: "shadow-xs hover:shadow-md",
  },
  warning: {
    cardBg: "bg-white",
    borderColor: "border-[#E3E8F0] hover:border-amber-300",
    topStripe: "from-[#D49A43] via-amber-400 to-amber-300",
    labelColor: "text-[#728096]",
    valueColor: "text-[#172A45]",
    iconBg: "bg-amber-50",
    iconText: "text-[#D49A43]",
    iconBorder: "border-amber-100",
    footerBorder: "border-slate-100",
    footerText: "text-[#728096]",
    dotBg: "bg-[#D49A43]",
    cardShadow: "shadow-xs hover:shadow-md",
  },
  purple: {
    cardBg: "bg-white",
    borderColor: "border-[#E3E8F0] hover:border-purple-300",
    topStripe: "from-purple-500 via-indigo-400 to-purple-300",
    labelColor: "text-[#728096]",
    valueColor: "text-[#172A45]",
    iconBg: "bg-purple-50",
    iconText: "text-purple-600",
    iconBorder: "border-purple-100",
    footerBorder: "border-slate-100",
    footerText: "text-[#728096]",
    dotBg: "bg-purple-500",
    cardShadow: "shadow-xs hover:shadow-md",
  },
  success: {
    cardBg: "bg-white",
    borderColor: "border-[#E3E8F0] hover:border-emerald-300",
    topStripe: "from-[#1AA879] via-emerald-400 to-emerald-300",
    labelColor: "text-[#728096]",
    valueColor: "text-[#172A45]",
    iconBg: "bg-emerald-50",
    iconText: "text-[#1AA879]",
    iconBorder: "border-emerald-100",
    footerBorder: "border-slate-100",
    footerText: "text-[#728096]",
    dotBg: "bg-[#1AA879]",
    cardShadow: "shadow-xs hover:shadow-md",
  },
  gold: {
    cardBg: "bg-white",
    borderColor: "border-[#E3E8F0] hover:border-amber-300",
    topStripe: "from-[#C49A52] via-[#E4C48A] to-[#C49A52]",
    labelColor: "text-[#728096]",
    valueColor: "text-[#172A45]",
    iconBg: "bg-amber-50",
    iconText: "text-[#C49A52]",
    iconBorder: "border-amber-200",
    footerBorder: "border-slate-100",
    footerText: "text-[#728096]",
    dotBg: "bg-[#C49A52]",
    cardShadow: "shadow-xs hover:shadow-md",
  },
  danger: {
    cardBg: "bg-white",
    borderColor: "border-[#E3E8F0] hover:border-rose-300",
    topStripe: "from-[#DC5151] via-rose-400 to-rose-300",
    labelColor: "text-[#728096]",
    valueColor: "text-[#172A45]",
    iconBg: "bg-rose-50",
    iconText: "text-[#DC5151]",
    iconBorder: "border-rose-100",
    footerBorder: "border-slate-100",
    footerText: "text-[#728096]",
    dotBg: "bg-[#DC5151]",
    cardShadow: "shadow-xs hover:shadow-md",
  },
  default: {
    cardBg: "bg-white",
    borderColor: "border-[#E3E8F0] hover:border-slate-300",
    topStripe: "from-slate-400 via-slate-300 to-slate-200",
    labelColor: "text-[#728096]",
    valueColor: "text-[#172A45]",
    iconBg: "bg-slate-50",
    iconText: "text-slate-600",
    iconBorder: "border-slate-200",
    footerBorder: "border-slate-100",
    footerText: "text-[#728096]",
    dotBg: "bg-slate-400",
    cardShadow: "shadow-xs hover:shadow-md",
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

      <div className="p-4 sm:p-5 flex-1 flex flex-col justify-between">
        <div>
          <div className="flex items-center justify-between gap-3">
            <p className={`text-[11px] font-bold uppercase tracking-wider truncate ${accent.labelColor}`} title={label}>
              {label}
            </p>
            {icon && (
              <div
                className={`h-8 w-8 rounded-lg flex items-center justify-center shrink-0 border transition-transform duration-200 group-hover:scale-105 ${accent.iconBg} ${accent.iconText} ${accent.iconBorder}`}
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
                    ? "bg-emerald-50 text-[#1AA879] border border-emerald-200"
                    : "bg-rose-50 text-[#DC5151] border border-rose-200"
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
