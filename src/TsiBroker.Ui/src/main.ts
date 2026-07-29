import './assets/main.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './router'

if (import.meta.env.VITE_THEME_COLOR) {
  document.documentElement.style.setProperty('--color-primary', import.meta.env.VITE_THEME_COLOR)
}

const app = createApp(App)

app.use(createPinia())
app.use(router)

router.isReady().then(() => {
  app.mount('#app')
})
