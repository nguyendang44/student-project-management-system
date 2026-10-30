// v0.8.1 hotfix: resolve CS0136 in V08NotificationEngine without changing behavior.
const fs = require('node:fs');
const path = require('node:path');
const file = path.join(process.cwd(), 'backend', 'StudentProjects.Api', 'Services', 'V08NotificationEngine.cs');
if (!fs.existsSync(file)) {
  console.error(`Không tìm thấy file: ${file}. Hãy chạy script tại thư mục gốc dự án.`);
  process.exit(1);
}
const before = fs.readFileSync(file, 'utf8');
const oldBranch = `else if (a.EntityName == "GuidanceSession" && journalMap.TryGetValue(entityId, out var j)
                    && byProject.TryGetValue(j.ProjectId, out var p))
                {
                    if (a.Action == "GUIDANCE_LOG_CREATED")
                        Queue(j.CreatedByUserId == p.StudentUserId ? p.LecturerUserId : p.StudentUserId,
                            $"V08:MEET:{a.Id:N}", $"Có nhật ký buổi hướng dẫn mới ngày {TimeZoneInfo.ConvertTime(j.MeetingAt, Vietnam):dd/MM/yyyy}.");
                    else if (a.Action == "GUIDANCE_LOG_CONFIRMED")
                        Queue(p.StudentUserId, $"V08:MEETOK:{a.Id:N}", "Giảng viên đã xác nhận một nhật ký hướng dẫn.");
                }`;
const newBranch = oldBranch
  .replace('byProject.TryGetValue(j.ProjectId, out var p)', 'byProject.TryGetValue(j.ProjectId, out var journalProject)')
  .replace('j.CreatedByUserId == p.StudentUserId ? p.LecturerUserId : p.StudentUserId', 'j.CreatedByUserId == journalProject.StudentUserId ? journalProject.LecturerUserId : journalProject.StudentUserId')
  .replace('Queue(p.StudentUserId, $"V08:MEETOK:', 'Queue(journalProject.StudentUserId, $"V08:MEETOK:');
if (before.includes(newBranch)) {
  console.log('Đã áp dụng bản sửa trước đó. Không thay đổi file.');
  process.exit(0);
}
const count = before.split(oldBranch).length - 1;
if (count !== 1) {
  console.error(`Không tìm được khối code v0.8 gốc (số khối khớp: ${count}). Không sửa để tránh làm hỏng source.`);
  process.exit(2);
}
const backup = file + '.before-v0801-cs0136.bak';
if (!fs.existsSync(backup)) fs.copyFileSync(file, backup);
fs.writeFileSync(file, before.replace(oldBranch, newBranch), 'utf8');
console.log(`Đã sửa CS0136: ${file}`);
console.log(`Backup: ${backup}`);
