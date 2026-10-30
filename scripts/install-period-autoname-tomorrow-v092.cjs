'use strict'
const fs = require('node:fs')
const path = require('node:path')

// v0.9.2: Update registration-period creation UI without replacing unrelated source.
const root = process.cwd()
const relative = 'frontend/src/views/TopicsManagementView.vue'
const target = path.join(root, relative)
const marker = 'V092_PERIOD_AUTO_NUMBER_TOMORROW_07'
if (!fs.existsSync(target)) {
  console.error(`Không tìm thấy ${relative}. Hãy chạy ở thư mục gốc dự án.`)
  process.exit(1)
}
const original = fs.readFileSync(target, 'utf8')
if (original.includes(marker)) {
  console.log('v0.9.2 đã được cài đặt; không có thay đổi mới.')
  process.exit(0)
}
if (!original.includes('V091_PERIOD_HOUR_ONLY')) {
  console.error('Cần cài bản v0.9.1 trước v0.9.2. Không thay đổi source.')
  process.exit(1)
}
let updated = original
function swap(label, before, after) {
  const matches = updated.split(before).length - 1
  if (matches !== 1) throw new Error(`${label}: dự kiến 1 vị trí, tìm được ${matches}. Đã dừng để bảo vệ mã hiện tại.`)
  updated = updated.replace(before, after)
}

swap('period default and sequential name',
  "const periodForm = reactive({ name:'', startsDate:'', startsHour:'', endsDate:'', endsHour:'', isOpen:true })",
  `// V092_PERIOD_AUTO_NUMBER_TOMORROW_07: Tên đợt tự tăng; ngày bắt đầu mặc định ngày mai lúc 07:00.
function periodTomorrowLocal(){
  const now = new Date()
  now.setDate(now.getDate() + 1)
  return [now.getFullYear(), String(now.getMonth()+1).padStart(2,'0'), String(now.getDate()).padStart(2,'0')].join('-')
}
const nextPeriodName = computed(() => {
  const ids = periods.value.map(p => p.name.trim()).filter(name => /^[0-9]+$/.test(name)).map(Number)
  return String(Math.max(0, ...ids) + 1)
})
const periodForm = reactive({ startsDate:periodTomorrowLocal(), startsHour:'07', endsDate:'', endsHour:'', isOpen:true })`)

swap('create period payload and reset',
  `async function createPeriod(){busy.value=true;clearAlert();try{
  if(!periodForm.startsDate||!periodForm.startsHour||!periodForm.endsDate||!periodForm.endsHour){error.value='Chọn đủ ngày và giờ bắt đầu, kết thúc.';return}
  const start = new Date(\`\${periodForm.startsDate}T\${periodForm.startsHour}:00:00\`)
  const end = new Date(\`\${periodForm.endsDate}T\${periodForm.endsHour}:00:00\`)
  if(!Number.isFinite(start.getTime())||!Number.isFinite(end.getTime())){error.value='Ngày giờ không hợp lệ.';return}
  if(end.getTime()<=start.getTime()){error.value='Thời điểm kết thúc phải sau thời điểm bắt đầu.';return}
  await periodsApi.create({name:periodForm.name,startsAt:start.toISOString(),endsAt:end.toISOString(),isOpen:periodForm.isOpen})
  Object.assign(periodForm,{name:'',startsDate:'',startsHour:'',endsDate:'',endsHour:'',isOpen:true});success.value='Đã tạo đợt đăng ký.';await reload()
}catch(e){error.value=errMessage(e)}finally{busy.value=false}}`,
  `async function createPeriod(){busy.value=true;clearAlert();try{
  const startsDate = periodForm.startsDate || periodTomorrowLocal()
  const startsHour = periodForm.startsHour || '07'
  if(!periodForm.endsDate||!periodForm.endsHour){error.value='Chọn ngày và giờ kết thúc.';return}
  const start = new Date(\`\${startsDate}T\${startsHour}:00:00\`)
  const end = new Date(\`\${periodForm.endsDate}T\${periodForm.endsHour}:00:00\`)
  if(!Number.isFinite(start.getTime())||!Number.isFinite(end.getTime())){error.value='Ngày giờ không hợp lệ.';return}
  if(end.getTime()<=start.getTime()){error.value='Thời điểm kết thúc phải sau thời điểm bắt đầu.';return}
  // Lấy danh sách mới nhất để tránh đặt tên trùng do dữ liệu được tạo từ phiên khác.
  periods.value=await periodsApi.list()
  const name = nextPeriodName.value
  await periodsApi.create({name,startsAt:start.toISOString(),endsAt:end.toISOString(),isOpen:periodForm.isOpen})
  Object.assign(periodForm,{startsDate:periodTomorrowLocal(),startsHour:'07',endsDate:'',endsHour:'',isOpen:true})
  await reload();success.value=\`Đã tạo đợt đăng ký số \${name}.\`
}catch(e){error.value=errMessage(e)}finally{busy.value=false}}`)

swap('period creation UI',
  `      <label>Tên đợt<input v-model.trim="periodForm.name" maxlength="200" required /></label>
      <label>Ngày bắt đầu<input v-model="periodForm.startsDate" type="date" required /></label>`,
  `      <label>Tên đợt (tự động)<input :value="nextPeriodName" type="text" readonly aria-label="Tên đợt tự động" /></label>
      <label>Ngày bắt đầu<input v-model="periodForm.startsDate" type="date" /></label>`)

const backup = `${target}.before-v092-${new Date().toISOString().replace(/[:.]/g,'-')}.bak`
fs.copyFileSync(target, backup)
fs.writeFileSync(target, updated, 'utf8')
console.log(`Cập nhật ${relative}`)
console.log(`Sao lưu: ${path.relative(root, backup)}`)
console.log('v0.9.2: Tên đợt tự tăng 1,2,3...; ngày bắt đầu mặc định ngày mai lúc 07:00; giờ 24h, mặc định Mở.')
