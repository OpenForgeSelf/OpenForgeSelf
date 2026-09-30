---
name: plugin-publish-verify
description: 宿主/插件「发布 + 验证」闭环（2026-09-27 起主路径 = 打 tag 自动发布 + 页面自动更新）。用于「发插件」「发布新版本」「验证更新生效」「只发插件不重启宿主」「跑发布流程」。发布动作交给 tag→CI→GitHub Release（或本地 release-local.ps1 打包 + 页面自动更新）；**禁止 agent 停/启/杀用户运行中的宿主进程**；开发期验证走 e2e 隔离实例；确需在运行实例侧载插件（版本化更新，不重启宿主）须先获用户同意。
---

# 发布与验证（2026-09-27 新规范：打 tag 自动发布 + 页面自动更新）

## 何时用

- 完成了一个宿主/插件功能，要把新版本交付出去
- 要验证「打 tag 后 CI 打包的 Release 资产」能被页面自动更新链路正确检查/下载
- 要确认发布产物（zip / 版本号 / SHA256SUMS）正确

## 发布路径：主路径 vs 可选

| | 方式 | 地位 |
|---|------|------|
| **主路径（推荐）** | **打 tag 自动发布**：`git tag -a v<X.Y.Z> -m "..."` → `git push github v<X.Y.Z>` → CI（`.github/workflows/release.yml`）自动构建打包并创建 GitHub Release → 用户/页面在「设置-版本更新」点「检查更新 → 下载 → 重启并更新」完成升级 | 一切交付的默认路径；宿主由 update-agent 自更新，**无人停宿主** |
| **本地离线发布** | `pwsh scripts/release/release-local.ps1 -Version v<X.Y.Z> -UpdateDir <目录>` → 设置页「更新源 = 本地目录」填该目录 → 页面点「检查更新 → 下载 → 重启并更新」 | 内网/离线/不想推 GitHub 时；等价于主路径的本地版 |
| **开发期侧载（可选）** | `run-plugin-publish-verify.ps1 -Plugin <X>`（stage 版本快照 → `POST /api/plugin/update/{id}` 版本化切换，宿主不重启） | 仅插件自身 DLL 变更且**用户同意触碰运行实例**时；宿主二进制（ForgeSelf.Api/Web）变更一律走 tag 发布，不走此路径 |

## 铁律（先记住这 4 条）

1. **禁止 agent 停/启/杀任何用户运行中的宿主进程**（含 `D:\src\tools\ForgeSelf`、`:51888` publish 实例、托盘实例等）。
   发布 = 打 tag 自动发布 + 页面自动更新（update-agent 换文件并重启宿主，属宿主自更新）。
   历史教训：旧流程「确保宿主从 publish/ 运行，非 publish 实例杀掉重起」曾误杀用户运行实例，被用户明确叫停。
2. **活动插件目录只放「插件自己的程序集」**：`<Dir>.dll` + `plugin.json` [+ `web/dist`]（版本化后根扁平为回退，实际生效在 `versions/<current>/`）。目录结构与备份生命周期真源 = `docs/04-standards/packaging-upgrade-backup.md`（目标去 `_backups`/插件备份，§4-R4）。
   绝不能把 `XCode.dll` / `NewLife.Core.dll` / `ForgeSelf.*.dll` 等**宿主共享 DLL** 拷进去——宿主启动即崩（实测踩坑）。
3. **验证分两段，都不许省**：
   - **开发期 = e2e 隔离实例**：`playwright.config.ts` globalSetup 自动构建宿主 → 起隔离实例（`FORGESELF_INSTANCE_ID`）→ 解密真实 token → 跑用例 → 回收。**不碰用户运行实例**。
   - **交付后 = 运行实例只读复验**：用户启用/更新到新版本之后，agent **必须**在运行实例（如 `:51888`）做一次**只读**走查（见下「运行实例只读复验」）。铁律1 禁的是停/启/杀与不可逆写，**不含只读走查**——别把它读成"别去看"（2026-09-28 我就因此漏走查，被用户指出）。
4. **发布产物核对**：打 tag 后等 CI 完成，用资产 API 直链下载 zip 核对 SHA256（`package-release.ps1` 写 `SHA256SUMS.txt`）；页面自动更新链路已由 spec 036 单测 + e2e 覆盖。

## 一键跑（本地打包，可选）

```powershell
pwsh scripts/release/release-local.ps1 -Version v0.2.5 -Sign -UpdateDir D:\updates
```

常用参数：`-Version v<X.Y.Z>`（打 tag 时的版本，缺省 `0.0.0-local`）、`-UpdateDir <目录>`（拷贝 zip+SHA256SUMS+更新说明到本地更新目录）、`-SkipFrontend`（复用已有 web dist 快速迭代）、`-FrameworkDependent`、`-Sign`。**`-Sign` 可选（输入2 起默认不签，需要签名时才传；输入42 曾立「必带」已废止）**：Authenticode 签名走 `scripts/sign-publish.ps1`（自签证书自动生成/复用 + DigiCert 时间戳；商业证书 `-PfxPath/-PfxPassword` 可插拔），签名在 zip 打包前，递归签顶层根启动器 + `versions/<ver>/` 全部 exe。**CI 流水线默认不传 `-Sign`**（release.yml），签名策略真源 = `docs/04-standards/packaging-upgrade-backup.md` §1.1。

## 流程骨架（主路径）

| 步 | 动作 | 关键命令 / 路径 |
|----|------|-----------------|
| 1 | 门禁 | 前端 `pnpm run check && pnpm run test`；后端 `dotnet build && dotnet test`；插件层 e2e 绿 |
| 2 | 汇总变更，向用户请示 git 提交（用户审批后才 commit） | 见 AGENTS.md §10 / 用户偏好（同一任务一次性提交） |
| 3 | 打 tag 自动发布 | `git tag -a v<X.Y.Z> -m "发版说明"` → `git push github v<X.Y.Z>` → CI 自动打包 → GitHub Release（OpenForgeSelf/OpenForgeSelf，公开仓库） |
| 4 | 通知用户页面自动更新 | 用户（或用户明确同意后代点）在设置-版本更新页「检查更新 → 下载 → 重启并更新」；宿主 update-agent 换文件并重启，全程无人停宿主 |
| 5 | 验证发布产物 | `gh release view v<X.Y.Z>` + 下载 zip 核对 SHA256SUMS；更新检查/下载链路由单测 + e2e 覆盖 |

> 第 3 步的 tag 是**唯一发布动作**。本地迭代若不想推 GitHub，用本地离线发布（`-UpdateDir` + 页面本地目录更新源）。

细节见：
- 发布治理硬规则与坑：[`docs/04-standards/agent-workflow.md`](../../../docs/04-standards/agent-workflow.md) B10
- 版本化布局说明：[`docs/02-features/035-plugin-versioned-layout.md`](../../../docs/02-features/035-plugin-versioned-layout.md)
- 自动更新链路（spec 036）：`ForgeSelf.Api/Services/StagedUpdateService.cs`、`UpdateChecker.cs`、`UpdateController.cs`

## 走查与产物验证标准步骤（2026-09-24 起内置，禁止每次现写探针）

**背景**：宿主每次重启轮换 `ApiToken` + `init-token` 一次性 → 浏览器旧 token 立即失效；
DLL 字符串是 UTF-16LE（strings/grep 漏检）。这两件事历史上反复手工（现写解密探针 +
手工注入 + 现写字节探针），2026-09-24 已固化为仓内工具与 e2e 步骤，**不要再发明**：

| 场景 | 正规入口 | 说明 |
|------|---------|------|
| 插件页走查（推荐） | `node ForgeSelf.Web/node_modules/@playwright/test/cli.js test --config=playwright.config.ts e2e/plugin-store.spec.ts --output=<空目录>` | globalSetup 自动构建宿主→起隔离实例→解密真实 token→注入 localStorage；断言版本徽标/启用标签/卡片渲染 + 截图存档（`screenshots/e2e/plugin-store/plugin-store-walkthrough.png`）。**必须给 `--output=<空目录>`**（防 safe-delete 拦截器） |
| 运行态宿主（51888）手工通道 | `node scripts/get-forge-token.cjs` → 拿明文 token → 注入 `localStorage['forge_api_token']` → 导航 | 默认读 `%USERPROFILE%\.forgeself\Config\ForgeSetting.config`（当前运行实例真源）；`--config <path>` / `FORGE_SETTING_CONFIG` 可覆盖。⚠ 触碰运行态宿主前必须获用户同意；新规范优先用 e2e 隔离实例走查 |
| DLL 字符串验证（发布产物） | `node scripts/probe-dll-string.cjs <dll> <目标串> [--expect-absent]` | UTF-8 + UTF-16LE 双检；`--expect-absent` 用于验证「已删除实现不在产物」；FOUND→exit 0 / ABSENT→exit 1（expect-absent 反转） |
| e2e 全自动 | 直接写/跑 `e2e/plugins/<id>/<id>.spec.ts` | `e2e/helpers/real-auth.ts` 的 `injectRealApiKey` 已内置解密+注入 |

**铁律**：走查 token 一律来自上述入口；验证产物一律用 `probe-dll-string.cjs`。
发现新场景缺正规入口 → 先补工具/用例再走，不现写一次性脚本（AGENTS §5.0）。

## 运行实例只读复验（交付后必做，2026-09-28 补）

**触发**：用户已在运行实例（如 `:51888`）启用/更新到新版本之后 —— 这一步**不属于**开发期 e2e 的替代，
而是「发布/侧载 → 生效」之间的**闭环确认**。历史漏点：AGENTS §0 门禁第④步把「走查」写在 e2e 隔离实例上，
我据此认为跑完 e2e 就算走完，用户启用 1.1.1 后我没有去运行实例复验（被用户当场指出）。

**步骤（全部只读）**：

0. **先验 token 有效性，再谈结论**（不可跳过）：注入前用 `node scripts/get-forge-token.cjs` 取当前配置解出的明文，
   在页面里打一个**需要鉴权的只读端点**（如 `GET /api/plugin/detail/<id>`）确认 **200**；
   出现 **401** 就**立即停下上报**，绝不把「页面空白 / 主区没渲染」当成插件缺陷。
   实测坑：宿主每次重启轮换 `ApiToken`，浏览器里存的旧 token 会静默 401，页面只剩导航条（`body` 只有几十字节），
   极易误判成「新版本坏了」。
1. `localStorage.setItem('forge_api_token', <第0步验过的明文>)` → 重新导航到 `plugin.json.frontend.route`（如 `/file-tools`）。
2. **核版本徽标**：`.view-title` 旁应显示 `v<新版本号>`（铁律13 的产物，正好当"跑的是哪一版"的自证）。
3. **走一条主链路**：填真实输入 → 点主操作 → 采**中间态**一帧（`Running`/进度）→ 等终态。
   中间态必须看数值自洽（占比 ≤100、合计闭合、无守恒告警）—— 1.1.1 修的就是只在中间态暴露的分母缺陷。
4. **截图存档并读图**：存 `ForgeSelf.Web/screenshots/live-<端口>/<插件id>-<版本>-<场景>.png`，按 `e2e-testing` Level 3 清单逐项核对。
5. 需要独立基准时，用 `find <目录> -type f -printf '%s\n' | awk '{s+=$1} END{print s}'` 之类**只读**测量对账，别信界面自说自话。

**边界（越界即违规）**：不点不可逆 / 无 UI 回退的动作（删除、清空、覆盖配置）—— 需要验证写路径就在**隔离实例**做；
不启停宿主；不调 `POST /api/plugin/update/{id}` 等改运行实例状态的端点，除非用户明确要求；
截图/日志之外的任何文件都不写进用户安装目录。

## 前端改动后的发布必做（2026-09-27 新规范）

插件管理/宿主前端（`ForgeSelf.Web/src`）改动后，**dev/e2e 通过 ≠ 发布态生效**：
必须走发布主路径——打 tag → CI 自动 `build-frontend.ps1` 重建 wwwroot + 插件 dist → 打包进 zip → 页面自动更新。
本地验证走 e2e 隔离实例（globalSetup 自动构建宿主，不碰用户运行实例）。
PluginController 鉴权后，前端必须走 `authFetch`（src/services/authFetch.ts）带 token，
否则插件管理页/远程视图 401 空态（曾误判为环境问题）。
## 关键事实速查

- 端点前缀是**单数** `api/plugin/...`（`[Route("api/[controller]")]`，控制器 `PluginController`）。
  `publish-plugin.ps1` 结尾打印的 `/api/plugins/...` 是**错的**，会 404。
- 插件目录：`AppContext.BaseDirectory/plugins` → publish 实例即 `publish/plugins`。
- 两层命名：目录/程序集 PascalCase（`AIAgent`），运行时 id kebab-case（`ai-agent`）。
  `-Plugin` 传**目录名**，`_backups` 下用 **id**。
- 默认端口 7102（`ForgeSetting.config` 的 `PortNumber`），本环境长期 publish 实例用 **51888**。
- **前端资源版本化读取（2026-09-24 已修复）**：`PluginFrontendFileMiddleware` 从
  `versions/<current>/web/dist/` 读取静态资源（current 指针存在且版本 web 存在时），
  `ComputeWebVersion` 指纹同样基于版本快照 → 版本切换后 `?v=` 变化、浏览器拉新版。
  核验：`curl http://localhost:<port>/plugins/<id>/web/dist/index.js`，比对**字节数**与版本快照一致（可用探针标记区分根扁平）。
- **跑 e2e 必须给 `--output`**：`node ForgeSelf.Web/node_modules/@playwright/test/cli.js test --config=playwright.config.ts <spec> --output=<空目录>`；
  否则 Playwright 启动前清理 `test-results` 会被沙箱 safe-delete 拦截器拦下，整跑失败。
- **存量迁移脚本**：scripts/migrate-plugin-versions.ps1（幂等）把扁平插件目录迁移为版本化布局（`versions/<ver>/` + current）；build 覆盖 publish/plugins 后可重复跑。
- **宿主二进制变更发布顺序铁律（2026-09-27 新规范）**：改宿主后端/前端后**不要手动停宿主去覆盖二进制**——
  一律走打 tag 自动发布（或本地 `release-local.ps1 -UpdateDir` + 页面本地目录更新源），宿主由 update-agent 自更新。
  旧教训（2026-09-24）：`build.ps1` 覆盖 publish/ 时运行中宿主锁住 `ForgeSelf.dll` 被静默跳过、脚本仍报成功；
  这条在 tag 发布路径下由 CI 全量重建天然规避。
- **PowerShell 相关**：本环境 `pwsh` 是 `...\WindowsApps\pwsh.exe` 残桩（静默不执行），可用的是 **PS 5.1**；
  且 PowerShell 工具可能**不回显 stdout** —— 跑脚本时务必 `*> <log>` 重定向后读日志，
  否则会误判"没跑"而重复执行（曾因此重跑发布脚本，实际第一次已成功）。
