/**
 * todo-tracker 插件界面入口（契约：spec 010 插件界面远程加载）。
 *
 * - 产物是 ESM，入口固定 `web/dist/index.js`；
 * - **导出名必须等于 plugin.json 的 `frontend.views[0]`（TodoView）**，宿主加载器按该名取组件；
 * - vue / vue-router / pinia / element-plus 都在 vite.config.ts 里声明为 external，
 *   运行时经宿主 import map 解析到同一份实例（漏声明会内联第二份 Vue ⇒ inject 失效，只在点击那一刻报错）。
 *
 * 本界面刻意只用原生 HTML + Tailwind 布局原语 + `--el-*` 主题变量，不引入 Element Plus 组件：
 * 宿主暴露的共享桥是**手工维护的清单**（public/shared/element-plus.js 自述），
 * 取不到就是 undefined，界面会在渲染时整块崩掉；而待办面板不需要那些重交互组件。
 */
import { createApp, h } from 'vue'
import './styles/tailwind.css'
import TodoView from './TodoView.vue'

export { TodoView }
export default TodoView

/** 独立预览挂载（本地调试用；宿主远程加载只走上面的具名导出，不会调用这里）。 */
if (typeof document !== 'undefined' && document.getElementById('todo-tracker-preview')) {
  createApp({ render: () => h(TodoView) }).mount('#todo-tracker-preview')
}
