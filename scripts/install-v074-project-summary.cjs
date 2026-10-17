/**
 * v0.7.4 project milestone summary + allow creating new milestone on completed projects.
 * Run from project root: node scripts/install-v074-project-summary.cjs
 * No EF/SQL migration, source-only with per-file backups and idempotency.
 */
const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const paths = {
  projects: path.join(root, 'frontend/src/views/LecturerSupervisionView.vue'),
  milestoneView: path.join(root, 'frontend/src/views/ProjectProgressV07View.vue'),
  endpoint: path.join(root, 'backend/StudentProjects.Api/Endpoints/ProjectProgressV07Endpoints.cs'),
  component: path.join(root, 'frontend/src/components/ProjectMilestoneSummary.vue'),
  api: path.join(root, 'frontend/src/features/progress/projectProgress.api.ts')
};
const ensure = (condition, message) => { if (!condition) throw new Error(message) };
const src = {};
for (const [key, p] of Object.entries(paths)) {
  ensure(fs.existsSync(p), `Thiếu file ${p}. Hãy giải nén đúng thư mục root, trên v0.7.3.`);
  src[key] = fs.readFileSync(p, 'utf8');
}
ensure(src.projects.includes("moduleId === 'projects'"), 'Không tìm thấy trang Dự án của v0.6.');
ensure(src.api.includes('projectProgressApi'), 'API tiến độ v0.7 chưa được cài.');
ensure(src.endpoint.includes('private static async Task<IResult> CreateMilestone'), 'Backend v0.7 chưa tương thích.');
ensure(src.milestoneView.includes('canWork'), 'Milestone Vue v0.7 chưa tương thích.');

const changes = [];
function schedule(key, output) { if (src[key] !== output) changes.push({key, before:src[key], after:output}); }

let projects = src.projects;
const tag = '  <ProjectMilestoneSummary v-if="moduleId === \'projects\'" />';
if (!projects.includes('ProjectMilestoneSummary')) {
  const tagEnd = '</template>';
  ensure(projects.includes(tagEnd), 'Không tìm thấy </template> trong trang Dự án.');
  const importAnchor = "import { computed, onMounted, ref, watch } from 'vue'";
  ensure(projects.includes(importAnchor), 'Không tìm thấy vùng imports của trang Dự án.');
  projects = projects.replace(importAnchor, importAnchor + "\nimport ProjectMilestoneSummary from '../components/ProjectMilestoneSummary.vue'");
  const at = projects.lastIndexOf(tagEnd);
  projects = projects.slice(0,at) + tag + '\n' + projects.slice(at);
} else {
  ensure(projects.includes(tag), 'Trang dự án đã có ProjectMilestoneSummary nhưng định dạng không tương thích.');
}
schedule('projects', projects);

let view = src.milestoneView;
if (!view.includes('const canAddMilestone = computed(')) {
  const needle = "const canWork = computed(() => selectedProject.value && ['REGISTERED','IN_PROGRESS','DRAFT'].includes(selectedProject.value.status))";
  ensure(view.includes(needle), 'Không tìm thấy canWork của trang Milestone.');
  view = view.replace(needle, needle + "\nconst canAddMilestone = computed(() => selectedProject.value && ['REGISTERED','IN_PROGRESS','DRAFT','COMPLETED'].includes(selectedProject.value.status))");
}
const createButton = /(<button\b[^>]*v-if="\['Lecturer','Student'\]\.includes\(role \|\| ''\) && )canWork("[^>]*\btype="button"[^>]*@click="newMilestone"[^>]*>)/;
if (createButton.test(view)) view = view.replace(createButton, '$1canAddMilestone$2');
else ensure(/v-if="\['Lecturer','Student'\]\.includes\(role \|\| ''\) && canAddMilestone"[^>]*@click="newMilestone"/.test(view), 'Không nhận dạng được nút + Tạo / Đề xuất Milestone.');
schedule('milestoneView', view);

let endpoint = src.endpoint;
const beg = endpoint.indexOf('private static async Task<IResult> CreateMilestone(');
const end = endpoint.indexOf('private static async Task<IResult> UpdateMilestone(',beg);
ensure(beg >= 0 && end > beg, 'Không tìm thấy method CreateMilestone.');
let creation = endpoint.slice(beg,end);
const oldGuard = 'if (!Active(project)) return Conflict("Project đã kết thúc, không thể thêm Milestone.");';
const newGuard = 'if (project.Status == ProjectStatus.CANCELLED) return Conflict("Project đã hủy, không thể thêm Milestone.");';
if (creation.includes(oldGuard)) creation = creation.replace(oldGuard,newGuard);
else ensure(creation.includes(newGuard), 'Không tìm thấy điều kiện chặn Project hoàn thành.');
const reopen = `        if (project.Status == ProjectStatus.COMPLETED)
        {
            project.Status = ProjectStatus.IN_PROGRESS;
            project.UpdatedAt = DateTimeOffset.UtcNow;
            Audit(db, Actor(principal), "PROJECT_REOPENED_FOR_NEW_MILESTONE", "Project", project.Id);
        }
`;
if (!creation.includes('PROJECT_REOPENED_FOR_NEW_MILESTONE')) {
  ensure(creation.includes('        db.Milestones.Add(item);'), 'Không tìm thấy vị trí lưu Milestone.');
  creation = creation.replace('        db.Milestones.Add(item);', reopen + '        db.Milestones.Add(item);');
}
endpoint = endpoint.slice(0,beg)+creation+endpoint.slice(end);
// Summary must count unfinished, unconfirmed Milestones as 0% so a new proposal doesn't leave a 100% Project bar.
const percentOld = /PercentComplete = milestones\.Any\(m => m\.ProjectId == p\.Id && m\.ProposalStatus == "CONFIRMED"\)\s*\? \(int\)Math\.Round\(milestones\.Where\(m => m\.ProjectId == p\.Id && m\.ProposalStatus == "CONFIRMED"\)\s*\.Average\(m => \(double\)m\.PercentComplete\)\) : 0/;
if (percentOld.test(endpoint)) {
  endpoint = endpoint.replace(percentOld, `PercentComplete = milestones.Any(m => m.ProjectId == p.Id)
                ? (int)Math.Round(milestones.Where(m => m.ProjectId == p.Id)
                    .Average(m => (double)m.PercentComplete)) : 0`);
} else ensure(endpoint.includes('PercentComplete = milestones.Any(m => m.ProjectId == p.Id)'), 'Không thể nhận dạng công thức tiến độ tổng hợp.');
schedule('endpoint',endpoint);

if (!changes.length) { console.log('v0.7.4 đã được cài, không sửa trùng.');process.exit(0); }
const stamp = new Date().toISOString().replace(/[:.]/g,'-');
const written = [];
try {
  for (const change of changes) {
    const target=paths[change.key];
    fs.copyFileSync(target,`${target}.before-v074-${stamp}.bak`);
    fs.writeFileSync(target,change.after,'utf8');
    written.push(change);
    console.log('Đã cập nhật:',path.relative(root,target));
  }
  console.log('v0.7.4 cài đặt hoàn tất. Sao lưu: *.before-v074-*.bak');
} catch (e) {
  for (const item of written.reverse()) fs.writeFileSync(paths[item.key],item.before,'utf8');
  console.error('Đã hoàn nguyên các file vừa thay đổi vì có lỗi.');
  throw e;
}
