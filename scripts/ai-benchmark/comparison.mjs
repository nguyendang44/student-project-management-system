const flag=(rule,reason)=>({rule,reason,decision:'NEEDS_REVIEW'});
const normalize=s=>typeof s==='string'?s.trim().replace(/\s+/gu,' '):'';
// A conservative language cue: it only decides whether to WARN about provenance.
export function claimTimeScope(text) {
  const value=normalize(text);
  if(/(?:đổi|thay đổi|chuyển).{0,70}từ.{0,50}(?:sang|thành)/iu.test(value))return 'TRANSITION';
  if(/(?:trước đây|phiên bản cũ|code cũ|trạng thái cũ)/iu.test(value) && !/(?:mới|hiện tại)/iu.test(value))return 'BEFORE';
  if(/(?:trạng thái mới|phiên bản mới|code mới|hiện tại|danh sách.{0,30}mới)/iu.test(value))return 'AFTER';
  return 'UNSPECIFIED';
}
export function evidenceSources(ids,packets) {
  const index=new Map(packets.flatMap(p=>p.lines.map(l=>[l.id,{...l,file:p.file}])));
  return (Array.isArray(ids)?ids:[]).map(id=>{
    const row=index.get(id);
    return {id,file:row?.file??null,kind:row?.kind??'unknown',versions:row?.kind==='removed'?['BEFORE']:row?.kind==='added'?['AFTER']:row?.kind==='context'?['BEFORE','AFTER']:[],beforeLine:row?.beforeLine??null,afterLine:row?.afterLine??null};
  });
}
export function inspectComparisons(answer,claim,packets) {
  const allowed=new Set(packets.flatMap(p=>p.lines.map(l=>l.id)));
  const rows=Array.isArray(answer.comparisons)?answer.comparisons:[];
  const flags=[];
  if(!rows.length) flags.push(flag('COMPARISON_MISSING','Chưa có bảng đối chiếu từng phần; nhãn gốc vẫn được giữ.'));
  const scope=claimTimeScope(claim.text);
  const inspectVersions=(ids,relation,label)=>{
    const sources=evidenceSources(ids,packets);
    if(scope==='AFTER' && relation==='MATCH' && sources.some(s=>s.kind==='removed'))
      flags.push(flag('REMOVED_EVIDENCE_FOR_AFTER',`${label}: khẳng định về trạng thái mới dẫn dòng REMOVED (${sources.filter(s=>s.kind==='removed').map(s=>s.id).join(', ')}). Dòng này chỉ tồn tại ở BEFORE; cần kiểm tra có đang dùng để chứng minh AFTER hay chỉ so sánh lịch sử. Không tự đổi nhãn.`));
    return sources;
  };
  const inspected=rows.map(value=>{
    const row=value && typeof value==='object' && !Array.isArray(value)?value:{};
    const issues=[];
    if(!normalize(row.claimPart)||!normalize(claim.text).includes(normalize(row.claimPart)))issues.push('claimPart không phải trích đoạn nguyên văn của ý báo cáo; diễn đạt đồng nghĩa vẫn cần duyệt trích dẫn');
    if(typeof row.codeReading!=='string'||!row.codeReading.trim())issues.push('thiếu codeReading');
    if(!['MATCH','CONFLICT','UNKNOWN'].includes(row.relation))issues.push('relation không hợp lệ');
    if(!Array.isArray(row.codeIds)||row.codeIds.some(id=>!allowed.has(id))||new Set(row.codeIds).size!==row.codeIds.length)issues.push('codeIds thiếu, trùng hoặc nằm ngoài đầu vào');
    else if(row.relation!=='UNKNOWN'&&!row.codeIds.length)issues.push('MATCH/CONFLICT cần ID bằng chứng');
    const valid=!issues.length;
    if(!valid)flags.push(flag('COMPARISON_INVALID',issues.join('; ')+'. Nhãn gốc vẫn được giữ; không dùng dòng này để kiểm tra quan hệ.'));
    const sources=inspectVersions(row.codeIds,row.relation,'Dòng đối chiếu');
    return {...row,structureValid:valid,validationIssues:issues,evidenceSources:sources,claimTimeScope:scope};
  });
  inspectVersions(answer.evidenceIds,answer.status==='SUPPORTED'?'MATCH':'UNKNOWN','Dẫn chứng nhãn');
  const valid=inspected.filter(r=>r.structureValid);
  if(answer.status==='SUPPORTED' && valid.some(r=>r.relation!=='MATCH'))
    flags.push(flag('COMPARISON_LABEL_CONFLICT','Nhãn SUPPORTED nhưng bảng có phần CONFLICT hoặc UNKNOWN.'));
  if(answer.status==='CONTRADICTED' && valid.length && valid.every(r=>r.relation==='MATCH'))
    flags.push(flag('COMPARISON_LABEL_CONFLICT','Nhãn CONTRADICTED nhưng tất cả dòng đối chiếu hợp lệ đều MATCH.'));
  return {comparisons:inspected,flags};
}
export function validateAuditInput(input,atom,code) {
  if(input.claim?.id!==atom.claimId||normalize(input.claim?.text)!==normalize(atom.text)||!Array.isArray(input.packets))throw new Error('Đầu vào lưu không khớp ý đối chiếu');
  const canonical=new Map(code.packets.flatMap(p=>p.lines.map(l=>[l.id,{...l,file:p.file}])));
  const seen=new Set();
  for(const p of input.packets)for(const row of p.lines) {
    const original=canonical.get(row.id);
    if(seen.has(row.id)||!original||original.file!==p.file||['kind','text','beforeLine','afterLine'].some(k=>row[k]!==original[k]))throw new Error('Đầu vào lưu có dòng không khớp nguồn: '+row.id);
    seen.add(row.id);
  }
  return input.packets.map(p=>({...p,lines:p.lines.map(row=>({...canonical.get(row.id)}))}));
}
// Conservative supported wording only. This is a source-based diagnostic, never a label override.
export function returnTypeCheck(claim,packets) {
  const match=/type hint trả về\s+(?:từ\s+([\w.]+)\s+)?(?:thành|sang)\s+([\w.]+)[.!]?\s*$/iu.exec(claim.text);
  if(!match)return null;
  const expected=match[2].replace(/\.$/u,'');
  const names=new Map();
  for(const p of packets)for(const l of p.lines) {
    const r=l.structure?.after, fn=r?.function;
    if(!fn?.returnAnnotation || !claim.text.includes(fn.name) && !claim.text.includes(fn.name.split('.').at(-1)))continue;
    const key=p.file+':'+fn.name;
    if(!names.has(key))names.set(key,{file:p.file,function:fn.name,actual:fn.returnAnnotation,ids:[]});
    if(l.text.includes('->') && !names.get(key).ids.includes(l.id))names.get(key).ids.push(l.id);
  }
  if(names.size!==1)return null;
  const result=[...names.values()][0];
  if(!result.ids.length)return null;
  return {...result,expected,source:'AST_RULE',relation:expected===result.actual?'MATCH':'CONFLICT',
    limitation:'Chỉ so tên annotation đích theo cú pháp; không suy runtime, alias hay tương đương kiểu. Không chấm toàn bộ câu báo cáo.'};
}

// Necessary evidence checks only: finding a matching node kind does not prove the claim.
export function inspectEvidenceRoles(answer,claim,packets) {
  const flags=[];
  const scope=claimTimeScope(claim.text);
  const rows=packets.flatMap(p=>p.lines.map(l=>({...l,file:p.file})));
  const sides=scope==='BEFORE'?['before']:scope==='TRANSITION'?['before','after']:['after'];
  const operations=(ids)=>rows.filter(r=>ids.includes(r.id)).flatMap(r=>sides.flatMap(side=>{
    const meta=r.structure?.[side];
    return (meta?.docstring?[]:meta?.operations??[]).map(op=>({...op,id:r.id,side,file:r.file,owner:meta.function?.name??null}));
  }));
  const checks=(answer.comparisons??[]).filter(r=>r?.relation==='MATCH').map(r=>({text:r.claimPart,ids:Array.isArray(r.codeIds)?r.codeIds:[]}));
  if(answer.status==='SUPPORTED')checks.push({text:claim.text,ids:answer.evidenceIds??[]});
  const available=operations(rows.map(r=>r.id));
  const metadataAvailable=rows.some(r=>sides.some(side=>r.structure?.[side]));
  for(const c of checks) {
    if(typeof c.text!=='string')continue;
    const cited=operations(c.ids);
    if(/khởi tạo/iu.test(c.text)) {
      if(!metadataAvailable)flags.push(flag('EVIDENCE_ROLE_UNCHECKED','Thiếu AST để kiểm tra bằng chứng khởi tạo.'));
      else if(!cited.some(o=>['Assign','AnnAssign'].includes(o.kind))) {
        const candidates=[...new Set(available.filter(o=>['Assign','AnnAssign'].includes(o.kind)).map(o=>o.id))];
        flags.push(flag('INITIALIZATION_EVIDENCE_MISSING',`Khẳng định khởi tạo nhưng các ID được dẫn không chứa AST phép gán ở phiên bản cần xét. Các ID phép gán có trong đầu vào: ${candidates.join(', ')||'không có'}. Đây là ứng viên cần đọc, không tự xác nhận biến/giá trị.`));
      }
    }
    if(/(?:trước.{0,100}sau|rồi.{0,30}(?:mới|sau)|sau đó)/iu.test(c.text)) {
      const calls=new Map(cited.filter(o=>o.kind==='Call').map(o=>[JSON.stringify([o.file,o.owner,o.side,o.startLine,o.endLine,o.code]),o]));
      const groups=new Map();
      for(const o of calls.values()) {
        const key=JSON.stringify([o.file,o.owner,o.side]);
        if(!groups.has(key))groups.set(key,new Set());
        groups.get(key).add(o.startLine);
      }
      if(!metadataAvailable)flags.push(flag('EVIDENCE_ROLE_UNCHECKED','Thiếu AST để kiểm tra bằng chứng thứ tự.'));
      else if(![...groups.values()].some(lines=>lines.size>=2)) flags.push(flag('ORDER_EVIDENCE_INCOMPLETE','Khẳng định thứ tự nhưng chưa dẫn đủ hai vị trí lời gọi khác nhau trong cùng hàm/phiên bản. Quy tắc này chỉ kiểm tra lời gọi; phép gán, nhánh hoặc thao tác cùng dòng cần người đọc duyệt. Không suy thứ tự chạy từ số dòng.'));
    }
  }
  return [...new Map(flags.map(f=>[f.rule+'|'+f.reason,f])).values()];
}
