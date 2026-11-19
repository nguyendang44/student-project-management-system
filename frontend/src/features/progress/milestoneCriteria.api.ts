import { request } from '../../api/client'

type GitRange = { id: string; baseSha: string; headSha: string }
export type MilestoneAssessment = {
  isCurrent: boolean
  id: string; source: 'AI' | 'REVIEW'; outcome: 'full' | 'partial' | 'missing'
  note: string | null; createdAt: string; git: GitRange | null
  evidence: { id: number; title: string; file: string; evidence: string }[]
}
export type MilestoneCriterion = {
  id: string; title: string; acceptanceCriteria: string; points: number; percentOfMilestone: number
  deadlineAt: string | null; history: MilestoneAssessment[]
}
export type CriterionWrite = Pick<MilestoneCriterion, 'title' | 'acceptanceCriteria' | 'points' | 'deadlineAt'>
export type CriterionPlanDraft = Pick<CriterionWrite, 'title' | 'acceptanceCriteria' | 'points'>
export type MilestoneAssessmentSummary = {
  confirmedPercent: number | null; proposedPercent: number | null; proposed: number
  tracked: number; reviewed: number; completed: number
  milestones: {
    milestoneId: string; confirmedPercent: number; proposedPercent: number | null
    legacyApproved: boolean; criteriaCount: number; reviewedCount: number; plannedPoints: number; planned: boolean
  }[]
}
type CriterionDetails = {
  proposedPercent: number | null
  criteria: MilestoneCriterion[]
  reports: (GitRange & { completedAt: string | null; repositoryUrl: string })[]
}
export const milestoneCriteriaApi = {
  summary: (projectId: string, signal: AbortSignal) =>
    request<MilestoneAssessmentSummary>(`/v07/projects/${projectId}/milestone-progress`, { signal }),
  details: (milestoneId: string, signal: AbortSignal) =>
    request<CriterionDetails>(`/v07/milestones/${milestoneId}/criteria`, { signal }),
  add: (milestoneId: string, data: CriterionWrite) =>
    request(`/v07/milestones/${milestoneId}/criteria`, { method: 'POST', body: JSON.stringify(data) }),
  suggest: (milestoneId: string, signal: AbortSignal) =>
    request<{ items: CriterionPlanDraft[] }>(`/v07/milestones/${milestoneId}/criteria/suggest`, { method: 'POST', signal }),
  savePlan: (milestoneId: string, items: CriterionWrite[]) =>
    request(`/v07/milestones/${milestoneId}/criteria/plan`, { method: 'POST', body: JSON.stringify({ items }) }),
  edit: (milestoneId: string, criterionId: string, data: CriterionWrite) =>
    request(`/v07/milestones/${milestoneId}/criteria/${criterionId}`, { method: 'PUT', body: JSON.stringify(data) }),
  remove: (milestoneId: string, criterionId: string) =>
    request(`/v07/milestones/${milestoneId}/criteria/${criterionId}`, { method: 'DELETE' }),
  deadline: (milestoneId: string, criterionId: string, deadlineAt: string) =>
    request(`/v07/milestones/${milestoneId}/criteria/${criterionId}/deadline`, {
      method: 'PUT', body: JSON.stringify({ deadlineAt }),
    }),
  propose: (milestoneId: string, gitRunId: string, signal: AbortSignal) =>
    request(`/v07/milestones/${milestoneId}/criteria/ai`, { method: 'POST', body: JSON.stringify({ gitRunId }), signal }),
  review: (milestoneId: string, criterionId: string, isMet: boolean, proposalId: string | null, note: string) =>
    request(`/v07/milestones/${milestoneId}/criteria/${criterionId}/review`, {
      method: 'POST', body: JSON.stringify({ isMet, proposalId, note }),
    }),
}
