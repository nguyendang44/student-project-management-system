import { request } from '../../api/client'

export interface GitProgressRunListItem {
  id: string
  baseSha: string
  headSha: string
  modelName: string
  status: 'PENDING' | 'RUNNING' | 'COMPLETED' | 'FAILED'
  totalCommits: number
  totalFiles: number
  createdAt: string
  completedAt: string | null
}
export interface GitProgressFile {
  path: string
  status: string
  additions: number
  deletions: number
}
export interface GitProgressEvidenceCitation {
  file: string
  evidence: string
}
export interface GitProgressEvidence {
  area?: string
  file: string
  evidence: string
  title?: string | null
  citations?: GitProgressEvidenceCitation[] | null
  description: string
  source?: 'ollama' | 'diff-rule' | 'feature-rule'
}
export interface GitProgressRun extends GitProgressRunListItem {
  projectId: string
  repositoryLinkId: string
  additions: number
  deletions: number
  sentFiles: number
  partialCoverage: boolean
  summary: string
  filesJson: string
  evidenceJson: string
  errorMessage: string | null
  repositoryUrl: string | null
}
export const gitProgressApi = {
  start: (repositoryId: string, baseSha: string, headSha: string) =>
    request<{ id: string; status: string; cached: boolean }>(`/v11/repositories/${repositoryId}/progress`, {
      method: 'POST', body: JSON.stringify({ baseSha, headSha }),
    }),
  list: (repositoryId: string) =>
    request<GitProgressRunListItem[]>(`/v11/repositories/${repositoryId}/progress`),
  get: (id: string) => request<GitProgressRun>(`/v11/progress/${id}`),
}
