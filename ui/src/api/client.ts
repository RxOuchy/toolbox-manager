import type {
  ApplicationDetailDto,
  ApplicationSummaryDto,
  CreateApplicationRequest,
  CreateParameterRequest,
  ParameterDto,
  RunRequestDto,
  RunStatus,
  TriggerRunRequest,
  UpdateApplicationRequest,
} from "./types";

const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:8080").replace(/\/+$/, "");

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {}),
    },
  });
  if (!res.ok) {
    let detail: string;
    try {
      const body = await res.json();
      detail = body?.error ?? JSON.stringify(body);
    } catch {
      detail = await res.text();
    }
    throw new Error(`${res.status} ${res.statusText}: ${detail}`);
  }
  if (res.status === 204) return undefined as unknown as T;
  return (await res.json()) as T;
}

export const api = {
  // Applications
  listApplications: (includeInactive = false) =>
    request<ApplicationSummaryDto[]>(
      `/api/applications?includeInactive=${includeInactive}`
    ),
  getApplication: (id: string) =>
    request<ApplicationDetailDto>(`/api/applications/${id}`),
  createApplication: (body: CreateApplicationRequest) =>
    request<ApplicationDetailDto>(`/api/applications`, {
      method: "POST",
      body: JSON.stringify(body),
    }),
  updateApplication: (id: string, body: UpdateApplicationRequest) =>
    request<ApplicationDetailDto>(`/api/applications/${id}`, {
      method: "PUT",
      body: JSON.stringify(body),
    }),
  deleteApplication: (id: string) =>
    request<void>(`/api/applications/${id}`, { method: "DELETE" }),

  // Parameters
  addParameter: (appId: string, body: CreateParameterRequest) =>
    request<ParameterDto>(`/api/applications/${appId}/parameters`, {
      method: "POST",
      body: JSON.stringify(body),
    }),
  updateParameter: (id: string, body: CreateParameterRequest) =>
    request<ParameterDto>(`/api/parameters/${id}`, {
      method: "PUT",
      body: JSON.stringify(body),
    }),
  deleteParameter: (id: string) =>
    request<void>(`/api/parameters/${id}`, { method: "DELETE" }),

  // Runs
  triggerRun: (appId: string, body: TriggerRunRequest) =>
    request<RunRequestDto>(`/api/applications/${appId}/run`, {
      method: "POST",
      body: JSON.stringify(body),
    }),
  listRuns: (params?: { applicationId?: string; status?: RunStatus; limit?: number }) => {
    const qs = new URLSearchParams();
    if (params?.applicationId) qs.set("applicationId", params.applicationId);
    if (params?.status) qs.set("status", params.status);
    if (params?.limit) qs.set("limit", String(params.limit));
    const query = qs.toString();
    return request<RunRequestDto[]>(`/api/runs${query ? `?${query}` : ""}`);
  },
  getRun: (id: string) => request<RunRequestDto>(`/api/runs/${id}`),
};
