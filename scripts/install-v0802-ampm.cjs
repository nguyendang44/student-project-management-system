#!/usr/bin/env node
'use strict';
/** Focused, idempotent v0.8.2 UI patch. Requires v0.8.1 views. */
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const j = 'frontend/src/views/GuidanceV08View.vue';
const n = 'frontend/src/views/NotificationsV08View.vue';
const helper = 'frontend/src/features/v08/v08DateTime.ts';
const paths = [j, n];
const marker = 'V0802_AMPM_JOURNAL';
function fail(msg) { throw new Error('v0.8.2: '+msg); }
function replaceOnce(src, from, to, name) {
  if (src.split(from).length !== 2) fail(`${name}: không xác định được vị trí cập nhật duy nhất; không sửa file.`);
  return src.replace(from, to);
}
if (paths.some(x => !fs.existsSync(path.join(root,x)))) fail('Thiếu GuidanceV08View.vue hoặc NotificationsV08View.vue.');
const originals = Object.fromEntries(paths.map(p => [p, fs.readFileSync(path.join(root,p),'utf8')]));
if (Object.values(originals).every(x => x.includes(marker)) && fs.existsSync(path.join(root,helper))) {
  console.log('v0.8.2 đã cài đặt; không sửa trùng.'); process.exit(0);
}
if (Object.values(originals).some(x => x.includes(marker)) || fs.existsSync(path.join(root,helper)))
  fail('Phát hiện cập nhật một phần hoặc đã có file định dạng ngày giờ. Hãy kiểm tra trước khi áp dụng.');
const updated = {};
let s = originals[j].replace(/\r\n/g,'\n');
s = replaceOnce(s,
  "import { v08Api, type ProjectOptionV08, type GuidanceV08 } from '../features/v08/v08.api'",
  "import { v08Api, type ProjectOptionV08, type GuidanceV08 } from '../features/v08/v08.api'\nimport { formatDateTimeV08 } from '../features/v08/v08DateTime' // "+marker, j);
s = replaceOnce(s,
  "const form = ref({ meetingLocal: '', summary: '', feedback: '', nextActions: '' })",
  "const newForm = () => ({ meetingDate: '', meetingHour: '12', meetingMinute: '00', meetingPeriod: 'PM' as 'AM' | 'PM', summary: '', feedback: '', nextActions: '' })\nconst form = ref(newForm())\nconst hours = Array.from({ length: 12 }, (_, i) => String(i + 1).padStart(2, '0'))\nconst minutes = Array.from({ length: 60 }, (_, i) => String(i).padStart(2, '0'))", j);
s = replaceOnce(s,
  "const fmt = (s: string) => new Date(s).toLocaleString('vi-VN', { hour12: false, timeZone: 'Asia/Ho_Chi_Minh' })",
  'const fmt = formatDateTimeV08', j);
s = replaceOnce(s,
  "  if (!projectId.value || !form.value.meetingLocal || !form.value.summary.trim()) { error.value = 'Vui lòng nhập ngày gặp và nội dung báo cáo.'; return }\n  const ms = Date.parse(form.value.meetingLocal)\n  if (!Number.isFinite(ms)) { error.value = 'Ngày giờ không hợp lệ.'; return }",
  `  const f = form.value
  if (!projectId.value || !f.meetingDate || !f.summary.trim()) { error.value = 'Vui lòng nhập ngày gặp và nội dung báo cáo.'; return }
  const h12 = Number(f.meetingHour)
  const m = Number(f.meetingMinute)
  if (!(h12 >= 1 && h12 <= 12) || !(m >= 0 && m <= 59)) { error.value = 'Giờ hoặc phút không hợp lệ.'; return }
  const h24 = h12 % 12 + (f.meetingPeriod === 'PM' ? 12 : 0)
  // Same local-time interpretation as the previous datetime-local input.
  const dateTime = new Date(f.meetingDate + 'T' + String(h24).padStart(2, '0') + ':' + f.meetingMinute + ':00')
  const ms = dateTime.getTime()
  if (!Number.isFinite(ms) || dateTime.getFullYear() !== Number(f.meetingDate.slice(0, 4))
      || dateTime.getMonth() + 1 !== Number(f.meetingDate.slice(5, 7))
      || dateTime.getDate() !== Number(f.meetingDate.slice(8, 10))) {
    error.value = 'Ngày giờ không hợp lệ.'; return
  }`, j);
s = replaceOnce(s,
  "    form.value = { meetingLocal: '', summary: '', feedback: '', nextActions: '' }",
  '    form.value = newForm()', j);
s = replaceOnce(s,
  '      <label>Ngày giờ gặp <input type="datetime-local" required v-model="form.meetingLocal" /></label>',
  `      <fieldset class="meeting-date-time">
        <legend>Ngày giờ gặp</legend>
        <div class="meeting-date-time-fields">
          <label>Ngày <input type="date" required v-model="form.meetingDate" /></label>
          <label>Giờ <select required v-model="form.meetingHour"><option v-for="h in hours" :key="h" :value="h">{{ h }}</option></select></label>
          <label>Phút <select required v-model="form.meetingMinute"><option v-for="m in minutes" :key="m" :value="m">{{ m }}</option></select></label>
          <label>AM / PM <select required v-model="form.meetingPeriod"><option value="AM">AM</option><option value="PM">PM</option></select></label>
        </div>
      </fieldset>`, j);
s = replaceOnce(s,
  '.entry-head{display:flex;justify-content:space-between;flex-wrap:wrap;gap:8px}',
  '.meeting-date-time{border:0;margin:0;padding:0;min-width:0}.meeting-date-time legend{padding:0;margin-bottom:5px}.meeting-date-time-fields{display:grid;grid-template-columns:minmax(160px,2fr) repeat(3,minmax(78px,1fr));gap:10px;max-width:700px}.meeting-date-time-fields label{min-width:0}.meeting-date-time-fields input,.meeting-date-time-fields select{min-width:0;box-sizing:border-box}@media(max-width:540px){.meeting-date-time-fields{grid-template-columns:repeat(3,minmax(0,1fr))}.meeting-date-time-fields label:first-child{grid-column:1/-1}}\n.entry-head{display:flex;justify-content:space-between;flex-wrap:wrap;gap:8px}', j);
updated[j]=s;
s = originals[n].replace(/\r\n/g,'\n');
s = replaceOnce(s,
  "import { v08Api, type NotificationV08 } from '../features/v08/v08.api'",
  "import { v08Api, type NotificationV08 } from '../features/v08/v08.api'\nimport { formatDateTimeV08 } from '../features/v08/v08DateTime' // "+marker, n);
s = replaceOnce(s,
  "const fmt = (s: string) => new Date(s).toLocaleString('vi-VN', { hour12: false, timeZone: 'Asia/Ho_Chi_Minh' })",
  'const fmt = formatDateTimeV08', n);
updated[n]=s;
const helperText = `/** Format date/time as dd/MM/yyyy hh:mm AM/PM, without VI locale SA/CH substitution. */
const vnZone = 'Asia/Ho_Chi_Minh'
const dateFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: vnZone, day: '2-digit', month: '2-digit', year: 'numeric'
})
const timeFormatter = new Intl.DateTimeFormat('en-US', {
  timeZone: vnZone, hour: '2-digit', minute: '2-digit', hour12: true
})
export function formatDateTimeV08(value: string): string {
  const date = new Date(value)
  if (!Number.isFinite(date.getTime())) return 'Ngày giờ không hợp lệ'
  return dateFormatter.format(date) + ' ' + timeFormatter.format(date)
}
`;
// Prepare and backup everything before writing. Never overwrite a user's existing helper.
const stamp = new Date().toISOString().replace(/[:.]/g, '-')
const backups=[];
try {
  for (const f of paths) {
    const full = path.join(root,f);
    const bak=full+'.before-v0802-'+stamp+'.bak';
    fs.copyFileSync(full,bak);backups.push([full,bak]);
  }
  for (const f of paths) {
    fs.writeFileSync(path.join(root,f), originals[f].includes('\r\n')?updated[f].replace(/\n/g,'\r\n'):updated[f]);
    console.log('Cập nhật '+f)
  }
  fs.writeFileSync(path.join(root,helper),helperText,{flag:'wx'});
  console.log('Đã cài v0.8.2: Ngày + Giờ + Phút + AM/PM, format 12 giờ trong nhật ký và thông báo.');
} catch (e) {
  for(const [full,bak] of backups) try {fs.copyFileSync(bak,full)} catch{}
  try {fs.unlinkSync(path.join(root,helper))}catch{}
  throw e
}
