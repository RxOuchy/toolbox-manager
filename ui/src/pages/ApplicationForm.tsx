import { FormEvent, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api/client";
import type { CreateParameterRequest, ParameterType } from "../api/types";

interface Props {
  mode: "create" | "edit";
}

const PARAMETER_TYPES: ParameterType[] = ["String", "Number", "Boolean", "Flag", "Secret"];

interface DraftParameter extends CreateParameterRequest {
  // Only set when editing existing parameters returned from the API.
  id?: string;
}

const blankParam = (order: number): DraftParameter => ({
  parameterName: "",
  displayLabel: "",
  parameterType: "String",
  isRequired: false,
  defaultValue: null,
  description: "",
  displayOrder: order,
});

export default function ApplicationForm({ mode }: Props) {
  const { id } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const existing = useQuery({
    queryKey: ["application", id],
    queryFn: () => api.getApplication(id!),
    enabled: mode === "edit" && !!id,
  });

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [executablePath, setExecutablePath] = useState("");
  const [workingDirectory, setWorkingDirectory] = useState("");
  const [timeoutSeconds, setTimeoutSeconds] = useState(300);
  const [isActive, setIsActive] = useState(true);
  const [parameters, setParameters] = useState<DraftParameter[]>([]);

  useEffect(() => {
    if (mode === "edit" && existing.data) {
      const a = existing.data;
      setName(a.name);
      setDescription(a.description);
      setExecutablePath(a.executablePath);
      setWorkingDirectory(a.workingDirectory ?? "");
      setTimeoutSeconds(a.timeoutSeconds);
      setIsActive(a.isActive);
      setParameters(
        a.parameters
          .slice()
          .sort((x, y) => x.displayOrder - y.displayOrder)
          .map<DraftParameter>((p) => ({
            id: p.id,
            parameterName: p.parameterName,
            displayLabel: p.displayLabel,
            parameterType: p.parameterType,
            isRequired: p.isRequired,
            defaultValue: p.defaultValue,
            description: p.description,
            displayOrder: p.displayOrder,
          }))
      );
    }
  }, [mode, existing.data]);

  const save = useMutation({
    mutationFn: async () => {
      const payload = {
        name,
        description,
        executablePath,
        workingDirectory: workingDirectory.trim() === "" ? null : workingDirectory,
        timeoutSeconds,
      };

      if (mode === "create") {
        return api.createApplication({
          ...payload,
          parameters: parameters.map((p, idx) => ({
            parameterName: p.parameterName,
            displayLabel: p.displayLabel,
            parameterType: p.parameterType,
            isRequired: p.isRequired,
            defaultValue: p.defaultValue,
            description: p.description,
            displayOrder: idx,
          })),
        });
      } else {
        const updated = await api.updateApplication(id!, { ...payload, isActive });

        // Naive sync: PUT every parameter that has an id, POST those that don't.
        // Deletions need to be done from the detail view for now.
        await Promise.all(
          parameters.map((p, idx) => {
            const body: CreateParameterRequest = {
              parameterName: p.parameterName,
              displayLabel: p.displayLabel,
              parameterType: p.parameterType,
              isRequired: p.isRequired,
              defaultValue: p.defaultValue,
              description: p.description,
              displayOrder: idx,
            };
            return p.id ? api.updateParameter(p.id, body) : api.addParameter(id!, body);
          })
        );

        return updated;
      }
    },
    onSuccess: (app) => {
      queryClient.invalidateQueries({ queryKey: ["applications"] });
      queryClient.invalidateQueries({ queryKey: ["application", app.id] });
      navigate(`/applications/${app.id}`);
    },
  });

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    save.mutate();
  };

  const addParameter = () => setParameters([...parameters, blankParam(parameters.length)]);
  const removeParameter = (idx: number) => setParameters(parameters.filter((_, i) => i !== idx));
  const updateParameter = (idx: number, patch: Partial<DraftParameter>) =>
    setParameters(parameters.map((p, i) => (i === idx ? { ...p, ...patch } : p)));

  if (mode === "edit" && existing.isLoading) return <p className="text-slate-500">Loading…</p>;

  return (
    <form onSubmit={onSubmit} className="space-y-6 max-w-3xl">
      <h2 className="text-xl font-semibold">
        {mode === "create" ? "Register a new application" : `Edit · ${name}`}
      </h2>

      <section className="bg-white border border-slate-200 rounded p-5 space-y-4">
        <div>
          <label className="field-label">Name</label>
          <input className="field" value={name} required onChange={(e) => setName(e.target.value)} />
        </div>
        <div>
          <label className="field-label">Description</label>
          <textarea
            className="field"
            value={description}
            rows={2}
            onChange={(e) => setDescription(e.target.value)}
          />
        </div>
        <div>
          <label className="field-label">Executable path (on the polling host)</label>
          <input
            className="field font-mono"
            placeholder="C:\ToolboxApps\MyTool\MyTool.exe"
            value={executablePath}
            required
            onChange={(e) => setExecutablePath(e.target.value)}
          />
        </div>
        <div>
          <label className="field-label">Working directory (optional)</label>
          <input
            className="field font-mono"
            placeholder="C:\ToolboxApps\MyTool"
            value={workingDirectory}
            onChange={(e) => setWorkingDirectory(e.target.value)}
          />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="field-label">Timeout (seconds)</label>
            <input
              type="number"
              min={1}
              max={86400}
              className="field"
              value={timeoutSeconds}
              required
              onChange={(e) => setTimeoutSeconds(Number(e.target.value))}
            />
          </div>
          {mode === "edit" && (
            <div className="flex items-end">
              <label className="inline-flex items-center gap-2 text-sm pb-2">
                <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
                Active
              </label>
            </div>
          )}
        </div>
      </section>

      <section className="bg-white border border-slate-200 rounded p-5 space-y-3">
        <header className="flex items-center justify-between">
          <h3 className="font-semibold text-sm">Parameters</h3>
          <button type="button" className="btn-secondary" onClick={addParameter}>
            + Add parameter
          </button>
        </header>

        {parameters.length === 0 && (
          <p className="text-sm text-slate-500">No parameters yet.</p>
        )}

        {parameters.map((p, idx) => (
          <div key={p.id ?? idx} className="border border-slate-200 rounded p-3 space-y-2">
            <div className="grid grid-cols-2 gap-2">
              <div>
                <label className="field-label">Argument flag</label>
                <input
                  className="field font-mono"
                  placeholder="--message"
                  required
                  value={p.parameterName}
                  onChange={(e) => updateParameter(idx, { parameterName: e.target.value })}
                />
              </div>
              <div>
                <label className="field-label">Display label</label>
                <input
                  className="field"
                  required
                  value={p.displayLabel}
                  onChange={(e) => updateParameter(idx, { displayLabel: e.target.value })}
                />
              </div>
            </div>
            <div className="grid grid-cols-3 gap-2">
              <div>
                <label className="field-label">Type</label>
                <select
                  className="field"
                  value={p.parameterType}
                  onChange={(e) => updateParameter(idx, { parameterType: e.target.value as ParameterType })}
                >
                  {PARAMETER_TYPES.map((t) => (
                    <option key={t} value={t}>{t}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="field-label">Default value</label>
                <input
                  className="field font-mono"
                  value={p.defaultValue ?? ""}
                  onChange={(e) => updateParameter(idx, { defaultValue: e.target.value === "" ? null : e.target.value })}
                />
              </div>
              <div className="flex items-end">
                <label className="inline-flex items-center gap-2 text-sm pb-2">
                  <input
                    type="checkbox"
                    checked={p.isRequired}
                    onChange={(e) => updateParameter(idx, { isRequired: e.target.checked })}
                  />
                  Required
                </label>
              </div>
            </div>
            <div>
              <label className="field-label">Description</label>
              <input
                className="field"
                value={p.description}
                onChange={(e) => updateParameter(idx, { description: e.target.value })}
              />
            </div>
            <div className="text-right">
              <button type="button" className="btn-danger" onClick={() => removeParameter(idx)}>
                Remove
              </button>
            </div>
          </div>
        ))}
      </section>

      {save.isError && (
        <p className="text-sm text-red-700 bg-red-50 border border-red-200 rounded p-2">
          {(save.error as Error).message}
        </p>
      )}

      <div className="flex justify-end gap-2">
        <button type="button" className="btn-secondary" onClick={() => navigate(-1)}>
          Cancel
        </button>
        <button type="submit" className="btn-primary" disabled={save.isPending}>
          {save.isPending ? "Saving…" : "Save"}
        </button>
      </div>
    </form>
  );
}
