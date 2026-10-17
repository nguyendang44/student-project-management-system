#!/usr/bin/env node
'use strict'
// Locale-only presentation patch. NEVER touches API, SQL, datetime values, or business logic.
const fs = require('node:fs')
const path = require('node:path')
const root = path.resolve(__dirname, '..')
const sourceRoot = path.join(root, 'frontend', 'src')
const dryRun = process.argv.includes('--dry-run')
if (!fs.existsSync(sourceRoot)) {
  console.error('Không tìm thấy frontend/src. Hãy giải nén bản vá đúng thư mục dự án.')
  process.exit(1)
}
const opts = "{ day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false, hourCycle: 'h23' }"
const timeOpts = "{ hour: '2-digit', minute: '2-digit', hour12: false, hourCycle: 'h23' }"
function matchingParen(str, start) {
  let depth = 0, quote = '', escape = false
  for (let i = start; i < str.length; i++) {
    const c = str[i]
    if (quote) {
      if (escape) escape = false
      else if (c === '\\') escape = true
      else if (c === quote) quote = ''
      continue
    }
    if (c === '"' || c === "'" || c === '`') { quote = c; continue }
    if (c === '(') depth++
    if (c === ')' && --depth === 0) return i
  }
  return -1
}
function update(input) {
  let count = 0
  let s = input
  // Locate method calls, instead of replacing numeric .toLocaleString() calls.
  const re = /\.(toLocaleString|toLocaleTimeString)\s*\(/g
  const ranges = []
  for (const match of s.matchAll(re)) {
    const open = match.index + match[0].length - 1
    const end = matchingParen(s, open)
    if (end === -1) continue
    const args = s.slice(open + 1, end).trim()
    const method = match[1]
    const noArgs = !args
    const literalLocale = /^(['"])(vi-VN|en-GB|en-US)\1$/i.exec(args)
    const dateConstructed = /new\s+Date\s*\([^\n]*\)\s*$/.test(s.slice(Math.max(0, match.index - 220), match.index))
    const dateOnly = /\.toLocaleDateString/.test(method)
    if (dateOnly) continue
    if (literalLocale || (noArgs && dateConstructed)) {
      let locale = literalLocale ? literalLocale[2] : 'vi-VN'
      if (locale === 'en-US' && !dateConstructed) continue  // may be number formatting
      if (locale !== 'en-GB') locale = 'vi-VN'
      const fmt = method === 'toLocaleTimeString' ? timeOpts : opts
      ranges.push([open + 1, end, `'${locale}', ${fmt}`])
    } else if (/^(['"])(vi-VN|en-GB|en-US)\1\s*,\s*\{[\s\S]*\}$/i.test(args)) {
      // Leave already-correct 24-hour calls untouched (idempotent patch).
      if (/\bhour12\s*:\s*false\b/.test(args) && /\bhourCycle\s*:\s*['"]h23['"]/.test(args)) continue
      // Respect existing dateStyle/timeStyle or timezone settings.
      const comma = args.indexOf(',')
      const locale = args.slice(0, comma)
      if (/en-US/i.test(locale) && !dateConstructed) continue
      let object = args.slice(comma + 1).trim()
      // Override the local hour display without dropping existing options.
      object = object.replace(/\bhour12\s*:\s*(?:true|false)\s*,?/g, '')
      object = object.replace(/\bhourCycle\s*:\s*(['"])[^'"]+\1\s*,?/g, '')
      object = object.replace(/^\{/, "{ hour12: false, hourCycle: 'h23',")
      ranges.push([open + 1, end, `${locale}, ${object}`])
    }
  }
  for (const [start, end, value] of ranges.reverse()) {
    s = s.slice(0, start) + value + s.slice(end)
    count++
  }
  // Native datetime-local and time input visuals depend on browser/OS. en-GB gives a
  // 24-hour hint where locale overrides are supported, without altering ISO values.
  s = s.replace(/<input\b[^>]*>/g, tag => {
    if (!/\btype\s*=\s*['"](?:datetime-local|time)['"]/.test(tag) || /\blang\s*=/.test(tag)) return tag
    count++
    return tag.replace(/\s*\/>$/, ' lang="en-GB" />').replace(/(?<!\/)\>$/, ' lang="en-GB">')
  })
  return { output: s, count }
}
function walk(d) {
  let result = []
  for (const e of fs.readdirSync(d, {withFileTypes:true})) {
    if (e.name === 'node_modules' || e.name.startsWith('.')) continue
    const p = path.join(d,e.name)
    if (e.isDirectory()) result = result.concat(walk(p))
    else if (e.isFile() && /\.(?:vue|ts|tsx|js|jsx)$/.test(e.name) && !e.name.endsWith('.d.ts')) result.push(p)
  }
  return result
}
let touched = 0, changedCalls = 0
for (const p of walk(sourceRoot)) {
  const before = fs.readFileSync(p,'utf8')
  const {output, count} = update(before)
  if (output === before) continue
  touched++; changedCalls += count
  console.log(`${dryRun ? 'SẼ SỬA' : 'ĐÃ SỬA'} ${path.relative(root,p)} (${count} vị trí)`)
  if (!dryRun) {
    const bak = p + '.before-time24.bak'
    if (!fs.existsSync(bak)) fs.copyFileSync(p,bak)
    fs.writeFileSync(p,output,'utf8')
  }
}
console.log(`${dryRun?'PREVIEW':'HOÀN TẤT'}: ${touched} file, ${changedCalls} vị trí. Không thay đổi Backend/SQL.`)
if (!touched) console.log('Không có vị trí mới cần sửa, hoặc các lời gọi đang dùng formatter khác. Kiểm tra những chỗ vẫn hiện SA/CH trên màn hình.')
