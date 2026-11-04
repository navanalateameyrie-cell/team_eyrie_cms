import { forwardRef } from "react";
import type { InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from "react";

export interface FormFieldProps {
  label?: string;
  required?: boolean;
  error?: string | null;
  hint?: string | null;
  children: ReactNode;
  className?: string;
}

export function FormField({ label, required, error, hint, children, className = "" }: FormFieldProps) {
  return (
    <div className={`space-y-1.5 ${className}`}>
      {label && (
        <label className="block text-xs font-semibold text-slate-700">
          {label}
          {required && <span className="text-rose-500 ml-1 font-bold">*</span>}
        </label>
      )}
      {children}
      {hint && !error && <p className="text-[11px] text-slate-500">{hint}</p>}
      {error && <p className="text-xs text-rose-600 font-medium">{error}</p>}
    </div>
  );
}

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  error?: boolean;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(({ error, className = "", ...props }, ref) => {
  return (
    <input
      ref={ref}
      className={`block w-full rounded-lg border bg-white px-3 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-offset-1 disabled:bg-slate-50 disabled:text-slate-400 transition-colors shadow-subtle ${error
        ? "border-rose-300 focus:border-rose-500 focus:ring-rose-200"
        : "border-slate-300 hover:border-slate-400 focus:border-navy-700 focus:ring-navy-200"
        } ${className}`}
      {...props}
    />
  );
});

Input.displayName = "Input";

export interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  error?: boolean;
}

export const Select = forwardRef<HTMLSelectElement, SelectProps>(({ error, className = "", children, ...props }, ref) => {
  return (
    <select
      ref={ref}
      className={`block w-full rounded-lg border bg-white px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-offset-1 disabled:bg-slate-50 disabled:text-slate-400 transition-colors shadow-subtle ${error
        ? "border-rose-300 focus:border-rose-500 focus:ring-rose-200"
        : "border-slate-300 hover:border-slate-400 focus:border-navy-700 focus:ring-navy-200"
        } ${className}`}
      {...props}
    >
      {children}
    </select>
  );
});

Select.displayName = "Select";

export interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  error?: boolean;
}

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(({ error, className = "", ...props }, ref) => {
  return (
    <textarea
      ref={ref}
      className={`block w-full rounded-lg border bg-white px-3 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-offset-1 disabled:bg-slate-50 disabled:text-slate-400 transition-colors shadow-subtle ${error
        ? "border-rose-300 focus:border-rose-500 focus:ring-rose-200"
        : "border-slate-300 hover:border-slate-400 focus:border-navy-700 focus:ring-navy-200"
        } ${className}`}
      {...props}
    />
  );
});

Textarea.displayName = "Textarea";
