// No reference answers or release-specific identifiers are used in this module.
export function preparePackets(code) {
  const files = new Map();
  for (const packet of code.packets) {
    if (!files.has(packet.file)) files.set(packet.file, []);
    files.get(packet.file).push(...packet.lines);
  }
  const result = [];
  for (const [file, lines] of files) {
    let group = [], lastOld = null, lastNew = null;
    const flush = () => {
      if (group.some(l => l.kind !== 'context')) {
        // Keep each original hunk together. Large hunks split only between
        // change runs, never halfway through a removed/added replacement pair.
        const runs = [];
        for (let i = 0; i < group.length; i++) {
          if (group[i].kind === 'context') continue;
          const start = i;
          while (i + 1 < group.length && group[i + 1].kind !== 'context') i++;
          runs.push([Math.max(0, start - 3), Math.min(group.length, i + 4)]);
        }
        const size = group.reduce((sum, l) => sum + l.text.length + 65, 0);
        if (size <= 9500) result.push({ file, lines: group });
        else {
          let selected = new Map();
          for (const [start,end] of runs) {
            const slice = group.slice(start,end);
            const merged = new Map([...selected, ...slice.map(l => [l.id,l])]);
            if (selected.size && [...merged.values()].reduce((sum,l) => sum+l.text.length+65,0) > 9500) {
              result.push({ file, lines: [...selected.values()] }); selected = new Map();
            }
            for (const l of slice) selected.set(l.id,l);
          }
          if (selected.size) result.push({ file, lines: [...selected.values()] });
        }
      }
      group = [];
    };
    for (const line of lines) {
      if ((line.beforeLine !== null && lastOld !== null && line.beforeLine > lastOld + 1) ||
          (line.afterLine !== null && lastNew !== null && line.afterLine > lastNew + 1)) flush();
      group.push(line);
      if (line.beforeLine !== null) lastOld = line.beforeLine;
      if (line.afterLine !== null) lastNew = line.afterLine;
    }
    flush();
  }
  return result;
}

export function pairedText(packets) {
  return packets.map(p => {
    const render = (side, excluded) => p.lines.filter(l => l.kind !== excluded).map(l =>
      `${l.id} ${l.kind === 'context' ? 'UNCHANGED' : side === 'beforeLine' ? 'REMOVED' : 'ADDED'} L${l[side]} | ${l.text}`
    ).join('\n');
    return `FILE ${p.file}\nBEFORE (code cũ):\n${render('beforeLine','added')}\nAFTER (code mới):\n${render('afterLine','removed')}`;
  }).join('\n\n');
}

export function splitClaimLegacy(claim) {
  // Conservative syntactic splitting, not an LLM and not based on gold labels.
  // Keep the entire original report alongside each clause for pronoun context.
  const pieces = claim.text.split(/;\s*|\s+và\s+(?=(?:đã|toàn bộ|tất cả|tốc độ|hiệu suất)(?:\s|$))|\.\s+(?=[A-ZĐ])/u)
    .map(x => x.trim()).filter(Boolean);
  return pieces.map((text,i) => ({ id: `${claim.id}.${i+1}`, text, originalText: claim.text, terms: claim.terms }));
}

export function requiresExecutionEvidence(text) {
  return /(?:đã\s+đo|đã\s+(?:chạy|pass).{0,40}(?:test|kiểm thử)|(?:test|kiểm thử).{0,60}(?:đã chạy|pass|thành công)|(?:tốc độ|hiệu suất).{0,60}(?:tăng|giảm|nhanh|\d+\s*%)|(?:tăng|giảm).{0,20}\d+\s*%)/iu.test(text);
}

export function aggregateStatuses(statuses) {
  if (!statuses.length) return 'INSUFFICIENT';
  if (statuses.every(x => x === 'SUPPORTED')) return 'SUPPORTED';
  if (statuses.includes('SUPPORTED') || statuses.includes('PARTIAL')) return 'PARTIAL';
  if (statuses.includes('CONTRADICTED')) return 'CONTRADICTED';
  return 'INSUFFICIENT';
}

export function codeFence(source) {
  const longest = Math.max(2,...(String(source).match(/`+/g) ?? []).map(x => x.length));
  const fence = '`'.repeat(longest + 1);
  return `${fence}python\n${source}\n${fence}`;
}

export function deduplicateFindings(items) {
  const seen = new Set();
  return items.map(item => {
    // Only exact descriptions with identical evidence are collapsed, not merely
    // claims sharing a broad feature name. The raw candidate remains visible.
    const key = JSON.stringify([item.description, [...(item.evidenceIds ?? [])].sort()]);
    const duplicate = item.citationValid && seen.has(key);
    if (item.citationValid) seen.add(key);
    return duplicate ? { ...item, citationValid: false, rejectionReason: 'Nhận xét trùng nguyên văn và bằng chứng' } : item;
  });
}

// Conservative Python inline-comment scanner: do not strip a # inside strings.
// Ambiguous triple-quoted text is left intact rather than classified as code-only.
export function withoutInlineComment(line) {
  if (line.includes('"""') || line.includes("'''")) return line;
  let quote = null, escaped = false;
  for (let i = 0; i < line.length; i++) {
    const ch = line[i];
    if (escaped) { escaped = false; continue; }
    if (quote && ch === '\\') { escaped = true; continue; }
    if (quote) { if (ch === quote) quote = null; continue; }
    if (ch === '"' || ch === "'") { quote = ch; continue; }
    if (ch === '#') return line.slice(0,i).trimEnd();
  }
  return line.trimEnd();
}

export function changeGroups(packets) {
  const groups = [];
  for (const packet of packets) {
    for (let i = 0; i < packet.lines.length; i++) {
      if (packet.lines[i].kind === 'context') continue;
      const start = i;
      while (i+1 < packet.lines.length && packet.lines[i+1].kind !== 'context') i++;
      const changed = packet.lines.slice(start,i+1);
      const before = changed.filter(l=>l.kind==='removed');
      const after = changed.filter(l=>l.kind==='added');
      const beforeText = before.map(l=>l.text).join('\n');
      const afterText = after.map(l=>l.text).join('\n');
      const oldCode = before.map(l=>withoutInlineComment(l.text)).filter(l=>l.trim()).join('\n');
      const newCode = after.map(l=>withoutInlineComment(l.text)).filter(l=>l.trim()).join('\n');
      const commentOnly = packet.file.endsWith('.py') && beforeText !== afterText && oldCode === newCode &&
        changed.some(l=>withoutInlineComment(l.text)!==l.text.trimEnd());
      groups.push({ id: `G-${changed[0].id}-${changed.at(-1).id}`, file:packet.file, before, after,
        context: packet.lines.slice(Math.max(0,start-2),Math.min(packet.lines.length,i+3)).filter(l=>l.kind==='context'),
        commentOnly });
    }
  }
  return groups;
}

export function mechanicalFinding(group) {
  const hasTypeIgnore = [...group.before,...group.after].some(l=>l.text.includes('type: ignore'));
  return { groupId: group.id, source:'comment-rule', category:hasTypeIgnore?'type_annotation':'documentation',
    title:hasTypeIgnore?'Thay đổi chú thích kiểm tra kiểu':'Thay đổi chú thích mã nguồn',
    before:group.before.map(l=>l.text).join('\n') || '(không có dòng xóa)',
    after:group.after.map(l=>l.text).join('\n') || '(không có dòng thêm)',
    description:'Phần mã ngoài chú thích trong nhóm này không đổi; không có căn cứ gọi đây là thêm hàm, thêm tham số hoặc sửa logic chạy.',
    beforeEvidenceIds:group.before.map(l=>l.id),afterEvidenceIds:group.after.map(l=>l.id),
    evidenceIds:[...group.before,...group.after].map(l=>l.id),citationValid:true,rejectionReason:null };
}

// Split explicit lists of cases, retaining their shared subject verbatim.
export function splitClaim(claim) {
  const clauses=splitClaimLegacy(claim);
  const texts=clauses.flatMap(c=>{
    const match=/^(.*?[,;]\s*)(có trường hợp\s+)(.+)$/u.exec(c.text);
    if(!match) return [c.text];
    const cases=match[3].split(/\s+và\s+(?:có\s+)?trường hợp\s+/u);
    return cases.length>1 ? cases.map(x=>match[1]+match[2]+x) : [c.text];
  });
  return texts.map((text,i)=>({id:`${claim.id}.${i+1}`,text,originalText:claim.text,terms:claim.terms}));
}
