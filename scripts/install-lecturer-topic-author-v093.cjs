#!/usr/bin/env node
'use strict'
// v0.9.3: upgrade only 6 source files; never modify database, migrations or old scripts.
const fs = require('node:fs')
const path = require('node:path')
const root = path.resolve(__dirname, '..')
const changes = require('./lecturer-topic-author-v093.patch.json')
const staged = []
function count(haystack, needle) { return haystack.split(needle).length - 1 }
try {
  for (const [relative, rules] of Object.entries(changes)) {
    const file = path.resolve(root, relative)
    if (!file.startsWith(root + path.sep)) throw new Error('Invalid path ' + relative)
    if (!fs.existsSync(file)) throw new Error('Missing source file: ' + relative)
    const original = fs.readFileSync(file, 'utf8')
    let updated = original
    for (const [i, rule] of rules.entries()) {
      if (count(updated, rule.to) === 1) continue // already installed
      if (count(updated, rule.from) !== 1) throw new Error('Unsupported source version: ' + relative + ' patch ' + (i + 1))
      updated = updated.replace(rule.from, rule.to)
    }
    staged.push({ file, relative, original, updated })
  }
  if (staged.every(f => f.updated === f.original)) {
    console.log('v0.9.3 already installed; no source changed.')
    process.exit(0)
  }
  const date = new Date().toISOString().replace(/[:.]/g, '-')
  const backup = path.join(root, 'scripts', 'v093-backups', date)
  for (const item of staged) {
    const target = path.join(backup, item.relative)
    fs.mkdirSync(path.dirname(target), { recursive: true })
    fs.writeFileSync(target, item.original, 'utf8')
  }
  const written = []
  try {
    for (const item of staged.filter(f => f.updated !== f.original)) {
      const tmp = item.file + '.v093-temp'
      fs.writeFileSync(tmp, item.updated, 'utf8')
      try { fs.renameSync(tmp, item.file) }
      catch (e) { try { fs.unlinkSync(tmp) } catch {} throw e }
      written.push(item)
      console.log('PATCHED ' + item.relative)
    }
  } catch (e) {
    for (const item of written.reverse()) fs.writeFileSync(item.file, item.original, 'utf8')
    throw e
  }
  console.log('v0.9.3 applied. Backups: ' + backup)
  console.log('No database changes or migrations were run.')
} catch (e) {
  console.error('v0.9.3 installation failed: ' + e.message)
  process.exitCode = 1
}
