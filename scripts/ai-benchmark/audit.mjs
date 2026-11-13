import {inspectComparisons,validateAuditInput,inspectEvidenceRoles} from './comparison.mjs';
import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {loadStructure} from './structure.mjs';
import {buildReview,writeReview} from './review.mjs';
import {validateResume} from './run.mjs';
const here=path.dirname(fileURLToPath(import.meta.url));
export async function main(argv=process.argv.slice(2)) {
 const opts={run:'regression-v38',case:'flask-3.1.1-holdout',output:'review-v39'};
 for(let i=0;i<argv.length;i+=2) {
  const key=argv[i]?.replace(/^--/,'');
  if(!(key in opts)||!argv[i+1])throw new Error('Tham số: --run, --case, --output');
  opts[key]=argv[i+1];
 }
 if(!Object.values(opts).every(x=>/^[a-zA-Z0-9][a-zA-Z0-9_.-]*$/.test(x)&&!x.includes('..')))throw new Error('Tên không hợp lệ');
 const read=async f=>JSON.parse(await fs.readFile(f,'utf8'));
 const code=await loadStructure(await read(path.join(here,'data',`${opts.case}.code.json`)),here);
 const dir=path.join(here,'results',opts.case,opts.run);
 const summary=await read(path.join(dir,'summary.json'));
 validateResume(summary,code,summary.model);
 const assessment=await read(path.join(dir,'assessment.json'));
 if(assessment.signature!==summary.signature)throw new Error('Kết quả đối chiếu và tổng hợp không cùng lần chạy');
 const checks=structuredClone(assessment.checks);
 for(const check of checks)for(const atom of check.atoms??[]) {
  if(!/^[A-Za-z0-9_-]+(?:\.[0-9]+)*$/.test(atom.claimId))throw new Error('ID ý không hợp lệ');
  const input=await read(path.join(dir,`input-${atom.claimId}.json`));
  const packets=validateAuditInput(input,atom,code);
  const result=inspectComparisons({...atom,status:atom.rawStatus??atom.status},{text:atom.text},packets);
  atom.comparisons=result.comparisons;
  const preserved=(atom.consistencyFlags??[]).filter(f=>!['COMPARISON_MISSING','COMPARISON_INVALID','COMPARISON_LABEL_CONFLICT','REMOVED_EVIDENCE_FOR_AFTER','INITIALIZATION_EVIDENCE_MISSING','ORDER_EVIDENCE_INCOMPLETE','EVIDENCE_ROLE_UNCHECKED'].includes(f.rule));
  atom.consistencyFlags=[...preserved,...result.flags,...inspectEvidenceRoles({...atom,status:atom.rawStatus??atom.status},{text:atom.text},packets)];
 }
 const output=path.join(dir,opts.output);
 await writeReview(output,buildReview(code,summary.items,checks));
 console.log(`Đã tạo phiếu: ${path.join(output,'review-report.md')}. Không gọi Ollama, không sửa kết quả gốc.`);
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) main().catch(e=>{console.error(e.code==='EEXIST'?'Phiếu đã tồn tại; dùng --output tên-khác để giữ đánh giá cũ.':e.message);process.exitCode=1;});
