import {inspectComparisons,returnTypeCheck,inspectEvidenceRoles,claimTimeScope,validateAuditInput} from './comparison.mjs';
import {flagFinding,buildReview,writeReview,flagConsistency} from './review.mjs';
import {attachStructure,structureFacts,orderedSource} from './structure.mjs';
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { summaryPrompt, claimPrompt, validateFindings, validateClaim, retrieve, evidenceIndex, makeReport, evaluateClaimWithRetry, validateResume, summarizePacket, normalizeCitations, claimCodeText, summaryContext, testRows, validateObservations } from './run.mjs';
import { preparePackets, splitClaim, aggregateStatuses, requiresExecutionEvidence, codeFence, changeGroups, withoutInlineComment } from './analysis-v2.mjs';
import { replayClaim } from './recheck.mjs';

test('offline replay rejects altered source and preserves a recorded model label', () => {
  const atom={id:'X.1',text:'A report claim'};
  const packets=[{file:'src/example.py',lines:[{id:'E001',kind:'added',afterLine:1,beforeLine:null,text:'value = 1'}]}];
  const input={claim:atom,packets};
  const canonical=evidenceIndex({packets});
  const answer={claimId:atom.id,claimQuote:atom.text,status:'CONTRADICTED',explanation:'The supplied code contradicts this claim.',evidenceIds:['E001 ADDED L1 | value = 1']};
  const saved={result:{responseError:true,attempts:[{answer}]}};
  const result=replayClaim(saved,input,atom,canonical);
  assert.equal(result.status,'CONTRADICTED');
  assert.deepEqual(result.evidenceIds,['E001']);
  const altered=structuredClone(input);
  altered.packets[0].lines[0].text='value = 2';
  assert.throws(()=>replayClaim(saved,altered,atom,canonical),/không khớp/);
});

const here = path.dirname(fileURLToPath(import.meta.url));
const code = JSON.parse(await fs.readFile(path.join(here, 'data/flask-3.1.2.code.json'), 'utf8'));
const expected = JSON.parse(await fs.readFile(path.join(here, 'data/flask-3.1.2.expected.json'), 'utf8'));

test('source identifiers are unique, versions pinned, and reference anchors exist in added code', async () => {
  for (const name of ['flask-3.1.2','flask-3.1.1-holdout']) {
    const source = JSON.parse(await fs.readFile(path.join(here, `data/${name}.code.json`), 'utf8'));
    const gold = JSON.parse(await fs.readFile(path.join(here, `data/${name}.expected.json`), 'utf8'));
    const index = evidenceIndex(source);
    assert.match(source.baseSha, /^[a-f0-9]{40}$/);
    assert.match(source.headSha, /^[a-f0-9]{40}$/);
    assert.notEqual(source.baseSha, source.headSha);
    assert.ok([...index.values()].every(l => l.file.startsWith('src/flask/') || l.file.startsWith('tests/')));
    for (const fact of gold.referenceFacts) for (const anchor of fact.anchors)
      assert.ok([...index.values()].some(l => l.file === fact.file && l.kind === 'added' && l.text.includes(anchor)));
  }
});

test('summary prompt never receives release notes or prepared report answers', () => {
  const prompt = summaryPrompt(code.packets);
  assert.ok(!prompt.includes(expected.releaseNotesUrl));
  for (const claim of expected.claims) assert.ok(!prompt.includes(claim.text));
  const claim = expected.claims[0];
  const prompt2 = claimPrompt(claim, retrieve(claim, code));
  assert.ok(!prompt2.includes(claim.reason));
  assert.ok(!prompt2.includes('"expected"'));
});

test('rejects fabricated IDs and context-only citations as evidence for a change', () => {
  const packet = code.packets.find(p => p.lines.some(l => l.kind === 'context'));
  const unchanged = packet.lines.find(l => l.kind === 'context');
  const results = validateFindings({ items: [
    { title: 'Nhận xét', description: 'Mô tả một thay đổi cần kiểm tra.', evidenceIds: ['DOES_NOT_EXIST'] },
    { title: 'Nhận xét', description: 'Mô tả một thay đổi cần kiểm tra.', evidenceIds: [unchanged.id] }
  ] }, [packet]);
  assert.ok(results.every(x => !x.citationValid));
});

test('rejects citations to a real line that was not sent to the model', () => {
  const packet = code.packets[0];
  const other = code.packets[1].lines[0].id;
  const result = validateFindings({ items: [{ title: 'Thay đổi', description: 'Mô tả chỉnh sửa có dẫn chứng ngoài prompt.', evidenceIds: [other] }] }, [packet]);
  assert.equal(result[0].citationValid, false);
});

test('claim retrieval is bounded and labels require valid evidence except insufficient', () => {
  const claim = expected.claims[0];
  const packets = retrieve(claim, code);
  assert.ok(packets.length > 0);
  const invalid = validateClaim({ claimId: claim.id, claimQuote: claim.text, status: 'SUPPORTED', explanation: 'Có thay đổi trong code.', evidenceIds: [] }, claim, packets);
  assert.equal(invalid.citationValid, false);
  const unknown = validateClaim({ claimId: claim.id, claimQuote: claim.text, status: 'INSUFFICIENT', explanation: 'Chưa có đủ bằng chứng.', evidenceIds: [] }, claim, packets);
  assert.equal(unknown.citationValid, true);
  assert.throws(() => validateClaim({ ...unknown, claimId: 'WRONG' }, claim, packets));
});

test('report distinguishes valid citations from semantic correctness', () => {
  const report = makeReport(code, expected, [], [], 'test-model');
  assert.ok(report.includes('ID hợp lệ không chứng minh nội dung nhận xét đúng'));
  assert.ok(report.includes('Chưa duyệt'));
  assert.ok(report.includes('không phải % độ đúng của toàn bộ AI'));
});

test('code-only stage runs without answer file and reuses completed batches', async () => {
  const tmp = await fs.mkdtemp(path.join(os.tmpdir(), 'flask-benchmark-test-'));
  const originalFetch = globalThis.fetch;
  const originalLog = console.log;
  let generateCalls = 0;
  try {
    await fs.mkdir(path.join(tmp, 'data'));
    await fs.copyFile(path.join(here, 'run.mjs'), path.join(tmp, 'run.mjs'));
    await fs.copyFile(path.join(here, 'structure.mjs'), path.join(tmp, 'structure.mjs'));
    await fs.copyFile(path.join(here, 'review.mjs'), path.join(tmp, 'review.mjs'));
    await fs.copyFile(path.join(here,'comparison.mjs'),path.join(tmp,'comparison.mjs'));
    await fs.copyFile(path.join(here, 'audit.mjs'), path.join(tmp, 'audit.mjs'));
    await fs.copyFile(path.join(here, 'data/flask-3.1.2.structure.json'),path.join(tmp,'data/flask-3.1.2.structure.json'));
    await fs.copyFile(path.join(here, 'analysis-v2.mjs'), path.join(tmp, 'analysis-v2.mjs'));
    await fs.copyFile(path.join(here, 'data/flask-3.1.2.code.json'), path.join(tmp, 'data/flask-3.1.2.code.json'));
    globalThis.fetch = async (url, options) => {
      if (url.endsWith('/api/tags')) return Response.json({ models: [{ name: 'qwen2.5-coder:7b', digest: 'fake-test-digest' }] });
      const payload = JSON.parse(options.body);
      assert.ok(!payload.prompt.includes(expected.releaseNotesUrl));
      generateCalls++;
      if (payload.format.properties.claimId) {
        const id = payload.prompt.match(/"claimId":"(C\d+(?:\.\d+)?)"/)[1];
        assert.ok(!payload.prompt.includes(expected.claims.find(c => c.id === id.split('.')[0]).reason));
        assert.ok(!payload.format.required.includes('observations'));
        const atom=expected.claims.flatMap(splitClaim).find(a=>a.id===id);
        const observations=testRows(retrieve(atom,code)).map(l=>({evidenceId:l.id,quote:l.text,meaning:'Quan sát dòng kiểm thử.'}));
        return Response.json({ response: JSON.stringify({ claimId: id, claimQuote: payload.prompt.match(/Ý CẦN KIỂM TRA: (.*)\n/)[1], status: 'INSUFFICIENT',
          explanation: 'Chưa đủ bằng chứng trong phản hồi giả lập kiểm thử.', evidenceIds: [] }), done_reason: 'stop' });
      }
      return Response.json({ response: '{"items":[]}', done_reason: 'stop' });
    };
    console.log = () => {};
    const { main } = await import(pathToFileURL(path.join(tmp, 'run.mjs')).href);
    await main(['--stage','summarize']);
    assert.equal(generateCalls, changeGroups(preparePackets(code)).filter(g=>!g.commentOnly).length);
    await main(['--stage','summarize']);
    assert.equal(generateCalls, changeGroups(preparePackets(code)).filter(g=>!g.commentOnly).length);
    const result = JSON.parse(await fs.readFile(path.join(tmp, 'results/flask-3.1.2/prompt-v39/summary.json'), 'utf8'));
    assert.ok(result.items.length > 0);
    assert.ok(result.items.every(x=>['comment-rule','uncovered'].includes(x.source)));
    await fs.copyFile(path.join(here, 'data/flask-3.1.2.expected.json'), path.join(tmp, 'data/flask-3.1.2.expected.json'));
    await main(['--stage','assess']);
    assert.equal(generateCalls, changeGroups(preparePackets(code)).filter(g=>!g.commentOnly).length + expected.claims.flatMap(splitClaim).length);
    const assessment = JSON.parse(await fs.readFile(path.join(tmp, 'results/flask-3.1.2/prompt-v39/assessment.json'), 'utf8'));
    assert.equal(assessment.checks.length, 6);
    const oldSummary=await fs.readFile(path.join(tmp,'results/flask-3.1.2/prompt-v39/summary.json'),'utf8');
    const beforeAuditCalls=generateCalls;
    const {main:audit}=await import(pathToFileURL(path.join(tmp,'audit.mjs')).href);
    await audit(['--run','prompt-v39','--case','flask-3.1.2','--output','offline-check']);
    assert.equal(generateCalls,beforeAuditCalls);
    assert.equal(await fs.readFile(path.join(tmp,'results/flask-3.1.2/prompt-v39/summary.json'),'utf8'),oldSummary);
    assert.ok((await fs.readFile(path.join(tmp,'results/flask-3.1.2/prompt-v39/offline-check/review-report.md'),'utf8')).includes('Chưa duyệt'));
    assert.ok((await fs.readFile(path.join(tmp, 'results/flask-3.1.2/prompt-v39/report.md'), 'utf8')).includes('1/6'));
    const baselineDir = path.join(tmp, 'results/flask-3.1.2/baseline');
    await fs.mkdir(baselineDir);
    const oldReport = JSON.stringify({ signature: 'legacy-v1', items: [] });
    await fs.writeFile(path.join(baselineDir, 'summary.json'), oldReport);
    await assert.rejects(main(['--run','baseline']), /Không ghi đè/);
    assert.equal(await fs.readFile(path.join(baselineDir, 'summary.json'), 'utf8'), oldReport);
    await assert.rejects(main(['--run','fix','--resume-from','prompt-v2']), /chạy mới/);
  } finally {
    globalThis.fetch = originalFetch;
    console.log = originalLog;
    await fs.rm(tmp, { recursive: true, force: true });
  }
});


test('pairs old and new source across an arbitrary input packet boundary', () => {
  const result = preparePackets({ packets: [
    { file: 'x.py', lines: [{ id:'E1',kind:'removed',beforeLine:1,afterLine:null,text:'x = old' }] },
    { file: 'x.py', lines: [{ id:'E2',kind:'added',beforeLine:null,afterLine:1,text:'x = new' }] }
  ] });
  assert.equal(result.length,1);
  assert.deepEqual(result[0].lines.map(l=>l.id), ['E1','E2']);
});

test('server assigns both sides by group and ignores model-supplied line IDs', () => {
  const packets=[{file:'x.py',lines:[
    {id:'E1',kind:'removed',beforeLine:1,afterLine:null,text:'x = 1'},
    {id:'E2',kind:'added',beforeLine:null,afterLine:1,text:'x = 2'}
  ]}];
  const group=changeGroups(packets)[0];
  const item={groupId:group.id,category:'logic',title:'Đổi giá trị',before:'x bằng 1',after:'x bằng 2',description:'Thay đổi giá trị gán cho x.',
    beforeEvidenceIds:['FABRICATED'],afterEvidenceIds:['WRONG']};
  const result=validateFindings({items:[item]},packets)[0];
  assert.equal(result.citationValid,true);
  assert.deepEqual(result.beforeEvidenceIds,['E1']);
  assert.deepEqual(result.afterEvidenceIds,['E2']);
  assert.equal(validateFindings({items:[{...item,groupId:'unknown'}]},packets)[0].citationValid,false);
});

test('splits compound reports without using expected answers', () => {
  const claim = {id:'NEW',text:'Đổi khai báo kiểu và đã đo tốc độ tăng 25%.',terms:[]};
  const atoms=splitClaim(claim);
  assert.equal(atoms.length,2);
  assert.equal(requiresExecutionEvidence(atoms[0].text),false);
  assert.equal(requiresExecutionEvidence(atoms[1].text),true);
  assert.equal(aggregateStatuses(['SUPPORTED','INSUFFICIENT']),'PARTIAL');
  assert.equal(aggregateStatuses(['CONTRADICTED']),'CONTRADICTED');
  assert.equal(requiresExecutionEvidence('Thêm mã kiểm thử cho yêu cầu POST.'),false);
});

test('does not accept a model rewriting the report to a different type', () => {
  const claim={id:'C9.1',text:'Đổi kiểu thành t.IO[str].',terms:[]};
  assert.throws(()=>validateClaim({claimId:claim.id,claimQuote:'Đổi kiểu thành t.IO[bytes].',
    status:'SUPPORTED',explanation:'Có bằng chứng mã nguồn.',evidenceIds:[]},claim,[]));
});

test('preserves code punctuation and safely fences embedded backticks', () => {
  const source='def f(x: str | None) -> str: # ``` <tag>';
  const rendered=codeFence(source);
  assert.ok(rendered.includes(source));
  assert.ok(rendered.startsWith('````python'));
  const packet=code.packets.find(p=>p.lines.some(l=>l.text.includes(' -> ')));
  const line=packet.lines.find(l=>l.text.includes(' -> '));
  const report=makeReport(code,null,[{title:'Kiểm tra',category:'type_annotation',before:'Trước',after:'Sau',
    description:'Giữ nguyên ký tự code.',evidenceIds:[line.id],citationValid:true}],[],'test');
  assert.ok(report.includes(line.text));
});


test('retries wrong quote without rewriting claim and keeps errors distinct from insufficient', async () => {
  const claim={id:'X.1',text:'Tốc độ tăng 10%.',terms:[]};
  let calls=0;
  const valid=await evaluateClaimWithRetry(claim,[],async prompt=>{
    calls++;
    if(calls===1) return {claimId:claim.id,claimQuote:'Tốc độ tăng 20%.',status:'INSUFFICIENT',explanation:'Không có số liệu đo thực tế.',evidenceIds:[]};
    assert.ok(prompt.includes('SỬA ĐỊNH DẠNG'));
    return {claimId:claim.id,claimQuote:claim.text,status:'INSUFFICIENT',explanation:'Không có số liệu đo thực tế.',evidenceIds:[]};
  });
  assert.equal(calls,2); assert.equal(valid.status,'INSUFFICIENT'); assert.equal(valid.attempts.length,2);
  const failed=await evaluateClaimWithRetry(claim,[],async()=>({claimId:'WRONG'}));
  assert.equal(failed.status,'ERROR'); assert.equal(failed.citationValid,false); assert.equal(failed.attempts.length,3);
  await assert.rejects(evaluateClaimWithRetry(claim,[],async()=>{throw new TypeError('fetch failed');}),/fetch failed/);
});

test('resume accepts only matching source versions and model', () => {
  const saved={signature:'old',model:'qwen2.5-coder:7b',baseSha:code.baseSha,headSha:code.headSha,items:[]};
  assert.deepEqual(validateResume(saved,code,saved.model),[]);
  assert.throws(()=>validateResume({...saved,headSha:'wrong'},code,saved.model));
  assert.throws(()=>validateResume(saved,code,'another-model'));
});


test('comment-only changes are labeled as rules and never sent for semantic AI inference', async () => {
  const packet={file:'x.py',lines:[
    {id:'E1',kind:'removed',beforeLine:1,afterLine:null,text:'    x = value # type: ignore'},
    {id:'E2',kind:'added',beforeLine:null,afterLine:1,text:'    x = value'}
  ]};
  const result=await summarizePacket(packet,()=>{throw new Error('Must not call model');});
  assert.equal(result[0].source,'comment-rule');
  assert.equal(result[0].category,'type_annotation');
  assert.equal(result[0].citationValid,true);
  assert.equal(withoutInlineComment('x = "# literal" # comment'),'x = "# literal"');
  const literal={...packet,lines:packet.lines.map((l,i)=>({...l,text:i?'x = "# b"':'x = "# a"'}))};
  assert.equal(changeGroups([literal])[0].commentOnly,false);
});

test('a mixed hunk keeps logic changes for AI but separates comment-only replacements', async () => {
  const packet={file:'x.py',lines:[
    {id:'E1',kind:'removed',beforeLine:1,afterLine:null,text:'x = value # type: ignore'},
    {id:'E2',kind:'added',beforeLine:null,afterLine:1,text:'x = value'},
    {id:'E3',kind:'context',beforeLine:2,afterLine:2,text:'unchanged = 0'},
    {id:'E4',kind:'removed',beforeLine:3,afterLine:null,text:'y = 1'},
    {id:'E5',kind:'added',beforeLine:null,afterLine:3,text:'y = 2'}
  ]};
  let calls=0;
  const result=await summarizePacket(packet,async(prompt,schema)=>{
    calls++;assert.ok(!prompt.split('NGỮ CẢNH LÂN CẬN')[0].includes('type: ignore'));assert.ok(prompt.includes('y = 2'));
    const id=schema.properties.items.items.properties.groupId.enum[0];
    return {items:[{groupId:id,category:'logic',title:'Đổi giá trị',before:'y bằng 1',after:'y bằng 2',description:'Đổi giá trị gán cho y.'}]};
  });
  assert.equal(calls,1);assert.equal(result.length,2);
  assert.deepEqual(result[1].evidenceIds,['E4','E5']);
});

test('C5 input retains both closing and reading the response', () => {
  const claim=expected.claims.find(x=>x.id==='C5');
  const packets=retrieve(claim,code);
  const prompt=claimPrompt({...claim,id:'C5.1'},packets);
  assert.ok(prompt.includes('client.get().close()'));
  assert.ok(prompt.includes('assert client.get().text == "flask"'));
  assert.ok(prompt.includes('DÒNG KIỂM THỬ CẦN ĐỌC HẾT'));
});

test('bad summary structure retries and surfaces ERROR instead of fabricating work', async () => {
  const packet={file:'x.py',lines:[{id:'E1',kind:'added',beforeLine:null,afterLine:1,text:'x = 2'}]};
  let calls=0;
  const result=await summarizePacket(packet,async()=>{calls++;return {items:[{groupId:'bad'}]};});
  assert.equal(calls,3);assert.equal(result[0].source,'model-error');assert.equal(result[0].citationValid,false);
});


test('verbatim copied source rows normalize only with matching ID, kind, line and text', () => {
  const p=[{file:'x.py',lines:[{id:'E42',kind:'added',beforeLine:null,afterLine:8,text:'    x: t.IO[bytes]'}]}];
  const exact='E42 ADDED L8 |     x: t.IO[bytes]';
  assert.deepEqual(normalizeCitations([exact],p).ids,['E42']);
  assert.equal(normalizeCitations([exact.replace('bytes','str')],p).changes.length,0);
  assert.equal(normalizeCitations([exact.replace('L8','L9')],p).changes.length,0);
  assert.equal(normalizeCitations([exact.replace('ADDED','REMOVED')],p).changes.length,0);
  assert.equal(normalizeCitations(['E42 arbitrary suffix'],p).changes.length,0);
});

test('added-only group does not require AI to invent a before description', () => {
  const packet={file:'x.py',lines:[{id:'E1',kind:'added',beforeLine:null,afterLine:1,text:'x = 1'}]};
  const group=changeGroups([packet])[0];
  const result=validateFindings({items:[{groupId:group.id,category:'logic',title:'Thêm giá trị',description:'Gán giá trị cho biến.'}]},[packet])[0];
  assert.equal(result.citationValid,true);assert.equal(result.before,'(không có dòng xóa)');
});


test('explicit case lists split with their shared subject without splitting type conjunctions',()=>{
 const c={id:'Z',text:'Thêm test cho upload, có trường hợp file rỗng và trường hợp file lớn.',terms:[]};
 const parts=splitClaim(c);
 assert.equal(parts.length,2);
 assert.equal(parts[0].text,'Thêm test cho upload, có trường hợp file rỗng');
 assert.equal(parts[1].text,'Thêm test cho upload, có trường hợp file lớn.');
 assert.ok(parts.every(p=>p.originalText===c.text));
 assert.equal(splitClaim({id:'Z',text:'Lưu app context và request context.',terms:[]}).length,1);
});

test('observation inventory rejects omitted assertions and invented quotes',()=>{
 const packets=[{file:'tests/x.py',lines:[{id:'E1',kind:'added',text:'assert value == 2',afterLine:1,beforeLine:null}]}];
 assert.throws(()=>validateObservations([],packets),/Bỏ sót/);
 assert.throws(()=>validateObservations([{evidenceId:'E1',quote:'assert value == 3',meaning:'Sai giá trị'}],packets),/nguyên văn/);
 assert.doesNotThrow(()=>validateObservations([{evidenceId:'E1',quote:'assert value == 2',meaning:'Đối chiếu giá trị bằng 2'}],packets));
});

test('targeted summaries cannot silently select another group or hide an empty result',async()=>{
 const packet={file:'x.py',lines:[
 {id:'E1',kind:'added',beforeLine:null,afterLine:1,text:'x = 1'},
 {id:'E2',kind:'context',beforeLine:1,afterLine:2,text:'unchanged()'},
 {id:'E3',kind:'added',beforeLine:null,afterLine:3,text:'y = 2'}]};
 const groups=changeGroups([packet]);
 const empty=await summarizePacket(packet,async()=>({items:[]}),()=>{},groups[0].id);
 assert.equal(empty[0].source,'uncovered');
 const wrong=await summarizePacket(packet,async()=>({items:[{groupId:groups[1].id,category:'logic',title:'Thay đổi',description:'Nhóm không được giao'}]}),()=>{},groups[0].id);
 assert.equal(wrong[0].responseError,true);
});


test('single-group schema rejects duplicates and excludes unrelated changes from context',async()=>{
 const packet={file:'x.py',lines:[
 {id:'E1',kind:'added',beforeLine:null,afterLine:1,text:'unrelated = 1'},
 {id:'E2',kind:'context',beforeLine:1,afterLine:2,text:'unchanged()'},
 {id:'E3',kind:'added',beforeLine:null,afterLine:3,text:'target = 2'}]};
 const target=changeGroups([packet])[1];
 assert.ok(!summaryContext([packet],target.id)[0].lines.some(l=>l.id==='E1'));
 let calls=0;
 const result=await summarizePacket(packet,async(prompt,schema)=>{
  calls++;assert.equal(schema.properties.items.maxItems,1);
  assert.ok(!prompt.includes('unrelated = 1'));
  const item={groupId:target.id,category:'logic',title:'Thay đổi',description:'Thêm phép gán target'};
  return {items:[item,item]};
 },()=>{},target.id);
 assert.equal(calls,3);assert.equal(result[0].responseError,true);
 const report=makeReport(code,null,result,[],'mock');
 assert.ok(report.includes('Nhóm chưa hoàn tất (trả rỗng hoặc lỗi): 1'));
});

test('all response errors do not display a misleading zero accuracy',()=>{
 const report=makeReport(code,expected,[],[{claimId:'X',status:'ERROR',rawStatus:'ERROR',expected:'SUPPORTED',citationValid:false}], 'mock');
 assert.ok(report.includes('Chưa có mục hợp lệ để tính điểm nhãn'));
 assert.ok(!report.includes('**0/1'));
});


test('pinned AST distinguishes docstring examples, existing parameters and test operations',async()=>{
 const metadata=JSON.parse(await fs.readFile(path.join(here,'data/flask-3.1.2.structure.json'),'utf8'));
 const enriched=attachStructure(code,metadata),facts=structureFacts(enriched.packets);
 const type=facts.filter(f=>['E0179','E0180'].includes(f.id));
 assert.equal(type.length,2);
 assert.ok(type.every(f=>f.function==='send_file' && Object.hasOwn(f.parameters,'path_or_file')));
 assert.notEqual(type[0].parameters.path_or_file,type[1].parameters.path_or_file);
 const example=enriched.packets.flatMap(p=>p.lines).find(l=>l.text.includes('@app.get("/stream")'));
 assert.ok(facts.filter(f=>f.id===example.id).every(f=>f.scope==='docstring' && f.docstringOwner==='stream_with_context'));
 assert.ok(facts.find(f=>f.id==='E0339' && f.side==='after').operations.some(o=>o.kind==='Call' && o.code==='client.get().close()'));
 assert.ok(facts.find(f=>f.id==='E0341' && f.side==='after').operations.some(o=>o.kind==='Assert'));
 assert.throws(()=>attachStructure({...code,headSha:'different'},metadata),/commit/);
 const altered=structuredClone(metadata);altered.records[0].text+='tampered';
 assert.throws(()=>attachStructure(code,altered),/không khớp/);
});


test('review flags structural conflicts without rewriting model or passing unreviewed meaning',async()=>{
 const meta=JSON.parse(await fs.readFile(path.join(here,'data/flask-3.1.2.structure.json'),'utf8'));
 const enriched=attachStructure(code,meta);
 const item={source:'ollama',title:'Thêm tham số path_or_file',description:'Thêm tham số path_or_file mới',category:'logic',evidenceIds:['E0179','E0180']};
 const original=JSON.stringify(item);
 assert.ok(flagFinding(item,enriched).some(f=>f.rule==='EXISTING_PARAMETER'));
 assert.equal(JSON.stringify(item),original);
 const example=enriched.packets.flatMap(p=>p.lines).find(l=>l.kind==='added'&&l.text.includes('@app.get("/stream")'));
 const flags=flagFinding({...item,title:'Đổi POST thành GET',description:'Đổi phương thức',evidenceIds:[example.id]},enriched);
 assert.ok(flags.some(f=>f.rule==='DOCSTRING_CATEGORY'));
 assert.ok(flags.some(f=>f.rule==='UNANCHORED_HTTP_METHOD'));
 const review=buildReview(enriched,[item],[]);
 assert.equal(review.summary[0].manual.explanation,'UNREVIEWED');
 assert.equal(review.summary[0].manual.evidenceRelevance,'UNREVIEWED');
 assert.equal(flagFinding({...item,source:'comment-rule'},enriched).length,0);
});

test('review does not overwrite an existing manual checklist',async()=>{
 const root=await fs.mkdtemp(path.join(os.tmpdir(),'review-'));
 try{
  const dir=path.join(root,'manual');
  await writeReview(dir,buildReview(code,[],[]));
  const file=path.join(dir,'review-checklist.json');
  await fs.writeFile(file,'manual edit');
  await assert.rejects(writeReview(dir,buildReview(code,[],[])),e=>e.code==='EEXIST');
  assert.equal(await fs.readFile(file,'utf8'),'manual edit');
 }finally{await fs.rm(root,{recursive:true,force:true});}
});

test('holdout AST matches pinned source rows without inspecting expected answers',async()=>{
 const source=JSON.parse(await fs.readFile(path.join(here,'data/flask-3.1.1-holdout.code.json'),'utf8'));
 const metadata=JSON.parse(await fs.readFile(path.join(here,'data/flask-3.1.1-holdout.structure.json'),'utf8'));
 const attached=attachStructure(source,metadata);
 assert.ok(attached.packets.every(p=>p.lines.every(l=>l.structure)));
});


test('label conflict flags preserve the model answer and do not relabel unknown wording',()=>{
 const a={status:'SUPPORTED',explanation:'Kiểu mới là Number. Do đó, báo cáo không chính xác.'};
 const original=JSON.stringify(a);
 assert.equal(flagConsistency(a).length,1);assert.equal(JSON.stringify(a),original);
 assert.equal(flagConsistency({status:'CONTRADICTED',explanation:'Do đó, báo cáo chính xác.'}).length,1);
 assert.equal(flagConsistency({status:'SUPPORTED',explanation:'Báo cáo không đúng ở bản cũ. Vì vậy, báo cáo chính xác.'}).length,0);
 assert.equal(flagConsistency({status:'INSUFFICIENT',explanation:'Chưa thể xác nhận.'}).length,0);
});

test('ordered source separates versions and scopes, deduplicates and sorts line positions',()=>{
 const row=(id,line,text,kind='added')=>({id,afterLine:line,beforeLine:kind==='context'?line:null,text,kind,structure:{after:{docstring:false,function:{name:'build'},operations:[{kind:'Call'}]}}});
 const packet={file:'example.py',lines:[row('B',12,'    values.append(current)'),row('A',9,'        values.extend(previous)'),row('B',12,'    values.append(current)')]};
 const groups=orderedSource([packet]);
 assert.deepEqual(groups[0].rows.map(r=>r.id),['A','B']);
 assert.equal(groups[0].rows[0].code,'        values.extend(previous)');
 assert.equal(groups[0].side,'after');
 assert.equal(groups[0].function,'build');
 const doc=structuredClone(packet);doc.lines.forEach(l=>l.structure.after.docstring=true);
 assert.equal(orderedSource([doc]).length,0);
});


test('structured comparisons flag conflicts without changing model labels',()=>{
 const claim={text:'Trả về Number'};const packets=[{file:'x.py',lines:[{id:'E1',text:'def read() -> Text:',kind:'added'}]}];
 const answer={status:'SUPPORTED',comparisons:[{claimPart:'Number',codeReading:'Text',codeIds:['E1'],relation:'CONFLICT'}]};
 const before=JSON.stringify(answer),result=inspectComparisons(answer,claim,packets);
 assert.ok(result.flags.some(f=>f.rule==='COMPARISON_LABEL_CONFLICT'));
 assert.equal(JSON.stringify(answer),before);
 const bad=inspectComparisons({...answer,comparisons:[{...answer.comparisons[0],claimPart:'Text',codeIds:['OUTSIDE']}]},claim,packets);
 assert.equal(bad.comparisons[0].structureValid,false);
 assert.ok(bad.flags.some(f=>f.rule==='COMPARISON_INVALID'));
 assert.ok(inspectComparisons({status:'SUPPORTED'},claim,packets).flags.some(f=>f.rule==='COMPARISON_MISSING'));
});

test('return annotation diagnostic compares target from report with AST, independent of gold labels',()=>{
 const row={id:'E1',text:'def read() -> Actual:',structure:{after:{function:{name:'Reader.read',returnAnnotation:'Actual'}}}};
 const packets=[{file:'x.py',lines:[row]}];
 const wrong=returnTypeCheck({text:'Reader.read đổi type hint trả về từ Old sang Expected.'},packets);
 assert.equal(wrong.expected,'Expected');assert.equal(wrong.actual,'Actual');assert.equal(wrong.relation,'CONFLICT');
 assert.equal(returnTypeCheck({text:'Reader.read đổi type hint trả về thành Actual.'},packets).relation,'MATCH');
 assert.equal(returnTypeCheck({text:'Hàm chạy nhanh hơn.'},packets),null);
 assert.equal(returnTypeCheck({text:'Reader.read đổi type hint trả về thành List[str].'},packets),null);
 assert.ok(inspectComparisons({status:'SUPPORTED',comparisons:[null]}, {text:'x'},packets).flags.length);
 const other=structuredClone(row);other.id='E2';other.structure.after.function.name='Other.read';
 assert.equal(returnTypeCheck({text:'read đổi type hint trả về thành Actual.'},[{file:'x.py',lines:[row,other]}]),null);
});


test('current-state matches using removed lines are warned without rewriting the answer',()=>{
 const packets=[{file:'x.py',lines:[
 {id:'E1',kind:'removed',beforeLine:1,afterLine:null,text:'items = [old]'},
 {id:'E2',kind:'added',beforeLine:null,afterLine:1,text:'items = []'},
 {id:'E3',kind:'context',beforeLine:2,afterLine:2,text:'use(items)'}]}];
 const claim={text:'Danh sách mới khởi tạo bằng [old].'};
 const answer={status:'SUPPORTED',evidenceIds:['E1','E2'],comparisons:[{claimPart:claim.text,codeReading:'Đọc được [old]',codeIds:['E1','E2'],relation:'MATCH'}]};
 const original=JSON.stringify(answer),result=inspectComparisons(answer,claim,packets);
 assert.ok(result.flags.some(f=>f.rule==='REMOVED_EVIDENCE_FOR_AFTER'));
 assert.deepEqual(result.comparisons[0].evidenceSources.map(e=>e.versions),[['BEFORE'],['AFTER']]);
 assert.equal(JSON.stringify(answer),original);
 assert.equal(claimTimeScope('Đổi giá trị từ old sang new.'),'TRANSITION');
 const historical={text:'Phiên bản cũ khởi tạo bằng [old].'};
 assert.ok(!inspectComparisons({...answer,comparisons:[{...answer.comparisons[0],claimPart:historical.text}]},historical,packets).flags.some(f=>f.rule==='REMOVED_EVIDENCE_FOR_AFTER'));
 const current=inspectComparisons({...answer,evidenceIds:['E2','E3'],comparisons:[{...answer.comparisons[0],codeIds:['E2','E3']}]},claim,packets);
 assert.ok(!current.flags.some(f=>f.rule==='REMOVED_EVIDENCE_FOR_AFTER'));
 const prompt=claimCodeText(packets);
 assert.ok(prompt.indexOf('AFTER')<prompt.indexOf('BEFORE'));
 assert.ok(!prompt.split('BEFORE')[0].includes('E1'));
});

test('audit checks saved evidence side against canonical source and quote flags are specific',()=>{
 const atom={claimId:'X.1',text:'toàn bộ test đã pass'};
 const source={packets:[{file:'x.py',lines:[{id:'E1',kind:'removed',beforeLine:1,afterLine:null,text:'x = 1'}]}]};
 const input={claim:{id:atom.claimId,text:atom.text},packets:structuredClone(source.packets)};
 assert.doesNotThrow(()=>validateAuditInput(input,atom,source));
 input.packets[0].lines[0].kind='added';
 assert.throws(()=>validateAuditInput(input,atom,source),/không khớp/);
 const result=inspectComparisons({status:'INSUFFICIENT',comparisons:[{claimPart:'tất cả test đã pass',codeReading:'Không có log',codeIds:[],relation:'UNKNOWN'}]},{text:atom.text},source.packets);
 assert.ok(result.flags.some(f=>f.reason.includes('claimPart')));
 assert.equal(result.comparisons[0].structureValid,false);
});


test('evidence role checks distinguish append from initialization without altering labels',()=>{
 const mk=(id,kind,line,code)=>({id,kind:'added',afterLine:line,beforeLine:null,text:code,structure:{after:{function:{name:'build'},operations:[{kind,startLine:line,endLine:line,code}]}}});
 const packets=[{file:'x.py',lines:[mk('A','AnnAssign',1,'items: list = []'),mk('B','Call',2,'items.extend(old)'),mk('C','Call',3,'items.append(current)')]}];
 const claim={text:'Danh sách mới khởi tạo bằng [current] rồi mới extend các mục cũ.'};
 const answer={status:'SUPPORTED',evidenceIds:['C']};const original=JSON.stringify(answer);
 const flags=inspectEvidenceRoles(answer,claim,packets);
 assert.ok(flags.some(f=>f.rule==='INITIALIZATION_EVIDENCE_MISSING'&&f.reason.includes('A')));
 assert.ok(flags.some(f=>f.rule==='ORDER_EVIDENCE_INCOMPLETE'));
 assert.equal(JSON.stringify(answer),original);
 assert.equal(inspectEvidenceRoles({...answer,evidenceIds:['A','B','C']},claim,packets).length,0);
 const noMeta=[{file:'x.py',lines:packets[0].lines.map(({structure,...row})=>row)}];
 assert.ok(inspectEvidenceRoles(answer,claim,noMeta).some(f=>f.rule==='EVIDENCE_ROLE_UNCHECKED'));
});
