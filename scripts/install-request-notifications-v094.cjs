'use strict';
// v0.9.4 targeted source patch. Never touches the Database.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '..');
const patches = JSON.parse(fs.readFileSync(path.join(__dirname, 'request-notification-v094.patch.json'), 'utf8'));
const targets = [];
function count(haystack, needle) { return haystack.split(needle).length - 1; }
try {
  for (const [relative, changes] of Object.entries(patches)) {
    const destination = path.resolve(root, relative);
    if (!destination.startsWith(root + path.sep) || !fs.existsSync(destination))
      throw new Error(`Không tìm thấy file source: ${relative}. Giải nén bản vá vào thư mục gốc dự án.`);
    const original = fs.readFileSync(destination, 'utf8');
    let updated = original, needed = 0;
    for (const [index, c] of changes.entries()) {
      if (updated.includes(c.to)) continue; // idempotent
      if (count(updated, c.from) !== 1)
        throw new Error(`Source không khớp tại ${relative} (thay đổi ${index + 1}). Chưa ghi file nào; hãy sao lưu và kiểm tra phiên bản.`);
      updated = updated.replace(c.from, c.to);
      needed++;
    }
    targets.push({ relative, destination, original, updated, needed });
  }
  if (targets.every(t => t.needed === 0)) {
    console.log('v0.9.4 đã được áp dụng trước đó, không có gì thay đổi.');
    process.exit(0);
  }
  const stamp = new Date().toISOString().replace(/[:.]/g, '-');
  const backupDir = path.join(root, 'scripts', 'v094-backups', stamp);
  for (const t of targets.filter(t => t.needed > 0)) {
    const backup = path.join(backupDir, t.relative);
    fs.mkdirSync(path.dirname(backup), { recursive: true });
    fs.writeFileSync(backup, t.original, 'utf8');
  }
  for (const t of targets.filter(t => t.needed > 0)) {
    const temp = `${t.destination}.v094.tmp-${crypto.randomBytes(4).toString('hex')}`;
    fs.writeFileSync(temp, t.updated, 'utf8');
    fs.renameSync(temp, t.destination);
    console.log(`PATCH ${t.relative} (${t.needed} phần)`);
  }
  console.log(`PASS: v0.9.4 đã cài. Sao lưu: ${backupDir}`);
  console.log('Khởi động lại Backend; chạy dotnet test và npm run build trước khi nghiệm thu.');
} catch (err) {
  console.error('KHÔNG THỂ ÁP DỤNG BẢN VÁ:', err.message);
  process.exitCode = 1;
}
