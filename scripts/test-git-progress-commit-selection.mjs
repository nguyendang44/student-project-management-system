// No extra test framework. Run after npm ci/install: node scripts/test-git-progress-commit-selection.mjs
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const requireFrontend = createRequire(resolve(root, 'frontend/package.json'))
const ts = requireFrontend('typescript')
const source = readFileSync(resolve(root, 'frontend/src/features/gitProgress/gitProgress.commitSelection.ts'), 'utf8')
const transpiled = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 }
}).outputText
const { latestTwentyValidCommits, isCommitOrderValid, formatCommitChoice } =
  await import(`data:text/javascript;base64,${Buffer.from(transpiled).toString('base64')}`)

const sha = n => n.toString(16).padStart(40, '0')
const original = Array.from({ length: 25 }, (_, i) => ({
  sha: sha(25 - i), message: `Fix feature ${25 - i}\nmore details`, author: 'Student',
  committedAt: '2026-11-07T00:00:00Z', htmlUrl: 'https://github.com/example/repo'
}))
const picked = latestTwentyValidCommits([...original, { ...original[0], sha: 'invalid' }])
assert.equal(picked.length, 20)
assert.equal(picked[0].sha, sha(25))
assert.equal(picked[19].sha, sha(6))
assert.equal(picked[0].sha.length, 40)
assert.equal(isCommitOrderValid(picked, picked[1].sha, picked[0].sha), true)
assert.equal(isCommitOrderValid(picked, picked[0].sha, picked[1].sha), false)
assert.equal(isCommitOrderValid(picked, picked[0].sha, picked[0].sha), false)
assert.equal(isCommitOrderValid(picked, sha(99), picked[0].sha), false)
assert.equal(isCommitOrderValid([], sha(2), sha(1)), false)
assert.match(formatCommitChoice(picked[0]), /Fix feature 25/)
assert.match(formatCommitChoice(picked[0]), /Student/)
assert.ok(!formatCommitChoice(picked[0]).includes('more details'))
console.log('PASS: Git Progress recent-20 commit choices, SHA, ordering and labels (10 assertions)')
