export type ProgressDeadlineWarning = {
  level: 'info' | 'warning' | 'danger'
  code: string
  message: string
  percent: number | null
  expectedPercent: number | null
  daysRemaining: number
  deadlineAt: string
}
