// Shapes mirror the C# DTOs in api/src/ToolboxManager.Api/Dtos/.

export type ParameterType = "String" | "Number" | "Boolean" | "Flag" | "Secret";

export interface ParameterDto {
  id: string;
  applicationId: string;
  parameterName: string;
  displayLabel: string;
  parameterType: ParameterType;
  isRequired: boolean;
  defaultValue: string | null;
  description: string;
  displayOrder: number;
}

export interface ApplicationSummaryDto {
  id: string;
  name: string;
  description: string;
  executablePath: string;
  workingDirectory: string | null;
  timeoutSeconds: number;
  isActive: boolean;
  parameterCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface ApplicationDetailDto {
  id: string;
  name: string;
  description: string;
  executablePath: string;
  workingDirectory: string | null;
  timeoutSeconds: number;
  isActive: boolean;
  parameters: ParameterDto[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateParameterRequest {
  parameterName: string;
  displayLabel: string;
  parameterType: ParameterType;
  isRequired: boolean;
  defaultValue: string | null;
  description: string;
  displayOrder: number;
}

export interface CreateApplicationRequest {
  name: string;
  description: string;
  executablePath: string;
  workingDirectory: string | null;
  timeoutSeconds: number;
  parameters: CreateParameterRequest[];
}

export interface UpdateApplicationRequest {
  name: string;
  description: string;
  executablePath: string;
  workingDirectory: string | null;
  timeoutSeconds: number;
  isActive: boolean;
}

export type RunStatus =
  | "Queued"
  | "Running"
  | "Succeeded"
  | "Failed"
  | "Cancelled"
  | "TimedOut";

export interface RunRequestDto {
  id: string;
  applicationId: string;
  applicationName: string;
  requestedBy: string;
  parameterValues: Record<string, string | null>;
  status: RunStatus;
  exitCode: number | null;
  stdout: string | null;
  stderr: string | null;
  errorMessage: string | null;
  queuedAt: string;
  startedAt: string | null;
  completedAt: string | null;
}

export interface TriggerRunRequest {
  requestedBy: string;
  parameterValues: Record<string, string | null>;
}
