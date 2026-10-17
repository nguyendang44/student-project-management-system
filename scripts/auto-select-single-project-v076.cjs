/**
 * v0.7.6 - For a student owning a single project, show it without a selector.
 * Applies a minimal, idempotent patch to the existing v0.7.5 Vue screen.
 * Does not edit Backend/SQL or change stored project/milestone data.
 */
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const viewFile = path.join(root, 'frontend', 'src', 'views', 'ProjectProgressV07View.vue');
if (!fs.existsSync(viewFile)) {
  console.error('Không tìm thấy frontend/src/views/ProjectProgressV07View.vue. Hãy đặt script vào đúng thư mục gốc dự án.');
  process.exit(1);
}
const source = fs.readFileSync(viewFile, 'utf8');
const normalized = source.replace(/\r\n/g, '\n');
const marker = '<!-- V076_STUDENT_SINGLE_PROJECT_AUTO_SELECT -->';
const header = '    <h3>Chọn Project</h3>';
const loadingLabel = '    <label v-else>Project của {{role === \'Student\' ? \'bạn\' : role === \'Lecturer\' ? \'sinh viên bạn hướng dẫn\' : \'hệ thống\'}}';
const approvedLabel = '<span>Đã duyệt: <strong>{{selectedProject.approvedCount}}/{{selectedProject.milestoneCount}}</strong></span>';
if (normalized.includes(marker)) {
  console.log('v0.7.6 đã được cài. Không sửa trùng.');
  process.exit(0);
}
if (!normalized.includes(header) || !normalized.includes(loadingLabel) || !normalized.includes(approvedLabel)) {
  console.error('Cấu trúc trang Project đã khác bản v0.7.5; dừng để tránh ghi đè hoặc sửa nhầm.');
  process.exit(1);
}
let updated = normalized.replace(header, `    ${marker}\n    <h3>{{role === 'Student' && projects.length === 1 ? 'Project của bạn' : 'Chọn Project'}}</h3>`);
updated = updated.replace(loadingLabel, `    <p v-else-if="role === 'Student' && projects.length === 1" class="v07-single-project">
      <strong>{{projects[0].topicTitle}}</strong> · {{projects[0].studentName}} · {{projects[0].lecturerName}}
    </p>
${loadingLabel}`);
updated = updated.replace(approvedLabel, '<span>Milestone đã duyệt: <strong>{{selectedProject.approvedCount}}/{{selectedProject.milestoneCount}}</strong></span>');
if (updated === normalized || !updated.includes('v-else-if="role === \'Student\' && projects.length === 1"')) {
  console.error('Không thể cài đặt an toàn; file gốc chưa bị thay đổi.');
  process.exit(1);
}
// Preserve Windows CRLF and original UTF-8 BOM when applicable.
const output = source.includes('\r\n') ? updated.replace(/\n/g, '\r\n') : updated;
const stamp = new Date().toISOString().replace(/[:.]/g, '-');
const backup = `${viewFile}.before-v076-${stamp}.bak`;
fs.copyFileSync(viewFile, backup);
fs.writeFileSync(viewFile, output, 'utf8');
console.log('Đã cập nhật: frontend/src/views/ProjectProgressV07View.vue');
console.log('Đã sao lưu: ' + backup);
console.log('Student có 1 Project: tự chọn, không có dropdown; các vai trò và API giữ nguyên.');
