/**
 * v0.7.5: Milestone deadline is a date, valid until the end of that day (Vietnam time).
 * Apply once to an existing v0.7.4 project, idempotent, preserves all project data.
 */
'use strict';
const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const paths = {
  view: path.join(root,'frontend/src/views/ProjectProgressV07View.vue'),
  summary: path.join(root,'frontend/src/components/ProjectMilestoneSummary.vue'),
  endpoint: path.join(root,'backend/StudentProjects.Api/Endpoints/ProjectProgressV07Endpoints.cs'),
};
const fail = (message) => { throw new Error(message); };
const ensure = (ok, why) => { if(!ok) fail(why); };
const original = Object.fromEntries(Object.entries(paths).map(([key, filename]) => {
  ensure(fs.existsSync(filename), `Không có file: ${filename}. Cần cài v0.7.4 trước.`);
  return [key, fs.readFileSync(filename,'utf8')];
}));
function replaceOnce(source, needle, replacement, why) {
  const at = source.indexOf(needle);
  ensure(at !== -1, `Source không tương thích: ${why}`);
  ensure(source.indexOf(needle,at+1) === -1, `Mẫu trùng: ${why}`);
  return source.slice(0,at)+replacement+source.slice(at+needle.length);
}
const asLF = t => t.replace(/\r\n/g,'\n');
const restoreEOL=(t,source)=> source.includes('\r\n') ? t.replace(/\n/g,'\r\n') : t;
const changed = {};
const markView='// V075_DEADLINE_DATE_ONLY';
const markApi='// V075_DEADLINE_END_OF_DAY';
const markSummary='// V075_DEADLINE_DATE_ONLY_SUMMARY';
if (!original.view.includes(markView)) {
  let s=asLF(original.view);
  const start=s.indexOf('const dateLocal = (date: string) => {');
  const end=s.indexOf('\nconst readableDate = ',start);
  ensure(start!==-1 && end>start,'Không tìm thấy formatter ngày cũ của Milestone.');
  const helper=`${markView}
// Date-only deadline is a Vietnamese civil day, independent of OS/browser timezone.
const deadlineDate = (date: string) => {
  const parts = new Intl.DateTimeFormat('en-GB', { timeZone:'Asia/Ho_Chi_Minh',
    year:'numeric', month:'2-digit', day:'2-digit' }).formatToParts(new Date(date))
  const part = (type: string) => parts.find(p => p.type === type)?.value ?? ''
  return \`\${part('year')}-\${part('month')}-\${part('day')}\`
}
const readableDeadline = (date: string) => new Intl.DateTimeFormat('vi-VN', {
  timeZone:'Asia/Ho_Chi_Minh', day:'2-digit', month:'2-digit', year:'numeric'
}).format(new Date(date))`;
  s=s.slice(0,start)+helper+s.slice(end);
  s=replaceOnce(s,
    '    startAt:dateLocal(selected.value.startAt),deadlineAt:dateLocal(selected.value.deadlineAt)}',
    '    startAt:selected.value.startAt,deadlineAt:deadlineDate(selected.value.deadlineAt)}',
    'prefill ngày khi sửa Milestone');
  const savestart=s.indexOf('async function saveMilestone() {');
  const saveend=s.indexOf('\nfunction submitProgress()',savestart);
  ensure(savestart!==-1 && saveend>savestart,'Không tìm thấy khối saveMilestone hiện tại.');
  const save=`async function saveMilestone() {
  if (!projectId.value) return
  const day = milestoneForm.value.deadlineAt
  if (!/^\\d{4}-\\d{2}-\\d{2}$/.test(day)) { error.value = 'Vui lòng chọn ngày Deadline.'; return }
  // Date input has no timezone. Interpret the chosen day in Vietnam (+07:00).
  const deadline = new Date(\`\${day}T23:59:59.999+07:00\`)
  const midnight = new Date(\`\${day}T00:00:00+07:00\`)
  if (!Number.isFinite(deadline.getTime()) || !Number.isFinite(midnight.getTime())) {
    error.value = 'Ngày Deadline không hợp lệ.'; return
  }
  // StartAt remains an internal backward-compatible API field, never a required UI input.
  const oldStart = editing.value && selected.value ? new Date(selected.value.startAt) : midnight
  const start = Number.isFinite(oldStart.getTime()) && oldStart <= deadline ? oldStart : midnight
  const request: MilestoneForm = { ...milestoneForm.value,
    startAt:start.toISOString(), deadlineAt:deadline.toISOString() }
  const wasEditing = editing.value
  const id = selectedId.value
  await run(() => wasEditing ? projectProgressApi.update(id,request) : projectProgressApi.create(projectId.value,request),
    wasEditing
      ? (role.value === 'Student' ? 'Đã sửa và gửi lại kế hoạch cho giảng viên.' : 'Đã cập nhật Milestone.')
      : (role.value === 'Student' ? 'Đã gửi đề xuất Milestone cho giảng viên.' : 'Đã tạo Milestone.'))
}`;
  s=s.slice(0,savestart)+save+s.slice(saveend);
  s=replaceOnce(s,
    '      <div class="v07-grid"><label>Bắt đầu <input v-model="milestoneForm.startAt" type="datetime-local" lang="en-GB" required /></label>\n        <label>Deadline <input v-model="milestoneForm.deadlineAt" type="datetime-local" lang="en-GB" required /></label></div>',
    '      <label>Deadline (ngày cuối cùng) <input v-model="milestoneForm.deadlineAt" type="date" required /></label>\n      <p class="v07-muted">Hạn nộp được tính đến hết ngày đã chọn (giờ Việt Nam).</p>',
    'ô nhập Bắt đầu và datetime Deadline');
  s=replaceOnce(s,'<td>{{readableDate(m.deadlineAt)}}</td>', '<td>{{readableDeadline(m.deadlineAt)}}</td>', 'ngày trong bảng Milestone');
  s=replaceOnce(s,'<span>Deadline: <strong>{{readableDate(selected.deadlineAt)}}</strong></span>',
      '<span>Deadline: <strong>{{readableDeadline(selected.deadlineAt)}}</strong></span>', 'ngày trong bộ chọn Milestone');
  s=replaceOnce(s,
    '      <span><strong>Bắt đầu:</strong> {{readableDate(selected.startAt)}}</span>\n      <span><strong>Hạn:</strong> {{readableDate(selected.deadlineAt)}}</span>',
    '      <span><strong>Deadline:</strong> {{readableDeadline(selected.deadlineAt)}}</span>',
    'chi tiết ngày bắt đầu / deadline');
  changed.view=restoreEOL(s,original.view);
}
if (!original.summary.includes(markSummary)) {
  let s=asLF(original.summary);
  s=replaceOnce(s,`const formatDate = (value: string) => new Date(value).toLocaleString('vi-VN', {
  day:'2-digit', month:'2-digit', year:'numeric', hour:'2-digit', minute:'2-digit', hour12:false, hourCycle:'h23'
})`,
    `${markSummary}\nconst formatDate = (value: string) => new Intl.DateTimeFormat('vi-VN', {\n  timeZone:'Asia/Ho_Chi_Minh', day:'2-digit', month:'2-digit', year:'numeric'\n}).format(new Date(value))`, 'ngày trong Tiến độ tổng hợp dự án');
  changed.summary=restoreEOL(s,original.summary);
}
if (!original.endpoint.includes(markApi)) {
  let s=asLF(original.endpoint);
  const target='    private static string? Validate(SaveMilestone? req)';
  ensure(s.includes(target),'Thiếu Validate(SaveMilestone).');
  const helper=`    ${markApi}
    // Business deadline = last tick of the selected calendar day in Vietnam (UTC+07:00).
    // Vietnam currently has no daylight-saving transitions.
    private static DateTimeOffset EndOfDeadlineDay(DateTimeOffset value)
    {
        var local = value.ToOffset(TimeSpan.FromHours(7));
        var midnight = new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, TimeSpan.FromHours(7));
        return midnight.AddDays(1).AddTicks(-1);
    }

`;
  s=replaceOnce(s,target,helper+target,'hàm validate deadline');
  s=replaceOnce(s,
    'if (req.StartAt == default || req.DeadlineAt == default || req.DeadlineAt < req.StartAt)\n            return "Deadline phải bằng hoặc sau ngày bắt đầu.";',
    'if (req.StartAt == default || req.DeadlineAt == default || EndOfDeadlineDay(req.DeadlineAt) < req.StartAt)\n            return "Ngày Deadline không thể trước ngày bắt đầu.";',
    'backend validation');
  s=replaceOnce(s,
    'IsOverdue = m.ProposalStatus == "CONFIRMED" && m.DeadlineAt < now && m.Status != MilestoneStatus.APPROVED,',
    'IsOverdue = m.ProposalStatus == "CONFIRMED" && EndOfDeadlineDay(m.DeadlineAt) < now && m.Status != MilestoneStatus.APPROVED,',
    'kiểm tra quá hạn theo cuối ngày');
  s=replaceOnce(s,
    'StartAt = req.StartAt, DeadlineAt = req.DeadlineAt, ExpectedResult = Text(req.ExpectedResult),',
    'StartAt = req.StartAt, DeadlineAt = EndOfDeadlineDay(req.DeadlineAt), ExpectedResult = Text(req.ExpectedResult),',
    'lưu mốc mới');
  s=replaceOnce(s,
    'item.StartAt = req.StartAt; item.DeadlineAt = req.DeadlineAt;',
    'item.StartAt = req.StartAt; item.DeadlineAt = EndOfDeadlineDay(req.DeadlineAt);',
    'cập nhật mốc cũ');
  changed.endpoint=restoreEOL(s,original.endpoint);
}
const pending=Object.entries(changed);
if (!pending.length) { console.log('Đã cài v0.7.5 trước đó. Không sửa trùng.'); process.exit(0); }
const stamp=new Date().toISOString().replace(/[:.]/g,'-');
const backups=[];
try {
  for (const [key, content] of pending) {
    const filename=paths[key];
    const backup=filename+`.before-v075-${stamp}.bak`;
    fs.copyFileSync(filename, backup);
    backups.push([filename,backup]);
    fs.writeFileSync(filename,content,'utf8');
    console.log('Đã cập nhật:',path.relative(root,filename));
  }
  console.log('v0.7.5 hoàn tất. Không sửa SQL/EF, không xóa dữ liệu.');
} catch (err) {
  for (const [filename,backup] of backups.reverse()) fs.copyFileSync(backup,filename);
  console.error('Có lỗi, đã hoàn nguyên mọi file đã sửa.');
  throw err;
}
