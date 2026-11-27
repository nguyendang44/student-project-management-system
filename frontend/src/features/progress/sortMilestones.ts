// V0712_SORT_MILESTONES
import type { MilestoneV07 } from './projectProgress.api'

/** Active work first; completed milestones last. Preserve creation order within each group. */
export function sortMilestonesForDisplay(items: readonly MilestoneV07[]): MilestoneV07[] {
  const group = (m: MilestoneV07) => {
    if (m.status === 'APPROVED') return 3
    if (['IN_PROGRESS', 'REVISION_REQUIRED', 'OVERDUE', 'SUBMITTED'].includes(m.status)) return 0
    return m.proposalStatus === 'CONFIRMED' ? 1 : 2
  }
  return [...items].sort((a, b) => group(a) - group(b) ||
    Date.parse(a.createdAt) - Date.parse(b.createdAt) || a.id.localeCompare(b.id))
}
