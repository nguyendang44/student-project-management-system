#!/usr/bin/env node
'use strict'
const fs = require('node:fs')
const path = require('node:path')
const root = path.resolve(__dirname, '..')
const target = path.join(root, 'frontend', 'src', 'views', 'ProjectProgressV07View.vue')
const replacement = path.join(__dirname, 'ProjectProgressV07View.v072.vue')
function fail(message) { console.error('Không cập nhật v0.7.2:', message); process.exit(1) }
if (!fs.existsSync(replacement) || !fs.existsSync(target)) fail('Không tìm thấy source v0.7.1 hoặc file bản vá. Giải nén ZIP vào thư mục gốc dự án.')
const oldContent = fs.readFileSync(target, 'utf8')
const newContent = fs.readFileSync(replacement, 'utf8')
const already = oldContent.includes('const isMilestonesPage = computed') && oldContent.includes('v-if="selected && isProgressPage"')
if (already) { console.log('v0.7.2 đã cài trước đó; không ghi đè hoặc sửa trùng.'); process.exit(0) }
for (const marker of ['ProjectProgressV07View', 'projectProgressApi', 'proposalReview', 'submitProgress', 'submitResult']) {
  if (!newContent.includes(marker) && marker !== 'ProjectProgressV07View') fail('File bản vá không đầy đủ: ' + marker)
}
for (const marker of ['projectProgressApi', 'proposalReview', 'submitProgress', 'submitResult', 'Milestones của Project', 'Cập nhật tiến độ', 'Nộp kết quả']) {
  if (!oldContent.includes(marker)) fail('Source hiện tại khác bản v0.7.1 ở phần ' + marker + '. Đã dừng để tránh ghi đè sửa đổi khác.')
}
if (!newContent.includes('isMilestonesPage') || !newContent.includes('isProgressPage') || !newContent.includes('isSubmissionsPage') || !newContent.includes('isEvaluationsPage')) fail('File bản vá thiếu bộ phân trang.')
const backup = target + '.before-v072-' + new Date().toISOString().replace(/[:.]/g, '-') + '.bak'
fs.copyFileSync(target, backup)
try {
  fs.writeFileSync(target, newContent, 'utf8')
  console.log('Đã cập nhật:', path.relative(root,target))
  console.log('Sao lưu:', path.relative(root,backup))
  console.log('Các mục: Milestone | Cập nhật tiến độ | Nộp kết quả | Đánh giá Milestone.')
  console.log('Không thay đổi Backend, API hoặc SQL Server.')
} catch (e) {
  fs.copyFileSync(backup,target)
  throw e
}
