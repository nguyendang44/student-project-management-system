/** Format date/time as dd/MM/yyyy HH:mm, without VI locale SA/CH substitution. */
const vnZone = 'Asia/Ho_Chi_Minh'
const dateFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: vnZone, day: '2-digit', month: '2-digit', year: 'numeric'
})
const timeFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: vnZone, hour: '2-digit', minute: '2-digit', hourCycle: 'h23'
})
export function formatDateTimeV08(value: string): string {
  const date = new Date(value)
  if (!Number.isFinite(date.getTime())) return 'Ngày giờ không hợp lệ'
  return dateFormatter.format(date) + ' ' + timeFormatter.format(date)
}
