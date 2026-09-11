import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { useAppearanceStore } from './stores/appearance'
import { useThemeStore } from './stores/theme'
import { usePluginManifestStore } from './stores/pluginManifest'
import { setupManifestRoutes } from './router'
import { initAuthToken, consumeTokenFromHash, installTokenHashWatcher } from './services/authInit'
import { exposeSharedDeps } from './shared/exposeSharedDeps'

/* 样式引入顺序（后加载优先级更高）：
   1. Element Plus 官方暗色变量基础
   2. 主题文件（覆盖 --el-* 变量，切换主题换此文件）
   3. Tailwind 工具类（布局 + 色彩引用，@theme 在 :root 固定 --color-*）
   4. 背景图片模式适配层（最后加载，确保 .has-bg-image 覆盖 @theme 的 --color-*）
   组件样式由 unplugin-vue-components 按需自动引入 */
import 'element-plus/theme-chalk/dark/css-vars.css'
/* ElMessage / ElMessageBox 均以「函数」方式调用（非模板组件），unplugin-vue-components
   不会为它们注入样式，必须显式引入组件样式，否则弹窗与轻提示缺失全部定位/外观。
   这是历史上各处用 !important 硬定位与 offset:60 兜底的根因，故在此统一补齐。
   刻意引用 theme-chalk 的纯 CSS 而非 `element-plus/es/.../style/css`：
   后者会引入新的 JS 依赖并触发 Vite 依赖预打包，而本环境的 safe-delete shim 会拦截
   预打包目录清理导致 dev server 直接崩溃。 */
import 'element-plus/theme-chalk/el-message-box.css'
import 'element-plus/theme-chalk/el-message.css'
import './styles/themes/workshop-forge.css'
import './styles/tailwind.css'
import './styles/themes/bg-image-mode.css'

// 暴露宿主共享依赖给插件界面（import map → public/shared/*.js shim → 本桥）。
// MUST 早于清单路由注册与任何插件界面加载，否则 shim 取不到宿主实例会直接抛错。
exposeSharedDeps()

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

// 消费托盘跳转携带的一次性 token（#token=xxx）。
// 必须同步且早于 initAuthToken：写入后 initAuthToken 会因本地已有 token 而跳过，
// 首屏请求也能带上 Authorization 头。无论命中与否都会清掉 fragment。
consumeTokenFromHash()

// 同一个浏览器里已打开过主界面时，托盘再次「打开主界面」只会改变 fragment，
// 浏览器不会重新加载文档 → 上面的首屏消费不会执行。此监听兜住这条路径。
installTokenHashWatcher()

// 初始化 API token（异步，不阻塞应用挂载）
initAuthToken()

app.mount('#app')
