#!/usr/bin/env node
// Minimal additive EF patch: never replace CoreEntities.cs/DbContext.cs with older versions.
'use strict'
const fs=require('fs')
const path=require('path')
const root=path.resolve(__dirname,'..')
const entity=path.join(root,'backend/StudentProjects.Domain/Entities/CoreEntities.cs')
const context=path.join(root,'backend/StudentProjects.Infrastructure/Persistence/StudentProjectsDbContext.cs')
const api=path.join(root,'backend/StudentProjects.Api/Endpoints/ProjectProgressV07Endpoints.cs')
const view=path.join(root,'frontend/src/views/ProjectProgressV07View.vue')
function stop(s){throw new Error('Không cài v0.7.1: '+s)}
for(const p of [entity,context,api,view]) if(!fs.existsSync(p)) stop('Thiếu '+path.relative(root,p))
const originalE=fs.readFileSync(entity,'utf8')
const originalC=fs.readFileSync(context,'utf8')
if(!fs.readFileSync(api,'utf8').includes('ReviewProposal(')) stop('File API v0.7.1 chưa có. Giải nén đúng ZIP trước khi chạy.')
if(!originalE.includes('public sealed class Milestone : Entity') || !originalC.includes('modelBuilder.Entity<Milestone>(e =>'))
  stop('Source EF không tương thích với bản v0.7.')
const nl=originalE.includes('\r\n')?'\r\n':'\n'
const cNl=originalC.includes('\r\n')?'\r\n':'\n'
const eStatus=/^([ \t]*)public MilestoneStatus Status \{ get; set; \} = MilestoneStatus\.PENDING;\r?\n/m
let updatedE=originalE, updatedC=originalC
if(!updatedE.includes('public bool ProposedByStudent { get; set; }')) {
  const milestonePart=updatedE.split('public sealed class Milestone : Entity')[1]?.split('public sealed class ProgressUpdate : Entity')[0]
  if(!milestonePart || !eStatus.test(milestonePart)) stop('Không tìm được vị trí thêm Milestone.ProposedByStudent.')
  const part=milestonePart.replace(eStatus,(_,indent)=>`${indent}public MilestoneStatus Status { get; set; } = MilestoneStatus.PENDING;${nl}${indent}public bool ProposedByStudent { get; set; }${nl}${indent}public string ProposalStatus { get; set; } = "CONFIRMED";${nl}${indent}public string? ProposalFeedback { get; set; }${nl}`)
  updatedE=updatedE.replace(milestonePart,part)
}
if(!updatedC.includes('e.Property(x => x.ProposalStatus).HasMaxLength(32).IsRequired();')) {
  const start=updatedC.indexOf('modelBuilder.Entity<Milestone>(e =>')
  const end=updatedC.indexOf('modelBuilder.Entity<ProgressUpdate>(e =>',start)
  if(start<0||end<0) stop('Không tìm được mapping Milestone trong DbContext.')
  const part=updatedC.slice(start,end)
  if(!part.includes('e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();')) stop('Mapping trạng thái Milestone đã khác bản nền, cần kiểm tra.')
  const next=part.replace('e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();',
    'e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();'+cNl+
    '            e.Property(x => x.ProposalStatus).HasMaxLength(32).IsRequired();'+cNl+
    '            e.Property(x => x.ProposalFeedback).HasMaxLength(2000);')
  updatedC=updatedC.slice(0,start)+next+updatedC.slice(end)
}
const changed=[]
if(updatedE!==originalE) changed.push({file:entity,data:updatedE,original:originalE})
if(updatedC!==originalC) changed.push({file:context,data:updatedC,original:originalC})
if(!changed.length){console.log('v0.7.1 EF đã cài trước đó. Không sửa trùng.'); process.exit(0)}
const suffix=new Date().toISOString().replace(/[:.]/g,'-')
const backups=[]
try {
  for(const x of changed){const dst=x.file+'.before-v071-'+suffix+'.bak';fs.copyFileSync(x.file,dst);backups.push({file:x.file,backup:dst})}
  for(const x of changed){fs.writeFileSync(x.file,x.data,'utf8');console.log('Đã cập nhật:',path.relative(root,x.file))}
  console.log('Đã sao lưu:',backups.map(x=>path.relative(root,x.backup)).join(', '))
  console.log('Tiếp theo: chạy SQL script v0.7.1 TRƯỚC KHI chạy lại Backend.')
} catch(e){for(const x of backups)try{fs.copyFileSync(x.backup,x.file)}catch{};throw e}
