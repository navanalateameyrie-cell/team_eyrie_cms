import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { searchApi, type SearchHit } from "../api/search";

const kindBadge: Record<SearchHit["kind"], { label: string; tone: string }> = {
  Client: { label: "Client", tone: "bg-brand-50 text-brand-700 border-brand-200" },
  ServiceFile: { label: "Service File", tone: "bg-sky-50 text-sky-700 border-sky-200" },
  TaxRegistration: { label: "Tax Reg", tone: "bg-amber-50 text-amber-800 border-amber-200" },
};

export default function GlobalSearchBox() {
  const nav = useNavigate();
  const [q, setQ] = useState("");
  const [hits, setHits] = useState<SearchHit[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const wrap = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key === "k") {
        e.preventDefault();
        inputRef.current?.focus();
        setOpen(true);
      }
    }
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, []);

  useEffect(() => {
    const term = q.trim();
    if (term.length < 2) {
      setHits([]);
      return;
    }
    const h = setTimeout(async () => {
      setLoading(true);
      try {
        const resp = await searchApi.search(term, { take: 10 });
        setHits(resp.items);
        setOpen(true);
      } catch {
        /* ignore */
      } finally {
        setLoading(false);
      }
    }, 220);
    return () => clearTimeout(h);
  }, [q]);

  useEffect(() => {
    function onClickOutside(e: MouseEvent) {
      if (wrap.current && !wrap.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", onClickOutside);
    return () => document.removeEventListener("mousedown", onClickOutside);
  }, []);

  function pick(h: SearchHit) {
    setOpen(false);
    setQ("");
    const clientId = h.clientId ?? h.id;
    nav(`/admin/clients/${clientId}`);
  }

  return (
    <div ref={wrap} className="relative w-64 md:w-80">
      <div className="relative">
        <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3 text-slate-400">
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>
        <input
          ref={inputRef}
          type="search"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          onFocus={() => hits.length > 0 && setOpen(true)}
          placeholder="Search clients, PAN, GSTIN…"
          className="w-full rounded-lg border border-blue-200/90 bg-white/95 pl-9 pr-14 py-1.5 text-xs text-slate-800 placeholder:text-slate-400 focus:border-blue-500 focus:bg-white focus:outline-none focus:ring-1 focus:ring-blue-500 shadow-2xs transition-colors"
        />
        <kbd className="pointer-events-none absolute right-2 top-1.5 hidden sm:inline-flex items-center rounded border border-blue-200 bg-blue-50/90 px-1.5 text-[10px] text-blue-700 font-mono font-medium">
          ⌘K
        </kbd>
      </div>

      {open && (
        <div className="absolute mt-1.5 w-full md:w-96 rounded-xl border border-slate-200 bg-white shadow-xl z-50 max-h-96 overflow-auto divide-y divide-slate-100">
          {loading && (
            <div className="flex items-center gap-2 px-4 py-3 text-xs text-slate-500">
              <svg className="animate-spin h-3.5 w-3.5 text-brand-500" viewBox="0 0 24 24" fill="none">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
              </svg>
              <span>Searching across practice…</span>
            </div>
          )}
          {!loading && hits.length === 0 && (
            <div className="px-4 py-3 text-xs text-slate-500">No matching records found.</div>
          )}
          {!loading &&
            hits.map((h) => {
              const badge = kindBadge[h.kind];
              return (
                <button
                  key={`${h.kind}:${h.id}`}
                  type="button"
                  onClick={() => pick(h)}
                  className="w-full text-left px-4 py-2.5 hover:bg-slate-50 flex items-start gap-2.5 transition-colors group"
                >
                  <span
                    className={`shrink-0 inline-flex items-center rounded border px-1.5 py-0.5 text-[10px] font-medium ${badge.tone}`}
                  >
                    {badge.label}
                  </span>
                  <div className="min-w-0 flex-1">
                    <div className="text-xs font-semibold text-slate-900 group-hover:text-brand-600 truncate">
                      {h.label}
                    </div>
                    {(h.context || h.code) && (
                      <div className="text-[11px] text-slate-500 truncate">
                        <span className="font-mono text-slate-600 font-medium">{h.code}</span>
                        {h.context && <span> · {h.context}</span>}
                      </div>
                    )}
                  </div>
                  {h.matchedOn && (
                    <span className="text-[10px] font-mono text-slate-400 shrink-0 self-center bg-slate-100 px-1.5 py-0.5 rounded">
                      {h.matchedOn}
                    </span>
                  )}
                </button>
              );
            })}
        </div>
      )}
    </div>
  );
}
