import { useEffect, useMemo, useState } from "react";
import { apiFetch } from "../../api/client";
import { useAuth } from "../../auth/AuthContext";
import {
  PageHeader,
  Card,
  CardHeader,
  CardContent,
  Button,
  Badge,
  Input,
} from "../../components/ui";

interface Entry {
  id: string;
  staffId: string;
  staffName: string;
  engagementId: string;
  clientName: string;
  serviceCode: string;
  financialYear: string;
  periodLabel: string | null;
  workDate: string;
  minutes: number;
  billable: boolean;
  notes: string | null;
  isApproved: boolean;
  version: number;
}

function mondayOf(d: Date): Date {
  const copy = new Date(d);
  const dow = copy.getDay();
  const delta = dow === 0 ? 6 : dow - 1;
  copy.setDate(copy.getDate() - delta);
  copy.setHours(0, 0, 0, 0);
  return copy;
}

function iso(d: Date): string {
  return d.toISOString().slice(0, 10);
}

function addDays(d: Date, n: number): Date {
  const c = new Date(d);
  c.setDate(c.getDate() + n);
  return c;
}

function formatMinutes(mins: number): string {
  const h = Math.floor(mins / 60);
  const m = mins % 60;
  if (h === 0) return `${m}m`;
  if (m === 0) return `${h}h`;
  return `${h}h ${m}m`;
}

export default function TimesheetPage() {
  const { hasPermission } = useAuth();
  const canApprove = hasPermission("timesheet.approve");
  const [weekStart, setWeekStart] = useState<Date>(() => mondayOf(new Date()));
  const [entries, setEntries] = useState<Entry[]>([]);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    try {
      const rows = await apiFetch<Entry[]>(`/api/v1/timesheet?weekStart=${iso(weekStart)}`);
      setEntries(rows);
    } catch (e) {
      setError(String(e));
    }
  }

  useEffect(() => {
    void load();
  }, [weekStart]);

  const days = useMemo(() => Array.from({ length: 7 }, (_, i) => addDays(weekStart, i)), [weekStart]);

  const rows = useMemo(() => {
    const map = new Map<
      string,
      {
        key: string;
        label: string;
        staffId: string;
        staffName: string;
        engagementId: string;
        cells: Map<string, Entry>;
      }
    >();
    for (const e of entries) {
      const key = `${e.engagementId}:${e.staffId}`;
      if (!map.has(key)) {
        map.set(key, {
          key,
          label: `${e.clientName} · ${e.serviceCode} · ${e.financialYear}${e.periodLabel ? "/" + e.periodLabel : ""}`,
          staffId: e.staffId,
          staffName: e.staffName,
          engagementId: e.engagementId,
          cells: new Map(),
        });
      }
      map.get(key)!.cells.set(e.workDate, e);
    }
    return [...map.values()].sort((a, b) => a.label.localeCompare(b.label));
  }, [entries]);

  async function logMinutes(engagementId: string, staffId: string, workDate: string, minutesStr: string) {
    const minutes = Number(minutesStr);
    if (!minutes || minutes <= 0) return;
    try {
      await apiFetch("/api/v1/timesheet", {
        method: "POST",
        body: JSON.stringify({ engagementId, staffId, workDate, minutes, billable: true, notes: null }),
      });
      await load();
    } catch (e) {
      setError(String(e));
    }
  }

  async function editMinutes(id: string, minutesStr: string, billable: boolean, notes: string | null) {
    const minutes = Number(minutesStr);
    try {
      await apiFetch(`/api/v1/timesheet/${id}`, {
        method: "PUT",
        body: JSON.stringify({ minutes, billable, notes }),
      });
      await load();
    } catch (e) {
      setError(String(e));
    }
  }

  async function approve(id: string, revert: boolean) {
    try {
      await apiFetch(`/api/v1/timesheet/${id}/${revert ? "unapprove" : "approve"}`, { method: "POST" });
      await load();
    } catch (e) {
      setError(String(e));
    }
  }

  const weekEnd = addDays(weekStart, 6);
  const totalWeekMinutes = entries.reduce((s, e) => s + e.minutes, 0);

  return (
    <div className="space-y-6">
      <PageHeader
        breadcrumbs={[
          { label: "Operations" },
          { label: "Timesheets" },
        ]}
        title="Weekly Time & Engagement Ledger"
        subtitle={`Period: ${iso(weekStart)} through ${iso(weekEnd)} · Logged: ${formatMinutes(totalWeekMinutes)}`}
        actions={
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setWeekStart(addDays(weekStart, -7))}
            >
              ← Previous Week
            </Button>
            <Input
              type="date"
              value={iso(weekStart)}
              onChange={(e) => setWeekStart(mondayOf(new Date(e.target.value)))}
              className="py-1 text-xs w-auto font-mono"
            />
            <Button
              variant="outline"
              size="sm"
              onClick={() => setWeekStart(addDays(weekStart, 7))}
            >
              Next Week →
            </Button>
          </div>
        }
      />

      {error && (
        <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-xs text-red-700">
          {error}
        </div>
      )}

      <Card>
        <CardHeader
          title="Daily Engagement Work Hours"
          subtitle="Input billable minutes directly into daily columns. Approved entries are locked."
        />
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50 font-medium text-slate-600">
              <tr>
                <th className="text-left px-4 py-3">Engagement Deliverable · Staff</th>
                {days.map((d) => {
                  const isToday = iso(d) === iso(new Date());
                  return (
                    <th
                      key={iso(d)}
                      className={`text-right px-2 py-3 ${isToday ? "bg-brand-50/70 text-brand-900 font-bold" : ""
                        }`}
                    >
                      <div>{d.toLocaleDateString(undefined, { weekday: "short" })}</div>
                      <div className="font-mono text-[11px] text-slate-400 font-normal">
                        {d.toLocaleDateString(undefined, { month: "numeric", day: "numeric" })}
                      </div>
                    </th>
                  );
                })}
                <th className="text-right px-4 py-3 font-semibold text-slate-800">Weekly Total</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {rows.map((r) => {
                const total = [...r.cells.values()].reduce((s, e) => s + e.minutes, 0);
                return (
                  <tr key={r.key} className="hover:bg-slate-50/50 transition-colors">
                    <td className="px-4 py-3">
                      <div className="font-semibold text-slate-900">{r.label}</div>
                      <div className="text-[11px] text-slate-500 font-medium mt-0.5">{r.staffName}</div>
                    </td>
                    {days.map((d) => {
                      const key = iso(d);
                      const e = r.cells.get(key);
                      return (
                        <td key={key} className="px-2 py-2 text-right">
                          {e ? (
                            <input
                              type="number"
                              min={0}
                              max={1440}
                              defaultValue={e.minutes}
                              disabled={e.isApproved}
                              onBlur={(ev) => editMinutes(e.id, ev.target.value, e.billable, e.notes)}
                              className={`w-16 rounded-md border px-2 py-1 text-right text-xs font-mono transition-colors ${e.isApproved
                                ? "bg-slate-100 border-slate-200 text-slate-500 cursor-not-allowed"
                                : "border-slate-300 hover:border-slate-400 focus:border-brand-500 focus:ring-1 focus:ring-brand-500"
                                }`}
                              title={
                                e.isApproved
                                  ? "Approved by Manager"
                                  : e.notes
                                    ? `Notes: ${e.notes}`
                                    : "Click and edit minutes"
                              }
                            />
                          ) : (
                            <input
                              type="number"
                              min={0}
                              placeholder="–"
                              onBlur={(ev) =>
                                ev.target.value &&
                                logMinutes(r.engagementId, r.staffId, key, ev.target.value).then(
                                  () => (ev.target.value = "")
                                )
                              }
                              className="w-16 rounded-md border border-slate-200 px-2 py-1 text-right text-xs font-mono text-slate-400 placeholder:text-slate-300 hover:border-slate-300 focus:border-brand-500"
                            />
                          )}
                        </td>
                      );
                    })}
                    <td className="px-4 py-3 text-right font-mono font-bold text-slate-900 whitespace-nowrap">
                      {formatMinutes(total)}
                    </td>
                  </tr>
                );
              })}
              {rows.length === 0 && (
                <tr>
                  <td colSpan={9} className="py-12 text-center text-slate-400 italic">
                    No time logged yet for this week. Enter time in minutes in any cell to start tracking.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </Card>

      {canApprove && entries.some((e) => !e.isApproved) && (
        <Card>
          <CardHeader
            title="Timesheet Approval Queue"
            subtitle="Review unapproved staff log entries for this week."
          />
          <CardContent>
            <div className="divide-y divide-slate-100">
              {entries
                .filter((e) => !e.isApproved)
                .map((e) => (
                  <div key={e.id} className="py-2.5 flex items-center justify-between text-xs">
                    <div>
                      <span className="font-semibold text-slate-800">{e.staffName}</span>
                      <span className="text-slate-400 mx-2">·</span>
                      <span className="text-slate-700">{e.clientName}</span>
                      <span className="text-slate-400 mx-1.5">({e.serviceCode})</span>
                      <span className="text-slate-400 mx-2">·</span>
                      <span className="font-mono text-slate-500">{e.workDate}</span>
                      <span className="text-slate-400 mx-2">·</span>
                      <Badge tone="warning" size="sm">
                        {formatMinutes(e.minutes)}
                      </Badge>
                    </div>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => approve(e.id, false)}
                      className="text-emerald-700 border-emerald-300 hover:bg-emerald-50 py-0.5 text-xs"
                    >
                      Approve Time
                    </Button>
                  </div>
                ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
