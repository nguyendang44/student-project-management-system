import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {validateClaim,evidenceIndex,makeReport,validateResume} from './run.mjs';
import {splitClaimLegacy as splitClaim,aggregateStatuses,requiresExecutionEvidence} from './analysis-v2.mjs';

const here=path.dirname(fileURLToPath(import.meta.url));
const read=async p=>JSON.parse(await fs.readFile(p,'utf8'));

export function replayClaim(saved,input,atom,canonical) {
  if(input.claim?.id!==atom.id || input.claim?.text!==atom.text || !Array.isArray(input.packets))
    throw new Error(`Đầu vào lưu của ${atom.id} không khớp báo cáo`);
  // Validate the actual supplied subset, not all code in the repository.
  const sent=evidenceIndex({packets:input.packets});
  for(const [id,row] of sent) {
    const expected=canonical.get(id);
    if(!expected || ['file','text','kind','beforeLine','afterLine'].some(k=>row[k]!==expected[k]))
      throw new Error(`Dữ liệu code lưu của ${atom.id} không khớp ${id}`);
  }
  const candidates=saved.result?.responseError
    ? (saved.result.attempts ?? []).map(a=>a.answer).filter(Boolean)
    : [saved.result];
  for(const [i,answer] of candidates.entries()) {
    try {
      const validated=validateClaim(answer,atom,input.packets);
      if(validated.citationValid) return {...validated,replayed:true,selectedAttempt:i+1};
    } catch { /* Try other recorded answers; never choose using a gold label. */ }
  }
  return {claimId:atom.id,claimQuote:atom.text,status:'ERROR',explanation:'Không có phản hồi đã lưu nào hợp lệ để kiểm tra lại.',
    evidenceIds:[],citationValid:false,responseError:true,rejectionReason:'Không khôi phục được phản hồi hợp lệ'};
}

export async function main(argv=process.argv.slice(2)) {
  const opts={from:'prompt-v3',run:'prompt-v3-recheck',case:'flask-3.1.2'};
  for(let i=0;i<argv.length;i+=2) {
    const k=argv[i].replace(/^--/,'');
    if(!(k in opts)||!argv[i+1]) throw new Error('Tham số: --from, --run, --case');
    opts[k]=argv[i+1];
  }
  if(!Object.values(opts).every(x=>/^[a-zA-Z0-9_.-]+$/.test(x)&&!x.includes('..'))||opts.from===opts.run)
    throw new Error('Tên thư mục không hợp lệ hoặc trùng nguồn');
  const root=path.join(here,'results',opts.case),source=path.join(root,opts.from),output=path.join(root,opts.run);
  const code=await read(path.join(here,'data',`${opts.case}.code.json`));
  const expected=await read(path.join(here,'data',`${opts.case}.expected.json`));
  const summary=await read(path.join(source,'summary.json'));
  validateResume(summary,code,summary.model);
  const canonical=evidenceIndex(code),checks=[];
  for(const item of expected.claims) {
    const atoms=[];
    // Gold labels and explanations are not passed into replayClaim.
    for(const atom of splitClaim({id:item.id,text:item.text,terms:item.terms})) {
      const saved=await read(path.join(source,`claim-${atom.id}.json`));
      const input=await read(path.join(source,`input-${atom.id}.json`));
      const result=replayClaim(saved,input,atom,canonical);
      const policy=!result.responseError&&requiresExecutionEvidence(atom.text);
      atoms.push({...result,text:atom.text,rawStatus:result.status,
        effectiveStatus:result.responseError?'ERROR':policy?'INSUFFICIENT':result.status,
        policyReason:policy?'Đầu vào chỉ có code, không có kết quả thực thi.':null});
    }
    const hasError=atoms.some(a=>a.responseError);
    checks.push({claimId:item.id,text:item.text,atoms,
      rawStatus:hasError?'ERROR':aggregateStatuses(atoms.map(a=>a.rawStatus)),
      status:hasError?'ERROR':aggregateStatuses(atoms.map(a=>a.effectiveStatus)),
      explanation:atoms.map(a=>`${a.claimId}: ${a.explanation}`).join(' '),
      evidenceIds:[...new Set(atoms.flatMap(a=>a.evidenceIds))],citationValid:atoms.every(a=>a.citationValid),
      rejectionReason:hasError?'Phản hồi lưu chưa hợp lệ':null,expected:item.expected,reason:item.reason});
  }
  // Refuse to overwrite any earlier report, even on a replay.
  await fs.mkdir(output);
  const provenance={mode:'offline-recheck',sourceRun:opts.from,note:'Không gọi model; chỉ kiểm tra lại phản hồi cũ. Không chứng minh model đã cải thiện.'};
  await fs.writeFile(path.join(output,'assessment.json'),JSON.stringify({...provenance,checks},null,2),'utf8');
  const report='> KIỂM TRA LẠI NGOẠI TUYẾN: dùng phản hồi đã lưu, không gọi Ollama. Nhãn AI không được sửa; chỉ chuẩn hóa trích dẫn khớp chính xác. Phần tổng hợp giữ nguyên, kể cả lỗi.\n\n'+
    makeReport(code,expected,summary.items,checks,summary.model);
  await fs.writeFile(path.join(output,'report.md'),report,'utf8');
  console.log(`Đã kiểm tra lại: ${path.join(output,'report.md')}`);
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url))
  main().catch(e=>{console.error(e.code==='EEXIST'?'Thư mục đích đã có; dùng --run tên-mới.':e.message);process.exitCode=1;});
