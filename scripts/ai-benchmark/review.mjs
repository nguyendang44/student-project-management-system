import fs from 'node:fs/promises';
import path from 'node:path';

const indexOf=code=>new Map(code.packets.flatMap(p=>p.lines.map(l=>[l.id,{...l,file:p.file}])));
const safe=value=>String(value??'').replace(/[\r\n|]/g,' ').replace(/</g,'&lt;').replace(/>/g,'&gt;');
export function flagFinding(item,code) {
  if(item.source!=='ollama') return [];
  const index=indexOf(code),rows=(item.evidenceIds??[]).map(id=>index.get(id)).filter(Boolean);
  const changed=rows.filter(r=>r.kind!=='context');
  const flags=[];
  const add=(rule,reason)=>flags.push({rule,reason,decision:'NEEDS_REVIEW'});
  const records=changed.flatMap(r=>Object.values(r.structure??{}));
  const text=`${item.title??''} ${item.description??''}`;
  if(changed.length && records.length && changed.every(r=>Object.keys(r.structure??{}).length) && records.every(r=>r.docstring) && item.category!=='documentation')
    add('DOCSTRING_CATEGORY','Bằng chứng thay đổi nằm trong docstring, nhưng phân loại AI không phải tài liệu.');
  if(records.some(r=>r.operations?.some(o=>['Import','ImportFrom'].includes(o.kind))) && item.category==='documentation')
    add('IMPORT_CATEGORY','AST nhận diện import trong bằng chứng; AI phân loại là tài liệu.');
  if(changed.length && changed.every(r=>r.file.startsWith('tests/')) && item.category!=='test')
    add('TEST_SCOPE','Các dòng thay đổi thuộc tests/; cần kiểm tra phân loại và phạm vi diễn giải.');
  if(/\bPOST\b/.test(text) && !rows.some(r=>/\bPOST\b/.test(r.text)))
    add('UNANCHORED_HTTP_METHOD','Nhận xét nêu POST nhưng các dòng được dẫn không có POST; chưa xác nhận phương thức từ ngữ cảnh khác.');
  const before=records.filter(r=>r.side==='before'),after=records.filter(r=>r.side==='after');
  if(/thêm\s+(?:mới\s+)?tham số/iu.test(text)) {
    const existing=new Set();
    for(const b of before) for(const a of after) if(b.function?.name && b.function.name===a.function?.name)
      for(const name of Object.keys(b.function.parameters)) if(Object.hasOwn(a.function.parameters,name) && text.includes(name)) existing.add(name);
    if(existing.size) add('EXISTING_PARAMETER',`Tham số được nhắc đã tồn tại ở cả hai phiên bản: ${[...existing].join(', ')}.`);
  }
  if(/(?:loại bỏ|xóa|bỏ) điều kiện/iu.test(text) && changed.some(r=>r.kind==='added' && /^\s*if\b/.test(r.text)))
    add('CONDITION_REMAINS','Code mới vẫn có câu if trong nhóm; cần phân biệt viết lại điều kiện với loại bỏ kiểm tra.');
  return flags;
}
export function buildReview(code,summary,checks) {
  const index=indexOf(code);
  const manual=()=>({explanation:'UNREVIEWED',evidenceRelevance:'UNREVIEWED',notes:''});
  return {
    version:'review-v1',baseSha:code.baseSha,headSha:code.headSha,
    note:'Cờ chỉ yêu cầu kiểm tra, không tự sửa nhãn hay kết luận AI sai. Không có cờ không có nghĩa nhận xét đúng.',
    summary:summary.map((item,i)=>({id:`A${i+1}`,source:item.source,title:item.title,description:item.description,
      evidenceIds:item.evidenceIds??[],autoFlags:flagFinding(item,code),
      manual:item.source==='ollama'?manual():null})),
    claims:checks.map(c=>({id:c.claimId,text:c.text,rawStatus:c.rawStatus??c.status,effectiveStatus:c.status,
      expected:c.expected,labelCheck:c.status==='ERROR'?'UNASSESSED':{rawMatches:c.rawStatus===c.expected,effectiveMatches:c.status===c.expected},
      atoms:(c.atoms??[]).map(a=>({id:a.claimId,text:a.text,explanation:a.explanation,evidenceIds:a.evidenceIds??[],
        comparisons:a.comparisons??[],typeCheck:a.typeCheck??null,autoFlags:a.consistencyFlags??flagConsistency({status:a.rawStatus??a.status,explanation:a.explanation}),
        idsExist:(a.evidenceIds??[]).every(id=>index.has(id)),manual:manual()}))})),
  };
}
export function reviewMarkdown(review) {
  const lines=['# Phiếu kiểm tra độc lập','','Nhãn đúng, diễn giải đúng và dẫn chứng đúng ý là ba tiêu chí riêng. ID tồn tại chỉ xác nhận vị trí code, không xác nhận ý nghĩa.', '',review.note,'',
    'Cờ từ quy tắc/AST, không phải nhận xét Ollama. Chưa chấm độ đúng diễn giải hoặc độ liên quan dẫn chứng.','',
    '| Mục | Cờ cần kiểm tra | Diễn giải | Dẫn chứng đúng ý |','|---|---|---|---|'];
  for(const item of review.summary.filter(x=>x.source==='ollama')) lines.push(`| ${item.id} | ${safe(item.autoFlags.map(f=>f.reason).join(' / ')||'Không có cờ tự động')} | Chưa duyệt | Chưa duyệt |`);
  lines.push('','## Đối chiếu báo cáo','','| Mục | Nhãn AI khớp đáp án | Sau quy tắc khớp đáp án | Diễn giải/dẫn chứng |','|---|---|---|---|');
  for(const c of review.claims) lines.push(`| ${c.id} | ${typeof c.labelCheck==='object'?c.labelCheck.rawMatches?'Có':'Không':'Chưa đánh giá'} | ${typeof c.labelCheck==='object'?c.labelCheck.effectiveMatches?'Có':'Không':'Chưa đánh giá'} | Chưa duyệt từng ý |`);
  for(const c of review.claims) for(const a of c.atoms) for(const flag of a.autoFlags) lines.push('',`${a.id}: ${safe(flag.reason)} Nhãn gốc được giữ nguyên.`);
  for(const c of review.claims)for(const a of c.atoms)for(const row of a.comparisons??[]) lines.push('',`${a.id} — ${safe(row.claimPart)}: ${safe((row.evidenceSources??[]).map(e=>e.id+'='+e.versions.join('+')).join(', '))}. Quan hệ AI: ${safe(row.relation)}; không xác nhận quan hệ này đúng.`);
  lines.push('','Có thể ghi đánh giá thủ công trong review-checklist.json: explanation và evidenceRelevance dùng CORRECT / PARTIAL / INCORRECT / UNREVIEWED, kèm notes. Phiếu không tự tính các trường chưa duyệt thành đúng. Báo cáo Markdown là bản tạo ban đầu, không tự đồng bộ các chỉnh sửa JSON.','');
  return lines.join('\n');
}
export async function writeReview(directory,review) {
  await fs.mkdir(directory); // Refuse to overwrite an earlier manual review.
  await fs.writeFile(path.join(directory,'review-checklist.json'),JSON.stringify(review,null,2),'utf8');
  await fs.writeFile(path.join(directory,'review-report.md'),reviewMarkdown(review),'utf8');
}

// A narrow textual warning, not a semantic judge and never a replacement label.
export function flagConsistency(answer) {
  const text=String(answer.explanation??'').normalize('NFC').toLowerCase();
  const conclusion=text.split(/(?:do đó|vì vậy|kết luận)[,:]?/u).at(-1).trim();
  const explicitRejection=/(?:báo cáo|khẳng định|ý chính)(?: này)? (?:là )?(?:không chính xác|không đúng|sai|trái với code|mâu thuẫn với code)/u.test(conclusion);
  const explicitSupport=/(?:báo cáo|khẳng định)(?: này)? (?:là )?(?:chính xác|đúng|được hỗ trợ bởi code)/u.test(conclusion);
  const issue=answer.status==='SUPPORTED' && explicitRejection ? 'Nhãn SUPPORTED nhưng phần kết luận bằng lời phủ định báo cáo.'
    : answer.status==='CONTRADICTED' && explicitSupport && !explicitRejection ? 'Nhãn CONTRADICTED nhưng phần kết luận bằng lời xác nhận báo cáo.' : null;
  return issue?[{rule:'LABEL_EXPLANATION_CONFLICT',reason:issue,decision:'NEEDS_REVIEW'}]:[];
}
