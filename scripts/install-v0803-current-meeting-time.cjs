#!/usr/bin/env node
'use strict';
// v0.8.3: Populate the meeting date/time form with the current browser-local time.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const relative = 'frontend/src/views/GuidanceV08View.vue';
const filename = path.join(root, relative);
const marker = 'V0803_DEFAULT_CURRENT_MEETING_TIME';

if (!fs.existsSync(filename)) throw new Error(`Không tìm thấy ${relative}. Hãy giải nén vào thư mục gốc dự án.`);
const original = fs.readFileSync(filename, 'utf8');
if (original.includes(marker)) {
  console.log('v0.8.3 đã cài đặt; không chỉnh sửa trùng.');
  process.exit(0);
}
const oldLine = "const newForm = () => ({ meetingDate: '', meetingHour: '12', meetingMinute: '00', meetingPeriod: 'PM' as 'AM' | 'PM', summary: '', feedback: '', nextActions: '' })";
if (original.split(oldLine).length !== 2) {
  throw new Error('Không tìm thấy duy nhất đoạn v0.8.2 cần cập nhật. File có thể đã thay đổi; chưa sửa gì.');
}
const replacement = `// ${marker}: Reset the default to the time when the form is created.
// Use browser-local components, matching the existing local-to-ISO submit conversion.
const pad2 = (value: number): string => String(value).padStart(2, '0')
const newForm = () => {
  const now = new Date()
  const hour24 = now.getHours()
  return {
    meetingDate: now.getFullYear() + '-' + pad2(now.getMonth() + 1) + '-' + pad2(now.getDate()),
    meetingHour: pad2(hour24 % 12 || 12),
    meetingMinute: pad2(now.getMinutes()),
    meetingPeriod: (hour24 >= 12 ? 'PM' : 'AM') as 'AM' | 'PM',
    summary: '', feedback: '', nextActions: ''
  }
}`;
const updated = original.replace(oldLine, replacement);
const backupPath = filename + '.before-v0803-' + new Date().toISOString().replace(/[:.]/g, '-') + '.bak';
fs.copyFileSync(filename, backupPath);
try {
  fs.writeFileSync(filename, updated, 'utf8');
} catch (err) {
  fs.copyFileSync(backupPath, filename);
  throw err;
}
console.log('Đã cài v0.8.3: Ngày giờ gặp mặc định là thời gian hiện tại khi mở form hoặc sau khi lưu.');
console.log('Đã sao lưu file: ' + backupPath);
