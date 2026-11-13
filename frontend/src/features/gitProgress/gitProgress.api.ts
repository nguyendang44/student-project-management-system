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
export interface GitProgressFeatureStep {
  detail: string
  citations: GitProgressEvidenceCitation[]
}
export interface GitProgressEvidence {
  area?: string
  file: string
  evidence: string
  title?: string | null
  citations?: GitProgressEvidenceCitation[] | null
  description: string
  factId?: string
  featureId?: string
  steps?: GitProgressFeatureStep[]
  components?: string[]
  technologies?: string[]
  verificationStatus?: string
  evidenceCoverage?: string
  aiExplanation?: string | null
  source?: 'ollama' | 'diff-rule' | 'feature-rule' | 'ollama-feature' | 'ai-feature-report' | 'diff-feature-report'
}
export interface GitProgressRejectionDiagnostics {
  unknownId: number
  unsentFile: number
  unsentLine: number
  nonFunctionalLine: number
  weakDescription: number
  duplicate: number
  eligible: number
  invalidLength: number
  unsupportedClaim: number
  boilerplate: number
  missingAction: number
  testSetupOnly: number
  groupedTestDetail?: number
  groupedFeatureDetail?: number
  selectionLimit?: number
  semanticMismatch?: number
  supportingDetail?: number
}
export interface GitProgressAiDiagnostics {
  analysisStatus?: 'available' | 'timeout' | 'connection-error' | 'invalid-response' | null
  contextStatus?: 'sampled' | 'partial' | 'unavailable' | 'empty' | null
  contextExcerpts?: number
  contextFiles?: number
  eligibleFiles?: number
  fullyAnalyzedFiles?: number
  missingPatchFiles?: number
  plannedBatches?: number
  completedBatches?: number
  failedBatches?: number
  skippedLines?: number
  truncatedBatches?: number
  coverageStatus?: 'complete' | 'partial' | 'no-readable-patch' | 'unknown' | null
  sentFiles: number
  modelItems: number
  parsedCandidates: number
  verified: number
  ruleFallbackItems: number
  rejections: GitProgressRejectionDiagnostics
  featureGroups: number
  explanationModelItems: number
  explanationCandidates: number
  explanationAccepted: number
  explanationStatus: 'no-feature-groups' | 'not-requested' | 'empty' | 'invalid-items' | 'filtered' | 'accepted' | 'error'
  featureReportModelItems?: number
  featureReportCandidates?: number
  featureReportAccepted?: number
  featureReportRejectedEvidence?: number
  featureEvidenceLines?: number
  featureEvidenceOmitted?: number
  featureReportStatus?: 'not-requested' | 'empty' | 'invalid-items' | 'filtered' | 'accepted' | 'error'
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
  aiDiagnostics?: GitProgressAiDiagnostics | null
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
