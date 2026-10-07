# 插件前端开发 SOP（spec 010 插件界面远程加载）

> 适用：为一个后端插件（`Plugins/<Dir>/`）增加独立 Web 界面（三栏 / 配置页 / 仪表盘等），
> 让它由宿主前端**远程加载**、**共享宿主的 Vue/Element Plus 单实例**，不把依赖打进插件、不产生双 Vue 实例。
> 机制背景见 `specs/010-plugin-frontend-runtime/`（design / plan / contracts）。

## 一、架构一分钟

1. 宿主把真实 `vue` / `element-plus` / `pinia` / `vue-router` 挂到 `window.__FORGE_SHARED__`（`src/shared/exposeSharedDeps.ts`，`main.ts` 顶部调用，**早于任何插件界面加载**）。
2. `public/shared/{vue,vue-router,pinia,element-plus}.js` 为 shim，从全局**具名再导出**（ESM 不支持动态 `export * from window.xxx`，必须枚举）。
3. `index.html` 注入 `<script type="importmap">`，把裸模块名映射到上述 shim URL。
4. 插件 `web/` 用 Vite lib 模式构建，把上述 4 个依赖设为 `external`，产物保留裸导入 → 浏览器解析到宿主同一实例。
5. 宿主 `PluginFrontendFileMiddleware` 服务 `/plugins/<id>/web/dist/**`；前端 `pluginViewLoader` 按清单 `entry`（形如 `web/dist/index.js`）远程加载并注入同目录 `style.css`。

> 双 Vue 实例陷阱：插件若打包自己的 Vue，`ref`/`reactive` 响应式与组件通信全部失效 —— 必须靠 external + import map 共享。

## 二、开发步骤

1. **目录**：在 `Plugins/<Dir>/web/` 建标准 Vite 工程（直接 copy `Plugins/AIAgent/web/` 作模板）。
2. **依赖**：`package.json` 把 `vue` / `element-plus` / `pinia` / `vue-router` 列为 `dependencies`（仅供类型与构建解析，**运行时由宿主提供**）。
3. **external**：`vite.config.ts` 的 `build.lib` + `rollupOptions.external` 把上述 4 个设为 external；`formats: ['es']`、`fileName: 'index'`。
4. **组件写法**：**禁止在模板用 `<ElXxx>`**（插件是独立预编译产物，宿主的 `unplugin-vue-components` 不处理它，运行期必解析失败）。一律原生 HTML + CSS，用 EP 的 `--el-*` CSS 变量保持视觉一致。程序化 API（`ElMessage`/`ElMessageBox` 等）走宿主注入的全局。
5. **样式**：Vite lib 模式 CSS 不会被产物 JS 引用，配 `assetFileNames: 'style[extname]'` 固定输出 `style.css`，由宿主加载器按同目录注入 `<link>`（幂等，`data-plugin-style` 去重）。
6. **入口导出**：入口模块 `export default` 或命名导出组件，导出名须与 `plugin.json` 的 `frontend.views[0]` 一致（如 `AiAgentView`）。
7. **shim 维护**：宿主升级 `vue`/`element-plus` 版本后，跑 `node scripts/generate-shared-shims.mjs` 重新生成 `public/shared/*.js`（脚本从真实包自动枚举导出，**禁止手改**——漏导出只有运行期才报 "does not provide an export named X"，极难定位）。
8. **构建**：`cd Plugins/<Dir>/web && pnpm i && pnpm run build` → 产物 `dist/index.js` + `dist/style.css`。
9. **注册**：在 `plugin.json` 加
   ```json
   "frontend": { "route": "/<id>", "entry": "web/dist/index.js", "views": ["<ViewName>"], "menu": "...", "icon": "..." }
   ```
   字段用 camelCase（与既有插件一致）。
10. **宿主拷贝 dist**：`ForgeSelf.Api.csproj` 须声明
    `<Content Include="Plugins\**\web\dist\**" CopyToOutputDirectory="PreserveNewest" />`
    并 `Remove` `Plugins\**\web\src\**` 与 `Plugins\**\web\node_modules\**`（否则源码/依赖被误复制进输出与发布产物）。

## 三、路由

- 插件 `frontend.route` **直接作为最终路径**（如 `/ai-agent`），与宿主静态路由冲突时宿主自动回退 `/plugin-view/<id>`，插件无需关心。
- **切勿在宿主 `router/index.ts` 手写插件页静态路由**（已废弃：`AgentView.vue` 已迁出，改为清单驱动远程加载）。

## 四、验证

- **单元**：`src/utils/__tests__/pluginViewLoader.test.ts` 钉住加载器解析逻辑。
- **端到端**：`ForgeSelf.Web/e2e/plugin-remote-view.spec.ts` 对接真实 publish 宿主，断言版本徽标 / 入口脚本 / 静态资源 200、真实 LLM 对话气泡渲染。**版本断言须与 `plugin.json` 同步**（硬编码，升版本后改文件）。
- **发布态验证**：走 `.agents/skills/plugin-publish-verify/` —— 全量发布 → 起 `publish/ForgeSelf.exe --console` → 只发插件 → 走版本化侧载 `POST /api/plugin/update/{id}` 热换载（不重启宿主）。
- **开发态快速回路**：**环境一律 `pwsh scripts/dev-stack.ps1` 起**（plugin-development 铁律 20，禁止手敲 `dotnet` / `vite` 运行命令）——dev 宿主（`FORGESELF_DEV_MODE=1` + `--plugins-dir <repo>/Plugins`，脚本已保证 `--console` 在首参）+ 宿主前端 dev server，**端口被占用自动顺延**，实际端口与令牌写 `.temp/dev-stack.json`。改 UI 后 `pnpm run build`（或 `pnpm run dev` watch 构建）→ 内容指纹 `?v=` 自动破缓存，刷新页面即见新界面；`FORGESELF_DEV_WEB_SRC=1`（随总闸默认开）下 web 资源一律 no-store 双保险。插件前端要真 HMR 用 `scripts/dev-plugin-web.ps1`。

## 五、反模式（踩过的坑，禁止）

| ❌ 反模式 | 后果 |
|---|--------|
| 把宿主共享 DLL（`XCode.dll` / `NewLife.*.dll` / `ForgeSelf.*.dll`）拷进插件目录 | 类型标识分裂（`IPlugin` 判等失败）+ ALC 卸载中加载 → **宿主启动即崩** |
| 插件打包自己的 Vue | 双实例，响应式全部失效 |
| 插件模板写 `<ElXxx>` | 运行期组件解析失败（白屏） |
| 手改 `public/shared/*.js` shim | 漏导出，运行期 "does not provide an export named X" |
| 改 C# 后想靠覆盖 `<Dir>.dll` 热更 | 入口 DLL 被 ALC 独占锁，覆盖无效（需停宿主或 side-by-side 版本） |
| 先写 `plugin.json` 再拷 DLL | 拷贝中途触发重载，后续 DLL 拷贝报 "Could not find file" —— **payload 先、`plugin.json` 最后** |

## 六、相关文档

- 机制设计：`specs/010-plugin-frontend-runtime/design/`、`.../contracts/`
- 发布与热更新验证：`.agents/skills/plugin-publish-verify/SKILL.md` 及 `references/`
- 后端资源服务：`ForgeSelf.Api/Plugins/Services/PluginFrontendFileMiddleware.cs`
- 前端加载器：`ForgeSelf.Web/src/utils/pluginViewLoader.ts`
