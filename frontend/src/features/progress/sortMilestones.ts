// V0712_SORT_MILESTONES
import type { MilestoneV07 } from './projectProgress.api'

/** Display-only ordering: confirmed plans first, then plans requiring review/revision.
 * Within each group, earliest deadline first. Never mutates the API response.
 */
export function sortMilestonesForDisplay(items: readonly MilestoneV07[]): MilestoneV07[] {
  const group = (m: MilestoneV07) => m.proposalStatus === 'CONFIRMED' ? 0 : 1
  const due = (m: MilestoneV07) => {
    const t = Date.parse(m.deadlineAt)
    return Number.isFinite(t) ? t : Number.MAX_SAFE_INTEGER
  }
  return [...items].sort((a, b) => group(a) - group(b) || due(a) - due(b))
}
