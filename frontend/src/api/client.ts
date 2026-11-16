const BACKEND_URL = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/$/, '') || '';
const API_BASE = `${BACKEND_URL}/api/v1`;

function getCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp('(^| )' + name + '=([^;]+)'));
  return match ? match[2] : null;
}

export function getAuthToken(): string | null {
  return localStorage.getItem('reflow_token') || getCookie('Reflow.Token');
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getAuthToken();
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
  login: async (data: { email: string; password: string }) => {
    const res = await request<{ accessToken?: string }>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(data),
    });
    if (res?.accessToken) {
      localStorage.setItem('reflow_token', res.accessToken);
    }
    return res;
  },
  googleLogin: async (data: { idToken?: string; accessToken?: string }) => {
    const res = await request<{ accessToken?: string }>('/auth/google', {
      method: 'POST',
      body: JSON.stringify(data),
    });
    if (res?.accessToken) {
      localStorage.setItem('reflow_token', res.accessToken);
    }
    return res;
  },
  me: () => request<{ id: string; email: string; displayName: string; createdAt: string }>('/auth/me'),
  logout: async () => {
    try {
      await request<void>('/auth/logout', { method: 'POST' });
    } finally {
      localStorage.removeItem('reflow_token');
    }
  },
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
    const res = await fetch(`${BACKEND_URL}/api/node-types`, { credentials: 'include' });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    return res.json() as Promise<NodeDefinition[]>;
  },
};

export interface SourceFile {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  createdAt: string;
}

export const files = {
  list: () => request<SourceFile[]>('/files'),
  upload: (fileName: string, content: string) =>
    request<SourceFile>('/files', { method: 'POST', body: JSON.stringify({ fileName, content }) }),
  remove: (id: string) => request<void>(`/files/${id}`, { method: 'DELETE' }),
};

export interface GeneratedNode {
  nodeId: string;
  nodeType: string;
  label: string | null;
  config: Record<string, unknown>;
}

export interface GeneratedEdge {
  sourceNodeId: string;
  targetNodeId: string;
}

export interface GeneratedWorkflowDraft {
  name: string;
  explanation: string;
  nodes: GeneratedNode[];
  edges: GeneratedEdge[];
  warnings: string[];
  repairAttempts: number;
}

export const assistant = {
  generate: (prompt: string) =>
    request<GeneratedWorkflowDraft>('/assistant/workflows/generate', {
      method: 'POST',
      body: JSON.stringify({ prompt }),
    }),
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

export interface RunSnapshot {
  runId: string;
  run: PipelineRun;
  tasks: TaskRun[];
  logs: RunLog[];
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

export const artifactUrl = (runId: string, nodeId: string) => {
  const token = getAuthToken();
  const base = `${API_BASE}/runs/${runId}/artifacts/${encodeURIComponent(nodeId)}`;
  return token ? `${base}?token=${encodeURIComponent(token)}` : base;
};

export async function downloadArtifact(runId: string, nodeId: string, fallbackFileName?: string): Promise<void> {
  const token = getAuthToken();
  const res = await fetch(artifactUrl(runId, nodeId), {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  });

  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.error || `Download failed: HTTP ${res.status}`);
  }

  const blob = await res.blob();
  const disposition = res.headers.get('content-disposition');
  let filename = fallbackFileName || `${nodeId}.csv`;
  if (disposition && disposition.includes('filename=')) {
    const match = disposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
    if (match && match[1]) {
      filename = match[1].replace(/['"]/g, '').trim();
    }
  }

  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  window.URL.revokeObjectURL(url);
}

export function exportRowsToFile(rows: Array<Record<string, unknown>>, filename: string, format: 'csv' | 'json') {
  if (!rows || rows.length === 0) return;

  let content: string;
  let mime: string;

  if (format === 'json') {
    content = JSON.stringify(rows, null, 2);
    mime = 'application/json';
  } else {
    const headers = Object.keys(rows[0]);
    const escapeCsv = (val: unknown) => {
      if (val === null || val === undefined) return '';
      const s = String(val);
      if (s.includes(',') || s.includes('"') || s.includes('\n')) {
        return `"${s.replace(/"/g, '""')}"`;
      }
      return s;
    };
    const csvLines = [
      headers.map(escapeCsv).join(','),
      ...rows.map(r => headers.map(h => escapeCsv(r[h])).join(',')),
    ];
    content = csvLines.join('\n');
    mime = 'text/csv';
  }

  const blob = new Blob([content], { type: mime });
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename.endsWith(`.${format}`) ? filename : `${filename}.${format}`;
  document.body.appendChild(a);
  a.click();
  a.remove();
  window.URL.revokeObjectURL(url);
}
