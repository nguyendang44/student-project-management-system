#!/usr/bin/env node
'use strict'
/** v0.7.3: allow a student's newer submission while lecturer review is pending. */
const fs = require('node:fs')
const path = require('node:path')
const root = path.resolve(__dirname, '..')
const backend = path.join(root, 'backend/StudentProjects.Api/Endpoints/ProjectProgressV07Endpoints.cs')
const frontend = path.join(root, 'frontend/src/views/ProjectProgressV07View.vue')

function fail(reason) { throw new Error('Dừng cài v0.7.3: ' + reason) }
function replaceOnce(source, oldText, newText, label) {
  if (source.split(oldText).length !== 2) fail('Không tìm thấy đúng một vị trí ' + label + '. Source khác phiên bản dự kiến; chưa thay đổi file.')
  return source.replace(oldText, newText)
}
function changeBackend(source) {
  const a = source.indexOf('private static async Task<IResult> Submit(')
  const b = source.indexOf('public sealed record ReviewInput(', a)
  if (a < 0 || b < a) fail('Không tìm được API Submit của v0.7.1.')
  let part = source.slice(a,b)
  // Only modify Submit; UpdateProgress must still block changes while awaiting review.
  part = replaceOnce(part,
    'if (!Active(project) || item.Status is MilestoneStatus.SUBMITTED or MilestoneStatus.APPROVED)',
    'if (!Active(project) || item.Status == MilestoneStatus.APPROVED)', 'trạng thái API Submit')
  part = replaceOnce(part,
    '        db.MilestoneSubmissions.Add(new MilestoneSubmission',
    '        var isResubmission = item.Status == MilestoneStatus.SUBMITTED;\n        db.MilestoneSubmissions.Add(new MilestoneSubmission',
    'lưu bản nộp mới')
  part = replaceOnce(part,
    'Audit(db, Actor(principal), "MILESTONE_SUBMITTED", "Milestone", id);',
    'Audit(db, Actor(principal), isResubmission ? "MILESTONE_RESUBMITTED" : "MILESTONE_SUBMITTED", "Milestone", id);',
    'ghi lịch sử audit')
  return source.slice(0,a)+part+source.slice(b)
}
function changeFrontend(source) {
  if (!source.includes('const isSubmissionsPage = computed') || !source.includes('<!-- Submission-only page. -->'))
    fail('Cần có giao diện tách trang v0.7.2 trước khi cài.')
  let s = replaceOnce(source,
    "const canStudentEditProposal = computed(",
    "const canStudentSubmit = computed(() => role.value === 'Student' && canWork.value && selected.value && selected.value.proposalStatus === 'CONFIRMED' && selected.value.status !== 'APPROVED')\nconst canStudentEditProposal = computed(",
    'quyền nộp lại Frontend')
  s = replaceOnce(s,
    "  workDone.value = ''; note.value = ''; resultSummary.value = ''; documentReference.value = ''; feedback.value = ''; proposalFeedback.value = ''",
    "  workDone.value = ''; note.value = ''; feedback.value = ''; proposalFeedback.value = ''\n  const latestSubmission = history.value?.submissions[history.value.submissions.length - 1]\n  resultSummary.value = latestSubmission?.resultSummary ?? ''\n  documentReference.value = latestSubmission?.documentReference ?? ''",
    'điền bản nộp mới nhất')
  s = replaceOnce(s,
    '  if (!selected.value) return\n  void run(() => projectProgressApi.submit(selectedId.value,resultSummary.value,documentReference.value), \'Đã nộp kết quả cho giảng viên.\')',
    "  if (!selected.value || !canStudentSubmit.value) return\n  const resubmitting = selected.value.status === 'SUBMITTED'\n  void run(() => projectProgressApi.submit(selectedId.value,resultSummary.value,documentReference.value),\n    resubmitting ? 'Đã nộp lại kết quả; giảng viên sẽ xem bản mới nhất.' : 'Đã nộp kết quả cho giảng viên.')",
    'submitResult có kiểm tra quyền')
  const subStart = s.indexOf('<!-- Submission-only page. -->')
  const subEnd = s.indexOf('<!-- Lecturer evaluation and review history only. -->',subStart)
  if (subStart < 0 || subEnd < subStart) fail('Không tìm thấy khối Nộp kết quả.')
  let page = s.slice(subStart,subEnd)
  page = replaceOnce(page,
    '    <form v-if="canStudentUpdate" class="v07-form" @submit.prevent="submitResult">',
    '    <p v-if="canStudentSubmit && selected.status === \'SUBMITTED\'" class="v07-muted">Đã gửi kết quả và đang chờ giảng viên đánh giá. Bạn vẫn có thể chỉnh sửa, nộp lại bản mới; các bản trước được lưu trong lịch sử.</p>\n    <form v-if="canStudentSubmit" class="v07-form" @submit.prevent="submitResult">',
    'form Nộp kết quả')
  page = replaceOnce(page,
    '<button :disabled="saving" type="submit">Nộp cho giảng viên</button>',
    '<button :disabled="saving" type="submit">{{selected.status === \'SUBMITTED\' ? \'Nộp lại kết quả\' : \'Nộp cho giảng viên\'}}</button>',
    'nút Nộp lại kết quả')
  page = replaceOnce(page,
    "selected.status === 'SUBMITTED' ? 'Đã nộp. Chờ giảng viên nhận xét hoặc phê duyệt.' : ",
    '', 'thông báo cũ khóa nộp')
  page = replaceOnce(page,
    '<p v-for="h in history.submissions" :key="h.id">{{readableDate(h.submittedAt)}}',
    '<p v-for="(h, index) in history.submissions" :key="h.id"><strong>{{index === history.submissions.length - 1 ? \'Bản mới nhất\' : `Bản ${index + 1}`}}</strong> · {{readableDate(h.submittedAt)}}',
    'đánh dấu bản nộp mới nhất')
  return s.slice(0,subStart)+page+s.slice(subEnd)
}

const missing = [backend,frontend].filter(f=>!fs.existsSync(f))
if (missing.length) fail('Thiếu file: '+missing.map(f=>path.relative(root,f)).join(', '))
const rawB = fs.readFileSync(backend,'utf8')
const rawF = fs.readFileSync(frontend,'utf8')
const newlineB = rawB.includes('\r\n') ? '\r\n' : '\n'
const newlineF = rawF.includes('\r\n') ? '\r\n' : '\n'
const originalB = rawB.replace(/\r\n/g,'\n')
const originalF = rawF.replace(/\r\n/g,'\n')
const bInstalled = originalB.includes('MILESTONE_RESUBMITTED')
const fInstalled = originalF.includes('const canStudentSubmit = computed(')
if (bInstalled && fInstalled) { console.log('v0.7.3 đã cài đặt. Không sửa trùng.'); process.exit(0) }
if (bInstalled !== fInstalled) fail('Backend và Frontend không cùng mức cập nhật. Dừng để kiểm tra, tránh cài đè từng phần.')
const updatedB = changeBackend(originalB)
const updatedF = changeFrontend(originalF)
if (updatedB === originalB || updatedF === originalF) fail('Bản vá chưa thay đổi đủ cả hai file.')
const suffix = new Date().toISOString().replace(/[:.]/g,'-')
const backups = []
try {
  for (const f of [backend,frontend]) {
    const backup=f+'.before-v073-'+suffix+'.bak'
    fs.copyFileSync(f,backup)
    backups.push({ f, backup })
  }
  fs.writeFileSync(backend,updatedB.replace(/\n/g,newlineB),'utf8')
  fs.writeFileSync(frontend,updatedF.replace(/\n/g,newlineF),'utf8')
  console.log('Đã sửa Backend:', path.relative(root,backend))
  console.log('Đã sửa Frontend:', path.relative(root,frontend))
  console.log('Đã sao lưu các file gốc:',backups.map(x=>path.relative(root,x.backup)).join(' | '))
  console.log('Sinh viên có thể nộp lại khi SUBMITTED; Milestone APPROVED vẫn khóa. Không thay đổi SQL Server.')
} catch(e) {
  for(const {f,backup} of backups) try { fs.copyFileSync(backup,f) } catch{}
  throw e
}
