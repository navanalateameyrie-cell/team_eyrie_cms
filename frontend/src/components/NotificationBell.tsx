import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { apiFetch } from "../api/client";
import { formatDate } from "../utils/formatters";

interface NotificationItem {
  id: string;
  kind: string;
  title: string;
  body: string;
  link: string | null;
  createdAtUtc: string;
  readAtUtc: string | null;
}

interface NotificationResponse {
  unreadCount: number;
  items: NotificationItem[];
}

const POLL_INTERVAL_MS = 60_000;

export default function NotificationBell() {
  const [data, setData] = useState<NotificationResponse | null>(null);
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  async function reload() {
    try {
      setData(await apiFetch<NotificationResponse>("/api/v1/notifications?take=20"));
    } catch {
      /* silent retry */
    }
  }

  useEffect(() => {
    void reload();
    const timer = window.setInterval(reload, POLL_INTERVAL_MS);
    return () => window.clearInterval(timer);
  }, []);

  useEffect(() => {
    function onClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", onClickOutside);
    return () => document.removeEventListener("mousedown", onClickOutside);
  }, []);

  async function markRead(id: string) {
    try {
      await apiFetch(`/api/v1/notifications/${id}/read`, { method: "POST" });
    } catch {
      /* keep UI optimistic */
    }
    await reload();
  }

  async function markAllRead() {
    try {
      await apiFetch("/api/v1/notifications/mark-all-read", { method: "POST" });
    } catch {
      /* ignore */
    }
    await reload();
  }

  const unread = data?.unreadCount ?? 0;

  return (
    <div className="relative" ref={ref}>
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        className="relative rounded-lg p-2 text-slate-600 hover:text-navy-950 hover:bg-blue-200/50 transition-colors focus:outline-none focus:ring-2 focus:ring-blue-400"
        aria-label="Practice Notifications"
      >
        <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={1.75}
            d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"
          />
        </svg>
        {unread > 0 && (
          <span className="absolute top-1 right-1 inline-flex items-center justify-center rounded-full bg-blue-600 text-white text-[10px] font-bold min-w-[16px] h-[16px] px-1 ring-2 ring-white shadow-2xs">
            {unread > 99 ? "99+" : unread}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 top-full mt-2 w-80 sm:w-96 rounded-xl border border-slate-200 bg-white shadow-2xl z-50 overflow-hidden divide-y divide-slate-100">
          <header className="flex items-center justify-between px-4 py-3 bg-slate-50/80">
            <div className="flex items-center gap-2">
              <span className="text-xs font-bold text-slate-900 tracking-wide uppercase">Notifications</span>
              {unread > 0 && (
                <span className="rounded-full bg-brand-100 text-brand-700 px-2 py-0.5 text-[10px] font-semibold">
                  {unread} new
                </span>
              )}
            </div>
            {unread > 0 && (
              <button
                type="button"
                onClick={markAllRead}
                className="text-xs font-medium text-brand-600 hover:text-brand-700 transition-colors"
              >
                Mark all read
              </button>
            )}
          </header>

          <ul className="max-h-96 overflow-y-auto divide-y divide-slate-100">
            {(data?.items ?? []).length === 0 ? (
              <li className="py-8 text-center text-xs text-slate-400">No new notifications.</li>
            ) : (
              (data?.items ?? []).map((n) => (
                <li
                  key={n.id}
                  className={`p-3.5 transition-colors ${n.readAtUtc ? "bg-white hover:bg-slate-50" : "bg-brand-50/40 hover:bg-brand-50/60"}`}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-1.5">
                        {!n.readAtUtc && <span className="h-1.5 w-1.5 rounded-full bg-brand-500 shrink-0" />}
                        <p className={`text-xs ${n.readAtUtc ? "font-medium text-slate-800" : "font-semibold text-slate-900"}`}>
                          {n.title}
                        </p>
                      </div>
                      <p className="mt-1 text-xs text-slate-600 line-clamp-2 leading-relaxed">{n.body}</p>
                      <div className="mt-1.5 flex items-center gap-3 text-[11px] text-slate-400">
                        <span>{formatDate(n.createdAtUtc)}</span>
                        {n.link && (
                          <Link
                            to={n.link}
                            onClick={() => {
                              if (!n.readAtUtc) void markRead(n.id);
                              setOpen(false);
                            }}
                            className="font-medium text-brand-600 hover:underline"
                          >
                            View details →
                          </Link>
                        )}
                      </div>
                    </div>
                    {!n.readAtUtc && (
                      <button
                        type="button"
                        onClick={() => void markRead(n.id)}
                        className="text-[10px] font-medium text-slate-400 hover:text-slate-600 shrink-0"
                        title="Mark as read"
                      >
                        ✓
                      </button>
                    )}
                  </div>
                </li>
              ))
            )}
          </ul>
        </div>
      )}
    </div>
  );
}
