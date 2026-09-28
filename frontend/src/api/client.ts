const API_BASE = '/api/v1';

function getCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp('(^| )' + name + '=([^;]+)'));
  return match ? match[2] : null;
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getCookie('Reflow.Token');
  const headers: Record<string, string> = {
    ...options.headers as Record<string, string>,
  };

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  if (!(options.body instanceof FormData)) {
    headers['Content-Type'] = 'application/json';
  }

  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers,
    credentials: 'include',
  });

  if (!res.ok) {
    const error = await res.json().catch(() => ({ error: 'Request failed' }));
    throw new Error(error.error || `HTTP ${res.status}`);
  }

  if (res.status === 204) return undefined as T;
  return res.json();
}

export const auth = {
  register: (data: { email: string; password: string; displayName: string }) =>
    request<{ id: string }>('/auth/register', { method: 'POST', body: JSON.stringify(data) }),
  login: (data: { email: string; password: string }) =>
    request<{ id: string }>('/auth/login', { method: 'POST', body: JSON.stringify(data) }),
  me: () => request<{ id: string; email: string; displayName: string; createdAt: string }>('/auth/me'),
  logout: () => request<void>('/auth/logout', { method: 'POST' }),
};

export interface Pipeline {
  id: string;
  name: string;
  status: number;
  currentVersion: number;
  updatedAt: string;
}

export interface PipelineNode {
  nodeId: string;
  nodeType: string;
  configJson: string;
  label: string | null;
  positionX: number;
  positionY: number;
}

export interface PipelineEdge {
  sourceNodeId: string;
  targetNodeId: string;
}

export interface PipelineDetail {
  id: string;
  name: string;
  status: number;
  currentVersion: number;
  nodes: PipelineNode[];
  edges: PipelineEdge[];
}

export const pipelines = {
  list: () => request<Pipeline[]>('/pipelines'),
  get: (id: string) => request<PipelineDetail>(`/pipelines/${id}`),
  create: async (data: { name: string }) => {
    const res = await request<{ id: string }>('/pipelines', { method: 'POST', body: JSON.stringify(data) });
    return res.id;
  },
  update: (id: string, data: { name?: string; nodes?: PipelineNode[]; edges?: PipelineEdge[] }) =>
    request<{ id: string }>(`/pipelines/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  remove: (id: string) => request<void>(`/pipelines/${id}`, { method: 'DELETE' }),
  validate: (id: string) => request<{ valid: boolean; errors: string[] }>(`/pipelines/${id}/validate`, { method: 'POST' }),
  publish: (id: string) => request<{ version: number }>(`/pipelines/${id}/publish`, { method: 'POST' }),
  archive: (id: string) => request<{ id: string }>(`/pipelines/${id}/archive`, { method: 'POST' }),
  versions: (id: string) => request<Array<{ versionNumber: number; publishedAt: string }>>(`/pipelines/${id}/versions`),
};

export interface NodeDefinition {
  type: string;
  displayName: string;
  category: string;
  description: string;
  inputPorts: string[];
  outputPorts: string[];
}

export const nodeTypes = {
  list: async () => {
    const res = await fetch('/api/node-types', { credentials: 'include' });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    return res.json() as Promise<NodeDefinition[]>;
  },
};

export interface PipelineRun {
  id: string;
  pipelineId: string;
  versionNumber: number;
  status: number;
  triggerKind: string;
  error: string | null;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
}

export interface TaskRun {
  id: string;
  nodeId: string;
  nodeType: string;
  status: number;
  attemptCount: number;
  error: string | null;
  startedAt: string | null;
  completedAt: string | null;
}

export interface TaskDetail {
  id: string;
  runId: string;
  nodeId: string;
  nodeType: string;
  status: number;
  configJson: string;
  outputJson: string | null;
  attemptCount: number;
  error: string | null;
}

export interface TaskAttempt {
  attemptNumber: number;
  status: number;
  error: string | null;
}

export interface VersionDetail {
  versionNumber: number;
  definition: string;
}

export interface RunLog {
  taskRunId: string | null;
  level: string;
  message: string;
  timestamp: string;
}

export const runs = {
  start: async (pipelineId: string) => {
    const res = await request<{ id: string }>(`/pipelines/${pipelineId}/runs`, { method: 'POST' });
    return res.id;
  },
  list: (pipelineId: string) => request<PipelineRun[]>(`/pipelines/${pipelineId}/runs`),
  get: (runId: string) => request<PipelineRun>(`/runs/${runId}`),
  tasks: (runId: string) => request<TaskRun[]>(`/runs/${runId}/tasks`),
  logs: (runId: string) => request<RunLog[]>(`/runs/${runId}/logs`),
  cancel: (runId: string) => request<{ id: string }>(`/runs/${runId}/cancel`, { method: 'POST' }),
  taskAttempts: (taskId: string) => request<TaskAttempt[]>(`/tasks/${taskId}/attempts`),
  taskDetail: (taskId: string) => request<TaskDetail>(`/tasks/${taskId}`),
  retryTask: (taskId: string) => request<{ id: string }>(`/tasks/${taskId}/retry`, { method: 'POST' }),
};

export const versions = {
  detail: (pipelineId: string, n: number) =>
    request<VersionDetail>(`/pipelines/${pipelineId}/versions/${n}`),
};

export interface TriggerItem {
  id: string;
  kind: number;
  name: string;
  isEnabled: boolean;
  cron: string | null;
  timezone: string | null;
  overlap: number;
  nextRunAt: string | null;
  lastFiredAt: string | null;
}

export const triggers = {
  list: (pipelineId: string) => request<TriggerItem[]>(`/pipelines/${pipelineId}/triggers`),
  createSchedule: (pipelineId: string, data: { name: string; cron: string; timezone: string; overlap: number; isEnabled?: boolean }) =>
    request<{ id: string }>(`/pipelines/${pipelineId}/triggers/schedules`, { method: 'POST', body: JSON.stringify(data) }),
  updateSchedule: (pipelineId: string, triggerId: string, data: { name?: string; cron?: string; timezone?: string; overlap?: number; isEnabled?: boolean }) =>
    request<{ id: string }>(`/pipelines/${pipelineId}/triggers/schedules/${triggerId}`, { method: 'PUT', body: JSON.stringify(data) }),
  createWebhook: (pipelineId: string, data: { name: string }) =>
    request<{ token: string }>(`/pipelines/${pipelineId}/triggers/webhooks`, { method: 'POST', body: JSON.stringify(data) }),
  regenerateWebhook: (pipelineId: string, triggerId: string) =>
    request<{ token: string }>(`/pipelines/${pipelineId}/triggers/webhooks/${triggerId}/regenerate`, { method: 'POST' }),
  remove: (pipelineId: string, triggerId: string) =>
    request<void>(`/pipelines/${pipelineId}/triggers/${triggerId}`, { method: 'DELETE' }),
};

export const RUN_STATUSES = ['Queued', 'Running', 'Completed', 'Failed', 'Cancelled'];
export const TASK_STATUSES = ['Pending', 'Ready', 'Running', 'Completed', 'Failed', 'RetryScheduled', 'Cancelled', 'Skipped'];
export const PIPELINE_STATUSES = ['Draft', 'Published', 'Archived'];
