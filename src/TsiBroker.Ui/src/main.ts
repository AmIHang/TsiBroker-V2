import './assets/main.less'

import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './router'
import i18n from './i18n'

if (import.meta.env.VITE_THEME_COLOR) {
  document.documentElement.style.setProperty('--color-primary', import.meta.env.VITE_THEME_COLOR)
  recolorFavicon(import.meta.env.VITE_THEME_COLOR)
}

// The favicon is a static SVG, so its "Offene Hexagonkontur" default color (#6f4295,
// matching --color-primary's default) is swapped for the themed color at runtime here.
function recolorFavicon(color: string) {
  const link = document.querySelector<HTMLLinkElement>('link[rel="icon"]')
  if (!link) return
  fetch(link.href)
    .then((res) => res.text())
    .then((svg) => {
      link.href = `data:image/svg+xml,${encodeURIComponent(svg.replaceAll('#6f4295', color))}`
    })
}

const app = createApp(App)

app.use(createPinia())
app.use(router)
app.use(i18n)

router.isReady().then(() => {
  app.mount('#app')
})
