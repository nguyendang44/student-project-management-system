'use strict';
// v0.9.6: relocate student milestone deadline editor into Milestone details.
// Frontend-only, no database operations. Installation is idempotent and backed up.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '..');
const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, 'deadline-in-details-v096.patch.json'), 'utf8'));
function occurrences(s, frag) { return s.split(frag).length - 1; }
const staged = [];
try {
  for (const [relative, changes] of Object.entries(manifest)) {
    const filename = path.resolve(root, relative);
    if (!filename.startsWith(root + path.sep) || !fs.existsSync(filename)) {
      throw new Error('Không tìm thấy ' + relative + '. Giải nén vào thư mục gốc dự án.');
    }
    const original = fs.readFileSync(filename, 'utf8');
    let updated = original;
    let count = 0;
    for (let i = 0; i < changes.length; i++) {
      const {from, to} = changes[i];
      if (occurrences(updated, from) === 1) {
        updated = updated.replace(from, to);
        count++;
      } else if (updated.includes(to)) {
        // This exact change was already installed.
      } else {
        throw new Error('Source không khớp: ' + relative + ' tại thay đổi ' + (i+1) + '. Yêu cầu đã cài v0.9.5. Chưa sửa file nào.');
      }
    }
    staged.push({filename, relative, original, updated, count});
  }
  const changed = staged.filter(item => item.count > 0);
  if (!changed.length) {
    console.log('PASS: v0.9.6 đã cài trước đó. Không có thay đổi.');
    process.exit(0);
  }
  const stamp = new Date().toISOString().replace(/[:.]/g, '-') + '-' + crypto.randomBytes(3).toString('hex');
  const backupRoot = path.join(root, 'scripts', 'v096-backups', stamp);
  for (const item of changed) {
    const dst = path.join(backupRoot, item.relative);
    fs.mkdirSync(path.dirname(dst), {recursive: true});
    fs.writeFileSync(dst, item.original, 'utf8');
  }
  const written = [];
  try {
    for (const item of changed) {
      const temp = item.filename + '.v096-' + crypto.randomBytes(5).toString('hex') + '.tmp';
      fs.writeFileSync(temp, item.updated, 'utf8');
      try { fs.renameSync(temp, item.filename); }
      catch(e) { fs.rmSync(temp, {force: true}); throw e; }
      written.push(item);
      console.log('PATCH ' + item.relative + ': ' + item.count + ' mục.');
    }
  } catch(e) {
    for (const item of written) fs.writeFileSync(item.filename, item.original, 'utf8');
    throw e;
  }
  console.log('PASS v0.9.6. Đã sao lưu tại ' + backupRoot);
  console.log('Tiếp theo: cd frontend && npm run build');
} catch(e) {
  console.error('KHÔNG THỂ CÀI ĐẶT:', e.message);
  process.exitCode = 1;
}
