import { useState, FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import type { ApplicationDetailDto } from "../api/types";
import { api } from "../api/client";

interface Props {
  application: ApplicationDetailDto;
  onClose: () => void;
  defaultEmail?: string;
}

export default function RunModal({ application, onClose, defaultEmail }: Props) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const initialValues: Record<string, string> = {};
  for (const p of application.parameters) {
    initialValues[p.parameterName] = p.defaultValue ?? (p.parameterType === "Flag" ? "false" : "");
  }
  const [values, setValues] = useState(initialValues);
  const [email, setEmail] = useState(defaultEmail ?? "");

  const mutation = useMutation({
    mutationFn: () =>
      api.triggerRun(application.id, {
        requestedBy: email,
        parameterValues: values,
      }),
    onSuccess: (run) => {
      queryClient.invalidateQueries({ queryKey: ["runs"] });
      navigate(`/runs/${run.id}`);
    },
  });

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    mutation.mutate();
  };

  return (
    <div className="fixed inset-0 bg-slate-900/40 flex items-center justify-center p-4 z-50">
      <div className="bg-white w-full max-w-lg rounded-lg shadow-lg">
        <header className="px-5 py-3 border-b flex items-center justify-between">
          <h2 className="font-semibold">Run · {application.name}</h2>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-700" aria-label="Close">×</button>
        </header>
        <form onSubmit={onSubmit} className="px-5 py-4 space-y-4">
          <div>
            <label className="field-label">Requested by (email)</label>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="field"
              placeholder="you@listrak.com"
            />
          </div>

          {application.parameters.length === 0 && (
            <p className="text-sm text-slate-500">No parameters defined — clicking Run will execute the app with no arguments.</p>
          )}

          {application.parameters
            .slice()
            .sort((a, b) => a.displayOrder - b.displayOrder)
            .map((p) => (
              <div key={p.id}>
                <label className="field-label">
                  {p.displayLabel}
                  {p.isRequired && <span className="text-red-500 ml-1">*</span>}
                  <span className="ml-2 font-mono text-[10px] text-slate-400">{p.parameterName}</span>
                </label>
                {renderInput(p, values, setValues)}
                {p.description && <p className="text-xs text-slate-500 mt-1">{p.description}</p>}
              </div>
            ))}

          {mutation.isError && (
            <p className="text-sm text-red-700 bg-red-50 border border-red-200 rounded p-2">
              {(mutation.error as Error).message}
            </p>
          )}

          <div className="flex justify-end gap-2 pt-2">
            <button type="button" className="btn-secondary" onClick={onClose} disabled={mutation.isPending}>
              Cancel
            </button>
            <button type="submit" className="btn-primary" disabled={mutation.isPending}>
              {mutation.isPending ? "Queueing…" : "Run"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function renderInput(
  p: ApplicationDetailDto["parameters"][number],
  values: Record<string, string>,
  setValues: (v: Record<string, string>) => void
) {
  const set = (v: string) => setValues({ ...values, [p.parameterName]: v });

  switch (p.parameterType) {
    case "Flag":
    case "Boolean":
      return (
        <label className="inline-flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={values[p.parameterName] === "true"}
            onChange={(e) => set(e.target.checked ? "true" : "false")}
          />
          <span className="text-slate-600">Enabled</span>
        </label>
      );
    case "Number":
      return (
        <input
          type="number"
          required={p.isRequired}
          value={values[p.parameterName] ?? ""}
          onChange={(e) => set(e.target.value)}
          className="field font-mono"
        />
      );
    case "Secret":
      return (
        <input
          type="password"
          required={p.isRequired}
          value={values[p.parameterName] ?? ""}
          onChange={(e) => set(e.target.value)}
          className="field font-mono"
          autoComplete="off"
        />
      );
    default:
      return (
        <input
          type="text"
          required={p.isRequired}
          value={values[p.parameterName] ?? ""}
          onChange={(e) => set(e.target.value)}
          className="field font-mono"
        />
      );
  }
}
