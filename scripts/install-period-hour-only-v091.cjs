'use strict'
const fs = require('node:fs')
const path = require('node:path')

// Run from the Student Project Management System root; this script can be re-run safely.
const root = process.cwd()
const relative = 'frontend/src/views/TopicsManagementView.vue'
const target = path.join(root, relative)
const marker = 'V091_PERIOD_HOUR_ONLY'
if (!fs.existsSync(target)) {
  console.error(`Không tìm thấy ${relative}. Hãy chạy từ thư mục gốc dự án.`)
  process.exit(1)
}
const original = fs.readFileSync(target, 'utf8')
if (original.includes(marker)) {
  console.log('Bản vá giờ nguyên cho Đợt đăng ký đã được cài. Không cần chạy lại.')
  process.exit(0)
}
let updated = original
function swap(label, oldText, newText) {
  const count = updated.split(oldText).length - 1
  if (count !== 1) throw new Error(`${label}: cần tìm đúng 1 vị trí, thực tế ${count}. Source khác bản tham chiếu; không tự sửa để tránh ghi đè.`)
  updated = updated.replace(oldText, newText)
}

swap('periodForm',
  "const periodForm = reactive({ name:'', startsAt:'', endsAt:'', isOpen:true })",
  "// V091_PERIOD_HOUR_ONLY: ngày tách riêng, giờ 24h từ 00 đến 23, không nhập phút.\nconst periodHours = Array.from({ length: 24 }, (_, h) => String(h).padStart(2, '0'))\nconst periodForm = reactive({ name:'', startsDate:'', startsHour:'', endsDate:'', endsHour:'', isOpen:true })")

swap('createPeriod',
  `async function createPeriod(){busy.value=true;clearAlert();try{
  if(!periodForm.startsAt||!periodForm.endsAt){error.value='Chọn thời gian bắt đầu và kết thúc.';return}
  await periodsApi.create({name:periodForm.name,startsAt:new Date(periodForm.startsAt).toISOString(),endsAt:new Date(periodForm.endsAt).toISOString(),isOpen:periodForm.isOpen})
  Object.assign(periodForm,{name:'',startsAt:'',endsAt:'',isOpen:true});success.value='Đã tạo đợt đăng ký.';await reload()
}catch(e){error.value=errMessage(e)}finally{busy.value=false}}`,
  `async function createPeriod(){busy.value=true;clearAlert();try{
  if(!periodForm.startsDate||!periodForm.startsHour||!periodForm.endsDate||!periodForm.endsHour){error.value='Chọn đủ ngày và giờ bắt đầu, kết thúc.';return}
  const start = new Date(\`\${periodForm.startsDate}T\${periodForm.startsHour}:00:00\`)
  const end = new Date(\`\${periodForm.endsDate}T\${periodForm.endsHour}:00:00\`)
  if(!Number.isFinite(start.getTime())||!Number.isFinite(end.getTime())){error.value='Ngày giờ không hợp lệ.';return}
  if(end.getTime()<=start.getTime()){error.value='Thời điểm kết thúc phải sau thời điểm bắt đầu.';return}
  await periodsApi.create({name:periodForm.name,startsAt:start.toISOString(),endsAt:end.toISOString(),isOpen:periodForm.isOpen})
  Object.assign(periodForm,{name:'',startsDate:'',startsHour:'',endsDate:'',endsHour:'',isOpen:true});success.value='Đã tạo đợt đăng ký.';await reload()
}catch(e){error.value=errMessage(e)}finally{busy.value=false}}`)

swap('formatDate',
  "function formatDate(iso:string){return new Date(iso).toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false, hourCycle: 'h23' })}",
  `function formatDate(iso:string){
  const value = new Date(iso)
  if(!Number.isFinite(value.getTime())) return '—'
  const hh = String(value.getHours()).padStart(2,'0')
  const mm = String(value.getMinutes()).padStart(2,'0')
  const date = new Intl.DateTimeFormat('vi-VN',{day:'2-digit',month:'2-digit',year:'numeric'}).format(value)
  // Với đợt cũ có phút lẻ, giữ nguyên phút để không làm sai thông tin.
  return mm==='00' ? \`\${hh} giờ \${date}\` : \`\${hh}:\${mm} \${date}\`
}`)

swap('period form template',
  '<form class="user-form" @submit.prevent="createPeriod"><label>Tên đợt<input v-model.trim="periodForm.name" maxlength="200" required /></label><label>Bắt đầu<input v-model="periodForm.startsAt" type="datetime-local" required lang="en-GB" /></label><label>Kết thúc<input v-model="periodForm.endsAt" type="datetime-local" required lang="en-GB" /></label><label>Trạng thái<select v-model="periodForm.isOpen"><option :value="true">Mở</option><option :value="false">Đóng</option></select></label><button class="action-primary" :disabled="busy" type="submit">Tạo đợt</button></form>',
  `<form class="user-form" @submit.prevent="createPeriod">
      <label>Tên đợt<input v-model.trim="periodForm.name" maxlength="200" required /></label>
      <label>Ngày bắt đầu<input v-model="periodForm.startsDate" type="date" required /></label>
      <label>Giờ bắt đầu (24h)<select v-model="periodForm.startsHour" required><option disabled value="">Chọn giờ</option><option v-for="h in periodHours" :key="'start-'+h" :value="h">{{h}} giờ</option></select></label>
      <label>Ngày kết thúc<input v-model="periodForm.endsDate" type="date" required /></label>
      <label>Giờ kết thúc (24h)<select v-model="periodForm.endsHour" required><option disabled value="">Chọn giờ</option><option v-for="h in periodHours" :key="'end-'+h" :value="h">{{h}} giờ</option></select></label>
      <label>Trạng thái<select v-model="periodForm.isOpen"><option :value="true">Mở</option><option :value="false">Đóng</option></select></label>
      <button class="action-primary" :disabled="busy" type="submit">Tạo đợt</button>
    </form>`)

// Do not overwrite if the source differs: all swaps must succeed before writing.
const backup = `${target}.before-v091-${new Date().toISOString().replace(/[:.]/g,'-')}.bak`
fs.copyFileSync(target,backup)
fs.writeFileSync(target,updated,'utf8')
console.log(`Cập nhật: ${relative}`)
console.log(`Sao lưu: ${path.relative(root,backup)}`)
console.log('Đợt đăng ký: chọn ngày + giờ 00–23, phút/giây = 00, mặc định Mở; danh sách giữ phút lẻ của dữ liệu cũ.')
