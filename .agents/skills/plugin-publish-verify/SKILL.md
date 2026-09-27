---
name: plugin-publish-verify
description: 宿主从 publish 运行的「插件发布 + 版本化显式更新验证」闭环（发布带版本号）。用于全量发布后启动 publish 宿主实例、只发单个插件（stage 版本快照 + POST /api/plugin/update 显式切换 current 指针）、验证宿主能访问更新后的插件版本。当用户要求「发插件」「验证插件更新」「只发布插件不重启宿主」「跑插件发布流程」时使用。
---

# 插件发布与验证（publish 宿主 · 单插件版本化显式更新）

## 何时用

- 全量发布后，要启动 `publish/` 宿主并验证
- 只改了一个插件，想**不重启宿主**就让它生效并验证
- 要确认宿主能访问更新后的插件（版本 / 清单 / 静态资源 / 版本化布局）

## 更新路径：主路径 vs 兜底

| | 方式 | 地位 |
|---|------|------|
| **主路径** | `run-plugin-publish-verify.ps1`：stage 到 `_backups/<id>/<version>/` → **`POST /api/plugin/update/{id}` 显式版本化切换**（宿主落 `versions/<version>/` + 切 current 指针 + ALC 换载） | **推荐**，日常就用这个（2026-09-24 起）；发布动作不触碰正在加载文件 |
| **兜底** | 界面安装（`.forgeself-plugin` 包走 `POST /api/plugin/install`）/ 冷启动宿主 | 新增插件 / 包分发；PluginHotReloadWatcher 已**一刀切移除**（2026-09-24 用户拍板，宿主无自动热重载） |

**结论：发布/升级走版本化显式更新**——新版本落 `versions/<new>/` 永不覆盖在加载文件，
DLL 锁与热重载竞态从机制上消除（详见 troubleshooting 坑 6）。

## 铁律（先记住这 4 条）

1. **宿主必须从 `publish/` 运行**，`publish/ForgeSelf.exe --console`。
   已运行实例**必须是 publish 里的**；不是就杀掉再起 publish 的实例。
   （publish 实例 = Production → 数据根 `~/.forgeself`；dev = `程序目录/Data`。两者数据不互通。）
2. **活动插件目录只放「插件自己的程序集」**：
   `publish/Plugins/<Dir>/` = `<Dir>.dll` + `plugin.json` [+ `web/dist`]（版本化后根扁平为回退，实际生效在 `versions/<current>/`）。
   **绝不能**把 `XCode.dll` / `NewLife.Core.dll` / `ForgeSelf.*.dll` 等**宿主共享 DLL** 拷进去——
   会导致类型标识分裂（`IPlugin` 判等失败）+ ALC 卸载中加载，**宿主启动即崩**（实测踩坑）。
3. **版本化布局**：更新成功 = `versions/<新版本>/` 目录存在 + `current` 指针 == 新版本 +
   接口返回版本 == 清单版本；静态资源从 `versions/<current>/web/dist/` 读取（Middleware 已版本化）。
4. **更新成功 = 接口返回的该插件版本 == 当前 plugin.json（SyncActiveManifest 同步）版本 == current 指针**（详见 verification.md）。

## 一键跑

```powershell
pwsh .agents/skills/plugin-publish-verify/scripts/run-plugin-publish-verify.ps1 -Plugin AIAgent
```

常用参数：`-Plugin <PascalCase目录名>`（必填，每次跑自动 patch+1）、`-SkipPublish`（跳过 build.ps1 全量发布，宿主产物已最新）、`-NoHostStart`、`-Force`（覆盖已存在的 staged 版本）、`-Engine <Engine名>`（插件带独立引擎时一并发布）。

## 流程骨架

| 步 | 动作 | 关键命令 / 路径 |
|----|------|-----------------|
| 1 | 全量发布 | `.\build.ps1` → `publish/`（⚠ 宿主在跑会锁住 ForgeSelf.dll 被 robocopy 静默跳过，换宿主二进制前先停宿主） |
| 2 | 起 publish 宿主 | `publish\ForgeSelf.exe --console`（必须是 publish 实例，否则杀掉重起） |
| 3 | 等就绪 | `GET /api/plugin` 返回 200 |
| 4 | 只发插件（stage 版本快照） | `scripts\publish-plugin.ps1 -Plugin X -PluginsRoot <repo>\publish\Plugins` → `_backups/<id>/<version>/` |
| 5 | 显式版本化更新 | `POST /api/plugin/update/{id}`（宿主 stage 到 `versions/<version>/` + 切 current + ALC 换载）；断言 `versions/<version>/` 存在 + current 指针对齐 |
| 6 | 验证 | current/清单/API 版本三对齐 / 版本快照入口 DLL hash / 前端清单 / 静态资源 200（内容来自版本快照） |

> 第 5 步是**唯一发布动作**（显式接口），不再覆盖活动目录、不依赖 watcher。

细节见：
- 完整流程与命令：[references/workflow.md](references/workflow.md)
- 验证清单与端点：[references/verification.md](references/verification.md)
- 已知缺陷与坑：[references/troubleshooting.md](references/troubleshooting.md)

## 走查与产物验证标准步骤（2026-09-24 起内置，禁止每次现写探针）

**背景**：宿主每次重启轮换 `ApiToken` + `init-token` 一次性 → 浏览器旧 token 立即失效；
DLL 字符串是 UTF-16LE（strings/grep 漏检）。这两件事历史上反复手工（现写解密探针 +
手工注入 + 现写字节探针），2026-09-24 已固化为仓内工具与 e2e 步骤，**不要再发明**：

| 场景 | 正规入口 | 说明 |
|------|---------|------|
| 插件页走查（推荐） | `node ForgeSelf.Web/node_modules/@playwright/test/cli.js test --config=playwright.config.ts e2e/plugin-store.spec.ts --output=<空目录>` | globalSetup 自动构建宿主→起隔离实例→解密真实 token→注入 localStorage；断言版本徽标/启用标签/卡片渲染 + 截图存档（`screenshots/e2e/plugin-store/plugin-store-walkthrough.png`）。**必须给 `--output=<空目录>`**（防 safe-delete 拦截器） |
| 运行态宿主（51888）手工通道 | `node scripts/get-forge-token.cjs` → 拿明文 token → 注入 `localStorage['forge_api_token']` → 导航 | 默认读 `%USERPROFILE%\.forgeself\Config\ForgeSetting.config`（当前运行实例真源）；`--config <path>` / `FORGE_SETTING_CONFIG` 可覆盖 |
| DLL 字符串验证（发布产物） | `node scripts/probe-dll-string.cjs <dll> <目标串> [--expect-absent]` | UTF-8 + UTF-16LE 双检；`--expect-absent` 用于验证「已删除实现不在产物」；FOUND→exit 0 / ABSENT→exit 1（expect-absent 反转） |
| e2e 全自动 | 直接写/跑 `e2e/plugins/<id>/<id>.spec.ts` | `e2e/helpers/real-auth.ts` 的 `injectRealApiKey` 已内置解密+注入 |

**铁律**：走查 token 一律来自上述入口；验证产物一律用 `probe-dll-string.cjs`。
发现新场景缺正规入口 → 先补工具/用例再走，不现写一次性脚本（AGENTS §5.0）。

## 前端改动后的发布必做（2026-09-24）

插件管理/宿主前端（`ForgeSelf.Web/src`）改动后，**dev/e2e 通过 ≠ 发布态生效**：
必须 `.\build.ps1` 全量重建 wwwroot → 停宿主 → 起宿主 → 浏览器走查 `http://localhost:51888/plugins`。
PluginController 鉴权（2026-09-24）后，前端必须走 `authFetch`（src/services/authFetch.ts）带 token，
否则插件管理页/远程视图 401 空态（曾误判为环境问题）。
## 关键事实速查

- 端点前缀是**单数** `api/plugin/...`（`[Route("api/[controller]")]`，控制器 `PluginController`）。
  `publish-plugin.ps1` 结尾打印的 `/api/plugins/...` 是**错的**，会 404。
- 插件目录：`AppContext.BaseDirectory/Plugins` → publish 实例即 `publish/Plugins`。
- 两层命名：目录/程序集 PascalCase（`AIAgent`），运行时 id kebab-case（`ai-agent`）。
  `-Plugin` 传**目录名**，`_backups` 下用 **id**。
- 默认端口 7102（`ForgeSetting.config` 的 `PortNumber`），本环境长期 publish 实例用 **51888**。
- **前端资源版本化读取（2026-09-24 已修复）**：`PluginFrontendFileMiddleware` 从
  `versions/<current>/web/dist/` 读取静态资源（current 指针存在且版本 web 存在时），
  `ComputeWebVersion` 指纹同样基于版本快照 → 版本切换后 `?v=` 变化、浏览器拉新版。
  核验：`curl http://localhost:<port>/plugins/<id>/web/dist/index.js`，比对**字节数**与版本快照一致（可用探针标记区分根扁平）。
- **跑 e2e 必须给 `--output`**：`node ForgeSelf.Web/node_modules/@playwright/test/cli.js test --config=playwright.config.ts <spec> --output=<空目录>`；
  否则 Playwright 启动前清理 `test-results` 会被沙箱 safe-delete 拦截器拦下，整跑失败。
- **存量迁移脚本**：scripts/migrate-plugin-versions.ps1（幂等）把扁平插件目录迁移为版本化布局（ersions/<ver>/ + current）；build 覆盖 publish/Plugins 后可重复跑。
- **发布顺序铁律（2026-09-24 实测）**：宿主运行期间 uild.ps1 会因 exe 被锁**静默跳过更新**（exe 时间戳不动、行为仍旧）。改宿主后端必须 **先停宿主 → build.ps1 → 起宿主** 再验证。
- **PowerShell 相关**：本环境 `pwsh` 是 `...\WindowsApps\pwsh.exe` 残桩（静默不执行），可用的是 **PS 5.1**；
  且 PowerShell 工具可能**不回显 stdout** —— 跑脚本时务必 `*> <log>` 重定向后读日志，
  否则会误判"没跑"而重复执行（曾因此重跑发布脚本，实际第一次已成功）。
