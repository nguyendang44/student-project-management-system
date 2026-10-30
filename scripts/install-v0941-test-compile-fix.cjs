#!/usr/bin/env node
'use strict';
// v0.9.4.1: Fix V09 test compile errors introduced by v0.9.4 tests.
// Changes only the integration test source. No database or production code changes.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '..');
const relative = 'backend/StudentProjects.IntegrationTests/V09SecurityAndProjectLifecycleTests.cs';
const target = path.join(root, relative);
const fail = message => { throw new Error(message); };
const count = (input, token) => input.split(token).length - 1;
try {
  if (!fs.existsSync(target)) fail(`Không tìm thấy ${relative}. Hãy giải nén ZIP vào thư mục gốc dự án.`);
  const original = fs.readFileSync(target, 'utf8');
  if (!original.includes('V094_REQUEST_NOTIFICATION_REGRESSION'))
    fail('Chưa tìm thấy tests v0.9.4 trong source; dừng để tránh sửa sai phiên bản.');
  const helperStart = '        private static void AddUser(StudentProjectsDbContext db, Guid id, string email, Guid role)';
  const helperEnd = '\n    }\n\n    private static async Task<HttpClient> Login';
  const helperBeginAt = original.indexOf(helperStart);
  const helperEndAt = original.indexOf(helperEnd, helperBeginAt);
  const fixedHelperStart = '    private static void AddUser(StudentProjectsDbContext db, Guid id, string email, Guid role)';
  const warningOld = 'Assert.Single(rows.Where(x => x.GetProperty("type").GetString() == $"V08:REQ:{requestId:N}"))';
  const warningNew = 'Assert.Single(rows, x => x.GetProperty("type").GetString() == $"V08:REQ:{requestId:N}")';
  const alreadyFixed = original.includes(fixedHelperStart) && !original.includes(helperStart) &&
    original.includes(warningNew) && !original.includes(warningOld);
  if (alreadyFixed) {
    console.log('PASS: v0.9.4.1 đã được áp dụng trước đó. Không thay đổi file.');
    process.exit(0);
  }
  if (count(original, helperStart) !== 1 || helperBeginAt < 0 || helperEndAt < 0 ||
    count(original, warningOld) !== 1 || count(original, warningNew) !== 0)
    fail('Cấu trúc file test khác bản v0.9.4 dự kiến. Chưa sửa gì; cần kiểm tra source hiện tại.');
  // Helper is extracted from the nested TestApp class into the containing test class.
  // Preserve the helper's body exactly; only reduce its indentation and move the TestApp closing brace.
  const helperBlock = original.slice(helperBeginAt, helperEndAt);
  const lines = helperBlock.split('\n');
  if (!lines[0].startsWith('        private static void AddUser') ||
    !lines.every(line => line === '' || line.startsWith('    ')) ||
    !helperBlock.trimEnd().endsWith('}'))
    fail('Không thể xác minh phần thân AddUser. Chưa thay đổi file.');
  const movedHelper = lines.map(line => line.startsWith('    ') ? line.slice(4) : line).join('\n');
  const updated = original.slice(0, helperBeginAt) +
    '    }\n\n' + movedHelper +
    '\n' + original.slice(helperEndAt + '\n    }\n'.length);
  const finalText = updated.replace(warningOld, warningNew);
  if (count(finalText, fixedHelperStart) !== 1 || count(finalText, helperStart) !== 0 ||
    count(finalText, warningNew) !== 1 || !finalText.includes('    private static async Task<HttpClient> Login'))
    fail('Tự kiểm tra sau sửa thất bại. Chưa ghi file.');
  const backupDir = path.join(root, 'scripts', 'v0941-backups', new Date().toISOString().replace(/[:.]/g, '-'));
  const backup = path.join(backupDir, relative);
  fs.mkdirSync(path.dirname(backup), {recursive: true});
  fs.writeFileSync(backup, original, 'utf8');
  const temp = `${target}.v0941.tmp-${crypto.randomBytes(4).toString('hex')}`;
  try {
    fs.writeFileSync(temp, finalText, 'utf8');
    fs.renameSync(temp, target);
  } catch (err) {
    if (fs.existsSync(temp)) fs.unlinkSync(temp);
    throw err;
  }
  console.log(`PASS: Đã sửa ${relative}`);
  console.log('Đã sửa: 6 lỗi CS0103 (AddUser) và 1 cảnh báo xUnit2031.');
  console.log(`Đã sao lưu: ${backup}`);
  console.log('Chạy lại: cd backend && dotnet test .\\StudentProjects.IntegrationTests\\StudentProjects.IntegrationTests.csproj');
} catch (err) {
  console.error('KHÔNG CÀI ĐƯỢC v0.9.4.1:', err.message);
  process.exitCode = 1;
}
