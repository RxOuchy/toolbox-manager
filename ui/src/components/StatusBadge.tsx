import type { RunStatus } from "../api/types";

const tone: Record<RunStatus, string> = {
  Queued:    "bg-slate-200 text-slate-700",
  Running:   "bg-blue-100 text-blue-800",
  Succeeded: "bg-green-100 text-green-800",
  Failed:    "bg-red-100 text-red-800",
  Cancelled: "bg-amber-100 text-amber-800",
  TimedOut:  "bg-orange-100 text-orange-800",
};

export default function StatusBadge({ status }: { status: RunStatus }) {
  return <span className={`badge ${tone[status]}`}>{status}</span>;
}
