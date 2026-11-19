// V0712_SORT_MILESTONES
import type { MilestoneV07 } from './projectProgress.api'

/** Confirmed plans first, then creation order within each group. */
export function sortMilestonesForDisplay(items: readonly MilestoneV07[]): MilestoneV07[] {
  const group = (m: MilestoneV07) => m.proposalStatus === 'CONFIRMED' ? 0 : 1
  return [...items].sort((a, b) => group(a) - group(b) ||
    Date.parse(a.createdAt) - Date.parse(b.createdAt) || a.id.localeCompare(b.id))
}
