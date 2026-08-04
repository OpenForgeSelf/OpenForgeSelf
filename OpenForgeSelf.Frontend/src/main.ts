import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { useAppearanceStore } from './stores/appearance'
import { useThemeStore } from './stores/theme'
import { initAuthToken } from './services/authInit'

/* 样式引入顺序（后加载优先级更高）：
   1. Element Plus 官方暗色变量基础
   2. 主题文件（覆盖 --el-* 变量，切换主题换此文件）
   3. Tailwind 工具类（布局 + 色彩引用，@theme 在 :root 固定 --color-*）
   4. 背景图片模式适配层（最后加载，确保 .has-bg-image 覆盖 @theme 的 --color-*）
   组件样式由 unplugin-vue-components 按需自动引入 */
import 'element-plus/theme-chalk/dark/css-vars.css'
import './styles/themes/workshop-forge.css'
import './styles/tailwind.css'
import './styles/themes/bg-image-mode.css'

const app = createApp(App)
const pinia = createPinia()

app.use(pinia)
app.use(router)

useThemeStore(pinia).initialize()
useAppearanceStore(pinia).initialize()

// 初始化 API token（异步，不阻塞应用挂载）
initAuthToken()

app.mount('#app')
