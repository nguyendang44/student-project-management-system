/** v0.7.8: Install 8x6 editable Milestone templates on v0.7.7.
 * Run from project root: node scripts/install-milestone-templates-v078.cjs
 * Verifies every source anchor before writing, backups per file, idempotent.
 */
'use strict'
const fs = require('fs')
const path = require('path')
const root = path.resolve(__dirname, '..')
const targets = {
  api: path.join(root,'frontend/src/features/progress/projectProgress.api.ts'),
  view: path.join(root,'frontend/src/views/ProjectProgressV07View.vue'),
  backend: path.join(root,'backend/StudentProjects.Api/Endpoints/ProjectProgressV07Endpoints.cs')
}
for (const filename of ['frontend/src/components/MilestoneTemplatePlanner.vue', 'frontend/src/features/progress/milestoneTemplates.ts']) {
  if (!fs.existsSync(path.join(root, filename))) throw new Error('Thiếu file mới của v0.7.8: '+filename)
}
const assert = (ok, why) => { if (!ok) throw new Error(`v0.7.8: ${why}`) }
const original = {}
for (const [key,file] of Object.entries(targets)) {
  assert(fs.existsSync(file), `Không tìm thấy ${path.relative(root,file)}. Hãy cài bản v0.7.7 trước.`)
  original[key] = fs.readFileSync(file,'utf8')
}
const MARK_VIEW = '// V078_MILESTONE_TEMPLATE_LIBRARY'
const MARK_API = '// V078_MILESTONE_TEMPLATE_API'
const MARK_BE = '// V078_MILESTONE_TEMPLATE_BATCH'
const normalize = source => source.replace(/\r\n/g,'\n')
const restoreEol = (text,base) => base.includes('\r\n') ? text.replace(/\n/g,'\r\n') : text
function replaceOne(s,a,b,msg) {
  const at=s.indexOf(a)
  assert(at>=0,`Thiếu đoạn mã: ${msg}`)
  assert(s.indexOf(a,at+a.length)<0,`Đoạn mã bị trùng: ${msg}`)
  return s.slice(0,at)+b+s.slice(at+a.length)
}
const result = {}
if (!original.api.includes(MARK_API)) {
  let src=normalize(original.api)
  assert(src.includes('create: (id: string, data: MilestoneForm)'), 'API thiếu create() của Milestone.')
  const insert=`  ${MARK_API}
  /** Atomic creation: all selected Milestones are saved together or none are. */
  createBatch: (id: string, items: MilestoneForm[]) =>
    request<{ createdCount: number; ids: string[] }>(
      \`/v07/projects/\${id}/milestone-plan\`,
      {method:'POST',body:JSON.stringify({items})}
    ),
`
  src=replaceOne(src,'  create: (id: string, data: MilestoneForm)',insert+'  create: (id: string, data: MilestoneForm)','projectProgressApi.create')
  result.api=restoreEol(src,original.api)
}
if (!original.view.includes(MARK_VIEW)) {
  let src=normalize(original.view)
  const importAnchor='<script setup lang="ts">\n'
  assert(src.includes(importAnchor),'Component không dùng Vue script setup.')
  src=replaceOne(src,importAnchor,importAnchor + `// v0.7.8: editable 8x6 Milestone templates.\nimport MilestoneTemplatePlanner from '../components/MilestoneTemplatePlanner.vue'\n`, 'imports')
  src=replaceOne(src,'const showMilestoneForm = ref(false)',
    `const showMilestoneForm = ref(false)\n${MARK_VIEW}\nconst showTemplatePlanner = ref(false)`, 'form milestone')
  const createButtonLines=src.split('\n').filter(line=>line.includes('@click="newMilestone"'))
  assert(createButtonLines.length===1, 'Không xác định được nút Tạo/Đề xuất Milestone trên v0.7.7.')
  const createLine=createButtonLines[0]
  const actionLine=`      <button v-if="['Lecturer','Student'].includes(role || '') && canAddMilestone" type="button" @click="showTemplatePlanner = !showTemplatePlanner; showMilestoneForm = false">{{role === 'Student' ? '+ Đề xuất theo mẫu' : '+ Tạo từ mẫu'}}</button>`
  src=replaceOne(src,createLine,createLine+'\n'+actionLine,'nút Tạo Milestone')
  const formAnchor='    <form v-if="[\'Lecturer\',\'Student\'].includes(role || \'\') && showMilestoneForm"'
  assert(src.includes(formAnchor),'Không tìm thấy form tạo Milestone.')
  const componentMarkup=`    <MilestoneTemplatePlanner v-if="showTemplatePlanner && canAddMilestone && ['Lecturer','Student'].includes(role || '')"\n      :project-id="projectId" :role="role === 'Student' ? 'Student' : 'Lecturer'"\n      :existing-titles="milestones.map(m => m.title)"\n      @applied="onTemplatesApplied" @close="showTemplatePlanner = false" />\n`
  src=replaceOne(src,formAnchor,componentMarkup+formAnchor,'chèn bộ mẫu bên trên form')
  // Manage one editor at a time. Do not change the manual Milestone save flow.
  src=replaceOne(src,'function newMilestone() {',
    'function newMilestone() {\n  showTemplatePlanner.value = false', 'mở tạo mốc thủ công')
  src=replaceOne(src,'function editMilestone() {',
    'function editMilestone() {\n  showTemplatePlanner.value = false', 'sửa mốc thủ công')
  src=replaceOne(src,'async function loadMilestones() {',
    'async function loadMilestones() {\n  showTemplatePlanner.value = false', 'đổi Project')
  const func=`async function onTemplatesApplied(count: number) {\n  showTemplatePlanner.value = false\n  await loadProjects()\n  success.value = role.value === 'Student'\n    ? \`Đã gửi \${count} Milestone đề xuất để giảng viên xem xét.\`\n    : \`Đã tạo \${count} Milestone từ mẫu.\`\n}\n`
  src=replaceOne(src,'function newMilestone() {',func+'function newMilestone() {','handler áp dụng mẫu')
  result.view=restoreEol(src,original.view)
}
if (!original.backend.includes(MARK_BE)) {
  let src=normalize(original.backend)
  assert(src.includes('EndOfDeadlineDay('), 'Thiếu chuẩn hóa Deadline cuối ngày của v0.7.5.')
  assert(src.includes('PROJECT_REOPENED_FOR_NEW_MILESTONE'), 'Thiếu mở Project khi bổ sung mốc của v0.7.4.')
  const route='        api.MapPost("/projects/{projectId:guid}/milestones", CreateMilestone);'
  src=replaceOne(src,route,route+`\n        api.MapPost("/projects/{projectId:guid}/milestone-plan", CreateMilestonePlan);`, 'route tạo bộ mẫu')
  const anchor='    private static async Task<IResult> UpdateMilestone(Guid id, SaveMilestone? req,'
  assert(src.includes(anchor), 'Không tìm thấy method UpdateMilestone.')
  const batch=`    ${MARK_BE}
    // A single EF SaveChanges call ensures the requested plan is atomic.
    // Same access checks as single-milestone creation: Student proposes, Lecturer creates directly.
    public sealed record MilestonePlanInput(List<SaveMilestone>? Items);

    private static async Task<IResult> CreateMilestonePlan(Guid projectId, MilestonePlanInput? req,
        ClaimsPrincipal principal, StudentProjectsDbContext db, CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return Results.NotFound();
        var creator = Role(principal);
        if (creator is not ("Lecturer" or "Student") || !CanWrite(project, principal, creator))
            return Results.Forbid();
        if (project.Status == ProjectStatus.CANCELLED)
            return Conflict("Project đã hủy, không thể thêm Milestone.");
        if (req?.Items == null || req.Items.Count < 1 || req.Items.Count > 48)
            return Invalid("Mỗi lần áp dụng phải chọn 1 đến 48 Milestone.");

        // Validate every entry and detect duplicates BEFORE tracking or saving anything.
        var titles = await db.Milestones.AsNoTracking().Where(m => m.ProjectId == projectId)
            .Select(m => m.Title).ToListAsync(ct);
        var unique = new HashSet<string>(titles.Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
        foreach (var item in req.Items)
        {
            var error = Validate(item);
            if (error != null) return Invalid(error);
            var title = Text(item.Title);
            if (!unique.Add(title))
                return Conflict($"Milestone '{title}' đã tồn tại hoặc trùng trong bộ mẫu.");
        }

        var ids = new List<Guid>(req.Items.Count);
        foreach (var item in req.Items)
        {
            var record = new Milestone
            {
                ProjectId = projectId, Title = Text(item.Title), Description = Text(item.Description),
                StartAt = item.StartAt, DeadlineAt = EndOfDeadlineDay(item.DeadlineAt),
                ExpectedResult = Text(item.ExpectedResult),
                Status = MilestoneStatus.PENDING, PercentComplete = 0,
                ProposedByStudent = creator == "Student",
                ProposalStatus = creator == "Student" ? "PENDING_REVIEW" : "CONFIRMED"
            };
            db.Milestones.Add(record);
            ids.Add(record.Id);
            Audit(db, Actor(principal), creator == "Student" ? "MILESTONE_PROPOSED_FROM_TEMPLATE" : "MILESTONE_CREATED_FROM_TEMPLATE",
                "Milestone", record.Id);
        }
        if (project.Status == ProjectStatus.COMPLETED)
        {
            project.Status = ProjectStatus.IN_PROGRESS;
            project.UpdatedAt = DateTimeOffset.UtcNow;
            Audit(db, Actor(principal), "PROJECT_REOPENED_FOR_NEW_MILESTONE", "Project", project.Id);
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { CreatedCount = ids.Count, Ids = ids });
    }

`
  src=replaceOne(src,anchor,batch+anchor,'method batch')
  result.backend=restoreEol(src,original.backend)
}
const changes=Object.entries(result)
if (!changes.length) { console.log('v0.7.8 đã cài trước đó: không sửa trùng.');process.exit(0) }
const backupSuffix=new Date().toISOString().replace(/[:.]/g,'-')
const done=[]
try {
  for (const [key,code] of changes) {
    const filepath=targets[key]
    const backup=`${filepath}.before-v078-${backupSuffix}.bak`
    fs.copyFileSync(filepath,backup)
    done.push([filepath,backup])
    fs.writeFileSync(filepath,code,'utf8')
    console.log('Đã cập nhật:',path.relative(root,filepath))
  }
  console.log('v0.7.8 cài đặt thành công. Thư viện mẫu: 8 nhóm × 6 Milestone.')
} catch(e) {
  for (const [filepath,backup] of done.reverse()) fs.copyFileSync(backup,filepath)
  console.error('Đã hoàn nguyên các file sau lỗi:',e.message)
  throw e
}
