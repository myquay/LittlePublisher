import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    name: 'home',
    component: () => import('@/views/HomeView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/webmentions',
    name: 'webmentions',
    component: () => import('@/views/WebmentionsView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/posts/new',
    name: 'post-new',
    component: () => import('@/views/PostEditorView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/posts/:id',
    name: 'post-edit',
    component: () => import('@/views/PostEditorView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/LoginView.vue'),
    meta: { guest: true },
  },
  {
    path: '/callback',
    name: 'callback',
    component: () => import('@/views/CallbackView.vue'),
  },
]

if (import.meta.env.DEV) {
  routes.push(
    {
      path: '/mockups',
      name: 'mockups',
      component: () => import('@/views/DesignMockupsView.vue'),
      meta: { guest: true },
    },
    {
      path: '/design-board',
      name: 'design-board',
      component: () => import('@/views/DesignBoardView.vue'),
      meta: { guest: true },
    },
  )
}

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

// Navigation guard
router.beforeEach(async (to, _from, next) => {
  const authStore = useAuthStore()

  // Wait for auth initialization
  if (!authStore.isInitialized) {
    await new Promise<void>((resolve) => {
      const checkInit = () => {
        if (authStore.isInitialized) {
          resolve()
        } else {
          setTimeout(checkInit, 50)
        }
      }
      checkInit()
    })
  }

  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    next({ name: 'login', query: { redirect: to.fullPath } })
  } else if (to.meta.guest && authStore.isAuthenticated) {
    next({ name: 'home' })
  } else {
    next()
  }
})

export default router
