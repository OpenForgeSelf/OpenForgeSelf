import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { useThemeStore } from './stores/theme'

/* 样式引入顺序（后加载优先级更高）：
   1. Element Plus 官方暗色变量基础
   2. 主题文件（覆盖 --el-* 变量，切换主题换此文件）
   3. 旧变量兼容层（映射到 --el-*，未迁移页面依赖）
   4. Tailwind 工具类（布局 + 色彩引用）
   组件样式由 unplugin-vue-components 按需自动引入 */
import 'element-plus/theme-chalk/dark/css-vars.css'
import './styles/themes/workshop-forge.css'
import './styles/legacy-compat.css'
import './styles/tailwind.css'

const app = createApp(App)
const pinia = createPinia()

app.use(pinia)
app.use(router)

useThemeStore(pinia).initialize()

app.mount('#app')
