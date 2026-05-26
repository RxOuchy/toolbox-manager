import { Link, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { api } from "../api/client";
import StatusBadge from "../components/StatusBadge";

export default function RunDetail() {
  const { id = "" } = useParams();

  const run = useQuery({
    queryKey: ["run", id],
    queryFn: () => api.getRun(id),
    enabled: !!id,
    refetchInterval: (q) => {
      const status = q.state.data?.status;
      return status && (status === "Queued" || status === "Running") ? 2_000 : false;
    },
  });

  if (run.isLoading) return <p className="text-slate-500">Loading…</p>;
  if (run.isError) return <p className="text-red-700">{(run.error as Error).message}</p>;
  if (!run.data) return null;

  const r = run.data;

  return (
    <div className="space-y-5">
      <header className="flex items-start justify-between">
        <div>
          <h2 className="text-xl font-semibold">Run · {r.applicationName}</h2>
          <p className="text-sm text-slate-500 mt-1">
            Queued {new Date(r.queuedAt).toLocaleString()} by {r.requestedBy}
          </p>
          <Link to={`/applications/${r.applicationId}`} className="text-xs text-brand-700 hover:underline">
            ← Back to application
          </Link>
        </div>
        <div className="text-right space-y-1">
          <StatusBadge status={r.status} />
          {r.exitCode != null && (
            <div className="text-xs text-slate-500 font-mono">exit {r.exitCode}</div>
          )}
        </div>
      </header>

      <section className="bg-white border border-slate-200 rounded p-4">
        <h3 className="font-semibold text-sm text-slate-700 mb-2">Parameter values</h3>
        {Object.keys(r.parameterValues).length === 0 ? (
          <p className="text-sm text-slate-500">No parameters supplied.</p>
        ) : (
          <table className="text-sm w-full">
            <tbody>
              {Object.entries(r.parameterValues).map(([k, v]) => (
                <tr key={k} className="border-t border-slate-100">
                  <td className="px-3 py-1.5 font-mono text-xs w-1/3">{k}</td>
                  <td className="px-3 py-1.5 font-mono text-xs text-slate-700">{v ?? <em className="text-slate-400">null</em>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      {r.errorMessage && (
        <section className="bg-red-50 border border-red-200 rounded p-4">
          <h3 className="font-semibold text-sm text-red-800 mb-1">Error</h3>
          <pre className="text-xs font-mono text-red-900 whitespace-pre-wrap">{r.errorMessage}</pre>
        </section>
      )}

      <OutputBlock label="stdout" content={r.stdout} />
      <OutputBlock label="stderr" content={r.stderr} />
    </div>
  );
}

function OutputBlock({ label, content }: { label: string; content: string | null }) {
  return (
    <section className="bg-white border border-slate-200 rounded">
      <header className="px-4 py-2 border-b text-xs font-semibold text-slate-600 uppercase">{label}</header>
      <pre className="px-4 py-3 text-xs font-mono whitespace-pre-wrap text-slate-800 max-h-96 overflow-auto">
        {content && content.length > 0 ? content : <span className="text-slate-400">— empty —</span>}
      </pre>
    </section>
  );
}
