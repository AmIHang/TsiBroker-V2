import { createRouter, createWebHistory } from 'vue-router'
import SendMessageView from '../views/SendMessageView.vue'
import { useAuthStore } from '@/stores/auth'

declare module 'vue-router' {
  interface RouteMeta {
    public?: boolean
    title?: string
  }
}

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: () => import('../views/LoginView.vue'),
      meta: { public: true },
    },
    {
      path: '/',
      name: 'send',
      component: SendMessageView,
      meta: { title: 'Sent messages' },
    },
    {
      path: '/received',
      name: 'received',
      component: () => import('../views/ReceivedMessagesView.vue'),
      meta: { title: 'Received messages' },
    },
    {
      path: '/settings',
      name: 'settings',
      component: () => import('../views/ResponseConfigView.vue'),
      meta: { title: 'Response settings' },
    },
    {
      path: '/whoami',
      name: 'whoami',
      component: () => import('../views/WhoAmIView.vue'),
      meta: { title: 'WhoAmI' },
    },
  ],
})

router.beforeEach(async (to) => {
  if (to.meta.public) {
    return true
  }

  const auth = useAuthStore()
  if (!auth.isChecked) {
    await auth.fetchMe()
  }

  if (!auth.username) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  return true
})

export default router
