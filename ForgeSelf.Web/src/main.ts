import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { useAppearanceStore } from './stores/appearance'
import { useThemeStore } from './stores/theme'
import { usePluginManifestStore } from './stores/pluginManifest'
import { setupManifestRoutes } from './router'
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

// 清单驱动的动态视图挂载：在首次导航时（含刷新/深链接）加载前端插件清单并注册
// manifest 路由（真实 import() 懒加载视图），随后重新解析本次导航，确保动态路由在
// 首次进入即可匹配。loadManifest 内部捕获错误：清单失败/为空时 manifest 为空数组，
// 不注入任何动态路由，现有静态路由原样兜底。
const manifestStore = usePluginManifestStore(pinia)
let manifestRoutesReady = false
router.beforeEach(async (to) => {
  if (manifestRoutesReady) return
  manifestRoutesReady = true
  await manifestStore.loadManifest()
  setupManifestRoutes(manifestStore.manifest)
  return { path: to.fullPath, replace: true }
})

// 初始化 API token（异步，不阻塞应用挂载）
initAuthToken()

app.mount('#app')
