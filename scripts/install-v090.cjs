#!/usr/bin/env node
// V090_INSTALL_SECURITY_TESTS: security remediation + isolated regression suite.
// Never modifies a database, frontend, or existing business endpoints.
'use strict'
const fs = require('node:fs')
const path = require('node:path')
const crypto = require('node:crypto')
const root = path.resolve(__dirname,'..')
const relInfra = 'backend/StudentProjects.Infrastructure/StudentProjects.Infrastructure.csproj'
const relApi = 'backend/StudentProjects.Api/StudentProjects.Api.csproj'
const relTests = 'backend/StudentProjects.IntegrationTests/StudentProjects.IntegrationTests.csproj'
const testRel = 'backend/StudentProjects.IntegrationTests/V09SecurityAndProjectLifecycleTests.cs'
const moduleRel = 'backend/StudentProjects.Api/Endpoints/V08Endpoints.cs'
const helperRel = 'backend/StudentProjects.Api/Services/V08NotificationLinks.cs'
const backupBase = 'scripts/v09-backups'
const marker = 'V090_SECURITY_PKCS'
const fail = message => { throw new Error('v0.9: '+message) }
const target = rel => path.join(root,rel)
for (const rel of [relInfra,relApi,relTests,testRel,moduleRel,helperRel]) {
  if (!fs.existsSync(target(rel))) fail('Thiếu '+rel+'. Giải nén ZIP v0.9 vào thư mục gốc dự án v0.8.4.')
}
if (!fs.readFileSync(target(moduleRel),'utf8').includes('MapV08Endpoints') ||
    !fs.readFileSync(target(helperRel),'utf8').includes('V08NotificationLinks'))
  fail('Yêu cầu source v0.8.4 đã cài; dừng để tránh vá sai phiên bản.')
const expectedTest = fs.readFileSync(target(testRel),'utf8')
if (!expectedTest.includes('V090_REGRESSION_TESTS')) fail('Test file mới không đúng v0.9')
// Never replace someone else's existing test file.
// A re-run is only accepted if its existing content is the same test revision.
const orig = [relInfra,relApi,relTests].map(rel => ({rel,raw:fs.readFileSync(target(rel),'utf8')}))
if (orig.some(x => !x.raw.includes('<TargetFramework>net10.0</TargetFramework>')))
  fail('Không thấy TargetFramework net10.0. Chưa thay đổi file nào.')
if (fs.existsSync(target('Directory.Packages.props')) || fs.existsSync(target('backend/Directory.Packages.props')))
  fail('Có quản lý NuGet tập trung. Dừng để không gây xung đột PackageVersion.')
function patched(src,rel) {
  let s=src
  const pin = '<PackageReference Include="System.Security.Cryptography.Pkcs" Version="10.0.13" />'
  // Direct references, if present, must be upgraded (not silently overridden).
  const direct = /<PackageReference\s+Include=["']System\.Security\.Cryptography\.Pkcs["'][^>]*\/?\s*>/g
  const matches = [...s.matchAll(direct)]
  if (matches.length > 1) fail(rel+': tìm thấy nhiều PackageReference Pkcs, cần kiểm tra thủ công.')
  if (matches.length === 1) {
    if (!/\bVersion=["'][\d.]+["']/.test(matches[0][0])) fail(rel+': Pkcs dùng version không phải dạng số.')
    const version = matches[0][0].match(/\bVersion=["']([\d.]+)["']/)[1]
    // Don't downgrade already-patched or future versions.
    if (Number(version.split('.')[0]) > 10 ||
      (version.split('.')[0] === '10' && Number(version.split('.')[2]||0) > 13))
      return s
    s=s.replace(matches[0][0],pin)
    return s
  }
  if (rel !== relInfra) return s
  if (s.includes(marker)) return s
  if (!s.includes('</Project>')) fail(rel+': không thấy </Project>.')
  // Pin only in Infrastructure; it propagates transitively to API and tests.
  return s.replace('</Project>', `  <ItemGroup>\n    <!-- ${marker}: patched 2026-10 advisories; do not suppress NU1902/NU1903 -->\n    ${pin}\n  </ItemGroup>\n</Project>`)
}
const changes = orig.map(x=>({...x,next:patched(x.raw,x.rel)})).filter(x=>x.next!==x.raw)
if (changes.length === 0) {
 console.log('v0.9 đã áp dụng hoặc dependency đã được vá. Không thay đổi file.')
 console.log('Chạy: powershell -ExecutionPolicy Bypass -File .\\scripts\\test-v090.ps1')
 process.exit(0)
}
// Validate all file contents before touching anything.
for (const c of changes) {
 if (!c.next.includes('10.0.13')) fail(c.rel+': pin version không được áp dụng.')
 if ((c.next.match(/<Project\b/g)||[]).length!==1 || (c.next.match(/<\/Project>/g)||[]).length!==1)
   fail(c.rel+': project XML bất thường.')
}
const suffix = new Date().toISOString().replace(/[-:.TZ]/g,'').slice(0,14)
const backupDir = target(`${backupBase}/v090-${suffix}`)
fs.mkdirSync(backupDir,{recursive:true})
const manifest=[]
for (const c of changes) {
  const backup = path.join(backupDir, path.basename(c.rel)+'.before')
  fs.writeFileSync(backup,c.raw,'utf8')
  manifest.push({file:c.rel,backup:path.relative(root,backup).replace(/\\/g,'/'),sha256:crypto.createHash('sha256').update(c.raw).digest('hex')})
}
fs.writeFileSync(path.join(backupDir,'manifest.json'),JSON.stringify(manifest,null,2),'utf8')
// Write patched files only after backups and full validation.
for (const c of changes) {
 fs.writeFileSync(target(c.rel),c.next,'utf8')
 console.log('Đã cập nhật:',c.rel)
}
console.log('Đã sao lưu vào:',path.relative(root,backupDir))
console.log('Không thay đổi Database, nghiệp vụ hoặc các thông tin đăng nhập.')
console.log('Chạy: powershell -ExecutionPolicy Bypass -File .\\scripts\\test-v090.ps1')
