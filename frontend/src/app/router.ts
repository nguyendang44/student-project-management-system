import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import AppLayout from '../components/AppLayout.vue'
import { modules } from './modules'
import { usePreviewStore } from '../stores/preview'
const moduleRoutes: RouteRecordRaw[] = modules.map((mod) => ({
  path: mod.path.slice(1), name: mod.id,
  component: () => import('../views/ModuleView.vue'),
  props: { moduleId: mod.id },
  meta: { title: mod.title, previewRoles: mod.roles },
}))
const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', component: () => import('../views/LoginView.vue'), meta: { title: 'Đăng nhập' } },
    { path: '/', component: AppLayout, children: [ { path: '', redirect: '/dashboard' }, ...moduleRoutes ] },
    { path: '/:pathMatch(.*)*', component: () => import('../views/NotFoundView.vue') },
  ],
})
router.beforeEach((to) => {
  // Client-side preview navigation only. Security is always enforced by the future server handlers.
  const roles = to.meta.previewRoles as string[] | undefined
  if (roles && !roles.includes(usePreviewStore().role)) return '/dashboard'
  return true
})
export default router
