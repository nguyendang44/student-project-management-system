/**
 * v0.7.7: Student with a single Project gets a static project label
 * in the ProjectMilestoneSummary component instead of "Chọn Project".
 * Lecturer/Admin and Student with multiple projects retain the selector.
 * Minimal, backed-up, idempotent Frontend-only patch.
 */
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const target = path.join(root, 'frontend', 'src', 'components', 'ProjectMilestoneSummary.vue');
if (!fs.existsSync(target)) {
  console.error('Không tìm thấy ProjectMilestoneSummary.vue. Hãy giải nén ZIP vào đúng thư mục gốc dự án đã cài v0.7.4 trở lên.');
  process.exit(1);
}
const source = fs.readFileSync(target, 'utf8');
const windowsNewlines = source.includes('\r\n');
let updated = source.replace(/\r\n/g, '\n');
const mark = '<!-- V077_STUDENT_SINGLE_PROJECT_OVERVIEW -->';
if (updated.includes(mark)) {
  console.log('v0.7.7 đã được cài, không sửa trùng.');
  process.exit(0);
}
function expectOnce(needle, desc) {
  const a = updated.indexOf(needle);
  if (a < 0 || updated.indexOf(needle, a + needle.length) !== -1) {
    console.error(`Không thể tìm duy nhất ${desc} trong component hiện tại. Dừng, chưa sửa file.`);
    process.exit(1);
  }
}
const importAnchor = "import { computed, onMounted, ref, watch } from 'vue'";
const constAnchor = "const projects = ref<ProjectV07[]>([])";
const dropdown = `    <label class="spms-project-label">Chọn Project
      <select v-model="projectId" :disabled="loading">
        <option v-if="!projects.length" value="">Chưa có Project</option>
        <option v-for="p in projects" :key="p.id" :value="p.id">{{p.topicTitle}} · {{p.studentName}} · {{p.lecturerName}}</option>
      </select>
    </label>`;
const styleAnchor = '.spms-project-label select{padding:11px 12px;border:1px solid #dce4ee;border-radius:8px;background:#fff;max-width:100%;min-width:0}';
expectOnce(importAnchor, 'Vue import');
expectOnce(constAnchor, 'Project state');
expectOnce(dropdown, 'ô Chọn Project');
expectOnce(styleAnchor, 'CSS Project selector');
updated = updated.replace(importAnchor, importAnchor + "\nimport { useAuthStore } from '../stores/auth'");
updated = updated.replace(constAnchor, 'const auth = useAuthStore()\n' + constAnchor);
updated = updated.replace(dropdown, `    ${mark}
    <div v-if="auth.role === 'Student' && projects.length === 1" class="spms-project-label">
      <span>Project của bạn</span>
      <div class="spms-project-static">
        <strong>{{projects[0].topicTitle}}</strong> · {{projects[0].studentName}} · {{projects[0].lecturerName}}
      </div>
    </div>
    <label v-else-if="auth.role !== 'Student' || projects.length > 1" class="spms-project-label">Chọn Project
      <select v-model="projectId" :disabled="loading">
        <option v-if="!projects.length" value="">Chưa có Project</option>
        <option v-for="p in projects" :key="p.id" :value="p.id">{{p.topicTitle}} · {{p.studentName}} · {{p.lecturerName}}</option>
      </select>
    </label>`);
updated = updated.replace(styleAnchor, styleAnchor + '\n.spms-project-static{padding:11px 12px;border:1px solid #dce4ee;border-radius:8px;background:#f8fafc;line-height:1.5;font-weight:400}');
const output = windowsNewlines ? updated.replace(/\n/g, '\r\n') : updated;
const stamp = new Date().toISOString().replace(/[:.]/g, '-');
const backup = `${target}.before-v077-${stamp}.bak`;
fs.copyFileSync(target, backup);
try {
  fs.writeFileSync(target, output, 'utf8');
  console.log('Đã sửa: frontend/src/components/ProjectMilestoneSummary.vue');
  console.log('Đã sao lưu:', backup);
  console.log('Student có 1 Project: tự chọn, không còn ô chọn Project trong Tiến độ tổng hợp dự án.');
} catch (e) {
  fs.copyFileSync(backup, target);
  throw e;
}
