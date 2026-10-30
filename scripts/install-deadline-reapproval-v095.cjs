'use strict';
// Student Projects v0.9.5: student milestone deadline edits + lecturer reapproval.
// Source-only patch, no database or migration operations.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const projectRoot = path.resolve(__dirname, '..');
const manifestPath = path.join(__dirname, 'deadline-reapproval-v095.patch.json');
const patches = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
const staged = [];
const count = (text, needle) => text.split(needle).length - 1;
try {
  for (const [relative, changes] of Object.entries(patches)) {
    const file = path.resolve(projectRoot, relative);
    if (!file.startsWith(projectRoot + path.sep) || !fs.existsSync(file)) {
      throw new Error(`Không tìm thấy source: ${relative}. Hãy giải nén ZIP đúng vào thư mục gốc dự án.`);
    }
    const original = fs.readFileSync(file, 'utf8');
    let updated = original;
    let changeCount = 0;
    for (const [i, c] of changes.entries()) {
      if (updated.includes(c.to)) continue; // already applied, safe to rerun
      if (count(updated, c.from) !== 1) {
        throw new Error(`Source không khớp: ${relative} (mục ${i+1}). Chưa sửa bất kỳ file nào. Hãy xác minh đã cài v0.9.4.1.`);
      }
      updated = updated.replace(c.from, c.to);
      changeCount++;
    }
    staged.push({file, relative, original, updated, changeCount});
  }
  const changed = staged.filter(x => x.changeCount);
  if (!changed.length) {
    console.log('PASS: v0.9.5 đã cài đặt trước đó. Không có thay đổi.');
    process.exit(0);
  }
  const stamp = new Date().toISOString().replace(/[:.]/g, '-');
  const backupDir = path.join(projectRoot, 'scripts', 'v095-backups', stamp);
  // Back up every affected file BEFORE modifying any file.
  for (const item of changed) {
    const backup = path.join(backupDir, item.relative);
    fs.mkdirSync(path.dirname(backup), {recursive:true});
    fs.writeFileSync(backup, item.original, 'utf8');
  }
  const committed = [];
  try {
    for (const item of changed) {
      const tmp = item.file + '.v095-' + crypto.randomBytes(6).toString('hex') + '.tmp';
      fs.writeFileSync(tmp, item.updated, 'utf8');
      fs.renameSync(tmp, item.file);
      committed.push(item);
      console.log(`PATCH ${item.relative}: ${item.changeCount} phần`);
    }
  } catch (error) {
    // Recover originals if a file write unexpectedly fails mid-install.
    for (const item of committed) fs.writeFileSync(item.file, item.original, 'utf8');
    throw error;
  }
  console.log(`PASS: v0.9.5. Sao lưu trước cập nhật tại ${backupDir}`);
  console.log('Khởi động lại Backend; chạy dotnet test và npm run build để nghiệm thu.');
} catch (error) {
  console.error('KHÔNG THỂ CÀI ĐẶT:', error.message);
  process.exitCode = 1;
}
