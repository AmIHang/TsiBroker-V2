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
    {
      path: '/railway-undertakings/:id',
      name: 'railway-undertaking-edit',
      component: () => import('../views/RailwayUndertakingEditView.vue'),
      meta: { title: 'Eisenbahnverkehrsunternehmen bearbeiten' },
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
