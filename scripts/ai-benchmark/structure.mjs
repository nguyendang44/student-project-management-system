import fs from 'node:fs/promises';
import path from 'node:path';

// Metadata is produced from Python AST at both pinned commits, never from gold labels.
export function attachStructure(code, metadata) {
  if(metadata.baseSha!==code.baseSha || metadata.headSha!==code.headSha) throw new Error('AST không khớp hai commit');
  const index=new Map();
  for(const r of metadata.records) {
    const key=`${r.file}:${r.id}:${r.side}`;
    if(index.has(key)) throw new Error('AST trùng bản ghi');
    index.set(key,r);
  }
  return {...code,packets:code.packets.map(p=>({...p,lines:p.lines.map(l=>{
    const structure={};
    for(const side of ['before','after']) {
      const number=l[side==='before'?'beforeLine':'afterLine'];
      if(number===null) continue;
      const record=index.get(`${p.file}:${l.id}:${side}`);
      if(!record || record.text!==l.text || record.line!==number) throw new Error(`AST không khớp dòng ${l.id}`);
      structure[side]=record;
    }
    return {...l,structure};
  })}))};
}
export async function loadStructure(code, directory) {
  try {return attachStructure(code,JSON.parse(await fs.readFile(path.join(directory,'data',`${code.id}.structure.json`),'utf8')));}
  catch(e) {if(e.code==='ENOENT') return code; throw e;}
}
export function structureFacts(packets) {
  const facts=[];
  for(const p of packets) for(const l of p.lines) for(const side of ['before','after']) {
    const r=l.structure?.[side];
    if(!r) continue;
    facts.push({id:l.id,side,scope:r.docstring?'docstring':p.file.startsWith('tests/')?'test-code':'application-code',
      function:r.function?.name ?? null,returnAnnotation:r.function?.returnAnnotation??null,docstringOwner:r.docstringOwner,
      // Full parameter inventory is unnecessary on every body line.
      ...(r.function && (r.function.startLine===r.line || /^\s*[\w]+\s*:/.test(l.text)) ? {parameters:r.function.parameters}:{}),
      operations:r.operations});
  }
  return facts;
}

// Lexical source order, not a control-flow or runtime trace. Preserve indentation.
export function orderedSource(packets) {
  const groups=new Map();
  for(const packet of packets) for(const row of packet.lines) for(const side of ['before','after']) {
    const line=row[side==='before'?'beforeLine':'afterLine'];
    if(line==null) continue;
    const meta=row.structure?.[side];
    if(meta?.docstring || !row.text.trim() || row.text.trimStart().startsWith('#')) continue;
    const selected=meta?.operations?.length || /^\s*(?:if\b|elif\b|else:|for\b|while\b|with\b|return\b|[\w.]+(?:\s*:[^=]+)?\s*=(?!=))/.test(row.text);
    if(!selected) continue;
    const scope=meta?.function?.name??'(chưa xác định hàm)';
    const key=JSON.stringify([packet.file,scope,side]);
    if(!groups.has(key))groups.set(key,{file:packet.file,function:scope,side,rows:new Map()});
    groups.get(key).rows.set(row.id,{id:row.id,line,code:row.text,kinds:[...new Set((meta?.operations??[]).map(o=>o.kind))]});
  }
  return [...groups.values()].map(g=>({...g,rows:[...g.rows.values()].sort((a,b)=>a.line-b.line)}));
}
