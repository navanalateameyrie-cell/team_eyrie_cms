import type { HTMLAttributes, ReactNode } from "react";

export function Card({ className = "", children, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={`rounded-xl border border-slate-200/90 bg-white shadow-card transition-shadow ${className}`}
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
  action,
  className = "",
}: {
  title?: ReactNode;
  subtitle?: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
  className?: string;
}) {
  const sub = subtitle ?? description;
  return (
    <div className={`flex items-start justify-between gap-4 border-b border-slate-100 px-5 py-3.5 ${className}`}>
      <div>
        {title && <h3 className="font-semibold text-slate-900 leading-tight">{title}</h3>}
        {sub && <p className="text-xs text-slate-500 mt-0.5">{sub}</p>}
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

