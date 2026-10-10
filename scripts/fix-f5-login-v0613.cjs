'use strict'
// Surgical, idempotent installer: updates only the existing auth store, navigation guard and stale login footnote.
const fs = require('node:fs')
const path = require('node:path')
const root = path.resolve(__dirname, '..')
const authPath = path.join(root, 'frontend', 'src', 'stores', 'auth.ts')
const routerPath = path.join(root, 'frontend', 'src', 'app', 'router.ts')
const loginPath = path.join(root, 'frontend', 'src', 'views', 'LoginView.vue')
const helperPath = path.join(root, 'frontend', 'src', 'stores', 'authSession.ts')
for (const file of [authPath, routerPath, loginPath, helperPath]) {
  if (!fs.existsSync(file)) throw new Error(`File not found: ${file}. Unzip the patch at the project root.`)
}
const original = {
  auth: fs.readFileSync(authPath, 'utf8'),
  router: fs.readFileSync(routerPath, 'utf8'),
  login: fs.readFileSync(loginPath, 'utf8'),
}
const isPatched = original.auth.includes('readAuthSession') && original.auth.includes('async function initialize()')
  && original.router.includes('await auth.initialize()')
if (isPatched) {
  console.log('v0.6.13 F5 login fix already installed. Nothing changed.')
  process.exit(0)
}
if (original.auth.includes('readAuthSession') || original.router.includes('await auth.initialize()')) {
  throw new Error('Detected a partial/other auth persistence patch. Files are unchanged. Inspect first.')
}
const newline = original.auth.includes('\r\n') ? '\r\n' : '\n'
const normalize = value => value.replace(/\r\n/g, '\n')
let auth = normalize(original.auth)
let router = normalize(original.router)
let login = normalize(original.login)
function replaceExactlyOnce(source, find, replace, label) {
  const first = source.indexOf(find)
  if (first < 0 || source.indexOf(find, first + find.length) >= 0)
    throw new Error(`Cannot identify exactly one ${label}. No files modified. Please send frontend/src/stores/auth.ts and frontend/src/app/router.ts.`)
  return source.slice(0, first) + replace + source.slice(first + find.length)
}
// Refuse to update a different or customized auth implementation rather than overwrite it.
for (const snippet of ["import { authApi } from '../features/auth/auth.api'", "import type { CurrentUser } from '../features/auth/auth.types'", "export const useAuthStore = defineStore('auth', () => {", "function clear() {", "async function logout() {"]) {
  if (!auth.includes(snippet)) throw new Error(`Auth store layout changed: ${snippet}. No files modified.`)
}
auth = replaceExactlyOnce(auth,
  "import { authApi } from '../features/auth/auth.api'",
  "import { authApi } from '../features/auth/auth.api'\nimport { HttpError } from '../api/client'\nimport { readAuthSession, writeAuthSession, removeAuthSession } from './authSession'", 'auth imports')
auth = replaceExactlyOnce(auth,
  '/** Access token is intentionally held in memory only, not localStorage/sessionStorage. */',
  '/** Login survives F5; Backend remains the source of truth for session validity. */', 'obsolete storage comment')
auth = replaceExactlyOnce(auth,
  "  const accessToken = ref<string | null>(null)\n  const currentUser = ref<CurrentUser | null>(null)",
  "  const saved = readAuthSession()\n  const accessToken = ref<string | null>(saved?.accessToken ?? null)\n  const currentUser = ref<CurrentUser | null>(saved?.currentUser ?? null)\n  const expiresAt = ref<string | null>(saved?.expiresAt ?? null)\n  let requiresValidation = Boolean(saved)\n  let validation: Promise<void> | null = null", 'auth state')
auth = replaceExactlyOnce(auth,
  "    currentUser.value = {\n      userId: response.userId, email: response.email, fullName: response.fullName, role: response.role,\n    }",
  "    const signedInUser: CurrentUser = {\n      userId: response.userId, email: response.email, fullName: response.fullName, role: response.role,\n    }\n    currentUser.value = signedInUser\n    expiresAt.value = response.expiresAt\n    requiresValidation = false\n    writeAuthSession({ accessToken: response.accessToken, currentUser: signedInUser, expiresAt: response.expiresAt })", 'successful login')
auth = replaceExactlyOnce(auth,
  '  function clear() {',
  `  /** Restore login across a reload and check with /auth/me before allowing navigation. */
  async function initialize(): Promise<void> {
    if (!accessToken.value) return
    if (!expiresAt.value || Date.parse(expiresAt.value) <= Date.now()) { clear(); return }
    if (!requiresValidation) return
    if (validation) return validation
    const validatingToken = accessToken.value
    validation = (async () => {
      try {
        const user = await authApi.me()
        // A different login/logout may have happened while the request was in flight.
        if (accessToken.value !== validatingToken) return
        currentUser.value = user
        requiresValidation = false
        if (expiresAt.value) {
          writeAuthSession({ accessToken: validatingToken, currentUser: user, expiresAt: expiresAt.value })
        }
      } catch (error) {
        if (accessToken.value !== validatingToken) return
        // A revoked/disabled/expired account MUST be signed out.
        // A transient network/server error MUST NOT erase the browser session.
        if (error instanceof HttpError && (error.status === 401 || error.status === 403)) clear()
      }
    })()
    try { await validation } finally { validation = null }
  }
  function clear() {`, 'session validation')
auth = replaceExactlyOnce(auth,
  '    accessToken.value = null\n    currentUser.value = null',
  '    accessToken.value = null\n    currentUser.value = null\n    expiresAt.value = null\n    requiresValidation = false\n    removeAuthSession()', 'logout/clear')
auth = replaceExactlyOnce(auth,
  '  return { accessToken, currentUser, isAuthenticated, role, login, logout, clear }',
  '  return { accessToken, currentUser, isAuthenticated, role, login, logout, clear, initialize }', 'store exports')
router = replaceExactlyOnce(router, 'router.beforeEach((to) => {', 'router.beforeEach(async (to) => {', 'router guard')
router = replaceExactlyOnce(router,
  '  const auth = useAuthStore()\n  if (to.path === \'/login\')',
  '  const auth = useAuthStore()\n  await auth.initialize()\n  if (to.path === \'/login\')', 'restore before redirects')
const oldFootnote = 'Phiên đăng nhập được lưu trong bộ nhớ. Tải lại trang sẽ yêu cầu đăng nhập lại.'
if (login.includes(oldFootnote)) {
  login = replaceExactlyOnce(login, oldFootnote,
    'F5 không làm mất đăng nhập nếu phiên còn hiệu lực. Đăng xuất trên thiết bị dùng chung.', 'stale login footnote')
}
const updated = {
  [authPath]: auth.replace(/\n/g, newline),
  [routerPath]: router.replace(/\n/g, original.router.includes('\r\n') ? '\r\n' : '\n'),
  [loginPath]: login.replace(/\n/g, original.login.includes('\r\n') ? '\r\n' : '\n'),
}
const ts = new Date().toISOString().replace(/[.:]/g, '-').slice(0, 19)
const changedPaths = Object.entries(updated).filter(([filename, content]) => fs.readFileSync(filename, 'utf8') !== content)
for (const [filename] of changedPaths) fs.copyFileSync(filename, `${filename}.before-v0613-${ts}.bak`)
try {
  for (const [filename, content] of changedPaths) fs.writeFileSync(filename, content, 'utf8')
} catch (error) {
  for (const [filename] of changedPaths) fs.copyFileSync(`${filename}.before-v0613-${ts}.bak`, filename)
  throw error
}
console.log('v0.6.13 installed: F5 preserves login until JWT expires; /auth/me validates on reload.')
console.log('Updated:', changedPaths.map(([filename]) => path.relative(root, filename)).join(', '))
console.log('Backups use suffix:', `.before-v0613-${ts}.bak`)
