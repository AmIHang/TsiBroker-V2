import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '../views/HomeView.vue'
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
      name: 'home',
      component: HomeView,
      meta: { title: 'Startseite' },
    },
    {
      path: '/about',
      name: 'about',
      // route level code-splitting
      // this generates a separate chunk (About.[hash].js) for this route
      // which is lazy-loaded when the route is visited.
      component: () => import('../views/AboutView.vue'),
      meta: { title: 'Über' },
    },
    {
      path: '/infrastructure-operators',
      name: 'infrastructure-operators',
      component: () => import('../views/InfrastructureOperatorsView.vue'),
      meta: { title: 'Infrastrukturbetreiber' },
    },
    {
      path: '/railway-undertakings',
      name: 'railway-undertakings',
      component: () => import('../views/RailwayUndertakingsView.vue'),
      meta: { title: 'Eisenbahnverkehrsunternehmen' },
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
