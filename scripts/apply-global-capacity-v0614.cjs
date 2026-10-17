#!/usr/bin/env node
// Non-destructive targeted patch for an existing v0.6.13 checkout.
// No SQL reset, no migrations, preserves edits in other Vue modules.
const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const target = path.join(root, 'frontend/src/views/LecturerSupervisionView.vue');
const program = path.join(root, 'backend/StudentProjects.Api/Program.cs');
const endpoint = path.join(root, 'backend/StudentProjects.Api/Endpoints/GlobalLecturerCapacityEndpoints.cs');
const client = path.join(root, 'frontend/src/features/capacity/globalCapacity.api.ts');
function fail(s) { throw new Error('Không áp dụng bản vá: ' + s); }
function once(data, oldText, newText, label) {
  const n = data.split(oldText).length - 1;
  if (n !== 1) fail(label + ' cần đúng 1 vị trí, hiện thấy ' + n + '. Không ghi đè file.');
  return data.replace(oldText, newText);
}
if (!fs.existsSync(target) || !fs.existsSync(program) || !fs.existsSync(endpoint) || !fs.existsSync(client)) {
  fail('Thiếu file dự án hoặc chưa giải nén đủ nội dung ZIP vào thư mục gốc.');
}
let view = fs.readFileSync(target,'utf8');
let app = fs.readFileSync(program,'utf8');
if (view.includes('globalCapacityApi.list()') && app.includes('app.MapGlobalLecturerCapacityEndpoints();')) {
  console.log('v0.6.14 đã được áp dụng. Không làm gì thêm.'); process.exit(0);
}
if (view.includes('globalCapacityApi.list()') || app.includes('app.MapGlobalLecturerCapacityEndpoints();')) fail('Chỉ một phần bản vá đã tồn tại. Hãy kiểm tra file trước khi chạy lại.');
view = once(view,
  "import { supervisionApi } from '../features/supervision/supervision.api'",
  "import { supervisionApi } from '../features/supervision/supervision.api'\nimport { globalCapacityApi } from '../features/capacity/globalCapacity.api'",
  'import API');
view = once(view,
  'const maxStudents = ref(1)',
  'const maxStudents = ref(1)\nconst adminCapacityEdits = ref<Record<string, number>>({})',
  'state sức chứa admin');
view = once(view,
  '    periods.value = await periodsApi.list()',
  `    // Global capacity is independent of registration periods, even when all are closed.
    if (props.moduleId === 'capacity') {
      capacities.value = await globalCapacityApi.list()
      maxStudents.value = ownCapacity.value?.maxStudents ?? 1
      adminCapacityEdits.value = Object.fromEntries(capacities.value.map(c => [c.lecturerUserId, c.maxStudents]))
      return
    }
    periods.value = await periodsApi.list()`,
  'load capacity independent of periods');
view = once(view,
  "    if (props.moduleId === 'capacity') {\n      capacities.value = periodId.value ? await supervisionApi.capacities(periodId.value) : []\n      maxStudents.value = ownCapacity.value?.maxStudents ?? 1\n    }\n",
  '',
  'xóa fetch sức chứa theo đợt');
view = once(view,
  "  if (!periodId.value) { error.value = 'Chọn đợt đăng ký.'; return }\n",
  '',
  'xóa yêu cầu chọn đợt để lưu');
view = once(view,
  '    await supervisionApi.updateCapacity(periodId.value, maxStudents.value)',
  `    if (!ownCapacity.value) { error.value = 'Không tìm thấy tài khoản giảng viên.'; return }
    await globalCapacityApi.update(ownCapacity.value.lecturerUserId, maxStudents.value)`,
  'API ghi sức chứa global');
view = once(view,
  'function startRevision(item: LecturerRequestV06) {',
  `async function saveAdminCapacity(lecturerId: string) {
  error.value = ''; success.value = ''
  const selected = capacities.value.find(c => c.lecturerUserId === lecturerId)
  const target = adminCapacityEdits.value[lecturerId]
  if (!selected || !Number.isInteger(target) || target < selected.currentStudents || target > 1000) {
    error.value = 'Sức chứa phải là số nguyên từ số sinh viên đang hướng dẫn đến 1000.'; return
  }
  loading.value = true
  try {
    await globalCapacityApi.update(lecturerId, target)
    await load()
    success.value = 'Admin đã cập nhật giới hạn hướng dẫn cho giảng viên.'
  } catch (e) { error.value = message(e) } finally { loading.value = false }
}
function startRevision(item: LecturerRequestV06) {`,
  'thêm thao tác Admin');
view = once(view,
  "watch(periodId, () => { if (props.moduleId === 'capacity' || props.moduleId === 'lecturerrequests') void load() })",
  "watch(periodId, () => { if (props.moduleId === 'lecturerrequests') void load() })",
  'watch selected period');
view = once(view,
  `  <article v-if="moduleId !== 'projects' && moduleId !== 'lecturerconfirmation' && role !== 'Student'" class="panel users-panel">`,
  `  <article v-if="moduleId === 'lecturerrequests' && role !== 'Student'" class="panel users-panel">`,
  'ẩn chọn đợt trong trang sức chứa');
view = once(view,
  `<article v-if="moduleId === 'capacity' && role === 'Lecturer' && periodId"`,
  `<article v-if="moduleId === 'capacity' && role === 'Lecturer'"`,
  'cho Lecturer sửa bất cứ lúc nào');
view = once(view,
  '<th v-if="role === \'Student\'">Thao tác</th></tr></thead><tbody>',
  '<th v-if="role === \'Student\'">Thao tác</th><th v-if="role === \'Admin\'">Chỉnh sức chứa</th></tr></thead><tbody>',
  'cột quản lý Admin');
view = once(view,
  `<td :colspan="role === 'Student' ? 5 : 4" class="empty-cell">`,
  `<td :colspan="role === 'Student' || role === 'Admin' ? 5 : 4" class="empty-cell">`,
  'số cột bảng');
view = once(view,
  `<td v-if="role === 'Student'"><button type="button" class="action-primary" :disabled="loading || c.remaining <= 0" @click="startLecturerRegistration(c)">Đăng ký hướng dẫn</button></td></tr>`,
  `<td v-if="role === 'Student'"><button type="button" class="action-primary" :disabled="loading || c.remaining <= 0" @click="startLecturerRegistration(c)">Đăng ký hướng dẫn</button></td>
        <td v-if="role === 'Admin'">
          <form class="capacity-inline-form" @submit.prevent="saveAdminCapacity(c.lecturerUserId)">
            <input v-model.number="adminCapacityEdits[c.lecturerUserId]" type="number" min="0" max="1000" step="1"
              :aria-label="'Sức chứa giảng viên ' + c.lecturerName" required style="max-width: 110px; margin-right: 8px" />
            <button class="action-primary" type="submit" :disabled="loading">Lưu</button>
          </form>
        </td></tr>`,
  'ô nhập lưu cho Admin');
app = once(app, 'app.MapAuthEndpoints();', 'app.MapAuthEndpoints();\napp.MapGlobalLecturerCapacityEndpoints();', 'đăng ký endpoint');
const stamp = new Date().toISOString().replace(/[:.]/g, '-');
const backups = [];
try {
  for (const file of [target,program]) {
    const backup=file+'.pre-v0614-'+stamp+'.bak';
    fs.copyFileSync(file,backup); backups.push(backup);
  }
  fs.writeFileSync(target,view,'utf8');
  fs.writeFileSync(program,app,'utf8');
  console.log('Đã cập nhật v0.6.14.');
  console.log('Đã sao lưu:', backups.join('\n '));
  console.log('Frontend Sức chứa: bỏ chọn đợt; Lecturer sửa mức chung; Admin sửa từng Lecturer.');
  console.log('API mới: GET /api/v1/lecturer-capacities/global, PUT .../global/{lecturerId}.');
} catch(e) {
  if (backups.length === 2) {
    fs.copyFileSync(backups[0],target); fs.copyFileSync(backups[1],program);
  }
  throw e;
}
