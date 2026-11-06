import { request } from '../../api/client'

export interface AnalysisFinding {
  id: string
  runId: string
  category: string
  severity: 'HIGH' | 'MEDIUM' | 'LOW'
  filePath: string
  lineNumber: number
  evidence: string
  explanation: string
  recommendation: string
  fingerprint: string
  verificationStatus?: 'NEEDS_REVIEW'
}
export interface AnalysisRunListItem {
  id: string
  branch: string
  commitSha: string
  scope?: string
  status: 'PENDING' | 'RUNNING' | 'COMPLETED' | 'FAILED'
  modelName: string
  createdAt: string
  completedAt: string | null
  findingsCount: number
}
export interface AnalysisRun extends AnalysisRunListItem {
  projectId: string
  repositoryLinkId: string
  summary: string
  developmentWork: string
  analyzedPathsJson: string
  skippedFiles: number
  errorMessage: string | null
  repositoryUrl: string | null
  findings: AnalysisFinding[]
  reviews: { id: string; lecturerUserId: string; comment: string; createdAt: string }[]
}
export interface AnalysisCompare {
  older: string
  newer: string
  sameCommit?: boolean
  resolvedCandidates: AnalysisFinding[]
  persistent: AnalysisFinding[]
  newFindings: AnalysisFinding[]
  note: string
}
export const codeAnalysisApi = {
  start: (repositoryId: string, branch: string) =>
    request<{ id: string; status: string; commitSha: string; cached: boolean }>(
      `/v11/repositories/${repositoryId}/analyses`,
      { method: 'POST', body: JSON.stringify({ branch }) },
    ),
  list: (repositoryId: string) =>
    request<AnalysisRunListItem[]>(`/v11/repositories/${repositoryId}/analyses`),
  get: (runId: string) => request<AnalysisRun>(`/v11/analyses/${runId}`),
  compare: (repositoryId: string, older: string, newer: string) =>
    request<AnalysisCompare>(
      `/v11/repositories/${repositoryId}/analyses/compare?older=${encodeURIComponent(older)}&newer=${encodeURIComponent(newer)}`,
    ),
  review: (runId: string, comment: string) =>
    request(`/v11/analyses/${runId}/reviews`, { method: 'POST', body: JSON.stringify({ comment }) }),
}
