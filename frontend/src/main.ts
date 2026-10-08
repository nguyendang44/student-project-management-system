import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './app/router'
import './styles/main.css'
import { useAuthStore } from './stores/auth'
import { setAuthTokenProvider } from './api/client'
const app = createApp(App)
app.use(createPinia())
setAuthTokenProvider(() => useAuthStore().accessToken)
app.use(router)
app.mount('#app')
