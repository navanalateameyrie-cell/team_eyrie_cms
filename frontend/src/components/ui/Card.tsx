import type { HTMLAttributes, ReactNode } from "react";

export function Card({ className = "", children, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={`rounded-2xl border border-[#E0E6EF] bg-white shadow-[0_2px_12px_rgba(16,33,58,0.04)] transition-all duration-200 ${className}`}
      {...props}
    >
      {children}
    </div>
  );
}

export function CardHeader({
  title,
  subtitle,
  description,
  icon,
  action,
  variant = "light",
  className = "",
}: {
  title?: ReactNode;
  subtitle?: ReactNode;
  description?: ReactNode;
  icon?: ReactNode;
  action?: ReactNode;
  variant?: "light" | "navy";
  className?: string;
}) {
  const sub = subtitle ?? description;
  const isNavy = variant === "navy";

  return (
    <div
      className={`flex items-start justify-between gap-4 px-5 py-3.5 ${isNavy
        ? "bg-[#102747] text-white border-b border-[#19385F] rounded-t-2xl"
        : "border-b border-slate-100"
        } ${className}`}
    >
      <div className="flex items-center gap-3 min-w-0">
        {icon && (
          <div
            className={`h-8 w-8 rounded-xl flex items-center justify-center shrink-0 shadow-2xs ${isNavy
              ? "bg-white/10 text-[#FDBA74] border border-[#C86A24]/40"
              : "bg-[#C86A24]/10 text-[#C86A24] border border-[#C86A24]/30"
              }`}
          >
            {icon}
          </div>
        )}
        <div className="min-w-0">
          {title && (
            <h3 className={`font-bold leading-tight ${isNavy ? "text-white text-sm tracking-tight" : "text-[#102747] text-sm"}`}>
              {title}
            </h3>
          )}
          {sub && (
            <p className={`text-xs mt-0.5 leading-tight ${isNavy ? "text-slate-300" : "text-slate-500"}`}>
              {sub}
            </p>
          )}
        </div>
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  );
}

export function CardContent({ className = "", children, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={`p-5 ${className}`} {...props}>
      {children}
    </div>
  );
}

export function CardFooter({ className = "", children, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={`border-t border-slate-100 bg-slate-50/50 px-5 py-3 rounded-b-xl ${className}`} {...props}>
      {children}
    </div>
  );
}

