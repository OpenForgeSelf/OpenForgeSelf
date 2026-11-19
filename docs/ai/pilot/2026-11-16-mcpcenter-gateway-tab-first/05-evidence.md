# 05-evidence：McpCenter「网关配置」调为第一个页签且默认激活

> 分级：**Verified**（亲自取得输出）/ **Inferred**（凭证据推断）/ **Unknown**（未验证）。日志均在 `<repo>\.temp\`（本批前缀 `mcp-tab-*`），不入库但结论内联。

## E1 实现落点 — Verified

- `Plugins/McpCenter/web/src/McpCenterView.vue`：①页签块换位（`<ElTabs>` 内网关配置 `<ElTabPane name="gateway">` 移至工具管理之前，单次原子编辑）；②`activeTab` 初值 `'tools'` → `'gateway'`；③dsh 写入状态 watch 加第三参 `{ immediate: true }`（网关配置成默认页签后原「首次切换才拉」永不触发 → 挂载即拉）。
- `Plugins/McpCenter/plugin.json`：Version `2.9.0` → `2.9.1`，Description 追加 v2.9.1 说明句。
- 后端零触碰：`git status` 仅 5 个改动文件（McpCenterView.vue / plugin.json / 3 个 e2e spec），无 `.cs`、无宿主文件（Inferred 自 stash pop 时 git 输出，文件清单逐条核对）。

## E2 插件前端门禁 — Verified

- `pnpm run check`（vue-tsc）：无错误输出（`.temp/mcp-tab-check.log`）。
- `pnpm run test`：**23/23 passed**（schemaForm.test.ts，v3.2.6，`.temp/mcp-tab-test.log`）。
- `pnpm run build`：51 modules，`dist/index.js` 75.21 kB（`.temp/mcp-tab-build.log`）。

## E3 e2e 先红后绿（AC1/AC2 断言非自洽实证）— Verified ✦

- **红**：`git stash push -- McpCenterView.vue`（仅回退实现，保留新断言与 2.9.1 清单）→ 旧实现重建 dist → 单跑 UI 渲染用例 → **失败于新断言**：`Expected substring: "网关配置" / Received string: "工具管理"`（`mcp-center.spec.ts:382`，`.temp/mcp-tab-red-e2e.log`）。环境链同轮全通（宿主构建+签名+启动、端点 evidence 行、徽标 v2.9.1），红因唯一 = 页签顺序，排除假红。
- **绿**：`git stash pop` → 重建 dist → `mcp-center.spec.ts` 全套 **9 passed (1.4m)**（`.temp/mcp-tab-green-e2e.log`），含新断言（第一页签=网关配置 + `is-active` 默认激活 + 挂载即见 `.gateway-status-cards`）与全部回归（外部三传输、CORS 保存/恢复、无状态 A/B、执行超时、list_tools 枚举）。

## E4 宿主侧回归 — Verified

`e2e/mcp-tools.spec.ts`（helper 增加显式切「工具管理」页签）→ **4 passed (58.0s)**（`.temp/mcp-tab-toolsspec-e2e.log`）。

## E5 设计守卫 — Verified

`pnpm run design:check`：style-tokens 硬编码色值 1761（基线 2501，较基线少 740）+ design-compliance 四计数与基线持平 → **PASS 无新增违例**（`.temp/mcp-tab-designcheck.log`）。

## E6 起源码走查（门禁③载体 = live 走查 spec）— Verified

- `dev-stack.ps1` 起源码前后端：后端 :7301（PID 90356）/ 前端 :7399（PID 34900），插件源 = `Plugins/`（dev-stack.json）。
- `mcp-center.live.spec.ts` 对 :7399 → **1 passed (2.0s)**：徽标 `[badge] v2.9.1`、工具计数 ForgeSelf Local=171、**console-errors 0**（`.temp/mcp-tab-live-walkthrough.log`）；走查后 `dev-stack.ps1 -Stop` 释放（exit 0）。全程未触碰 :51888 宿主。

## E7 截图读图（Level 3 视觉核对）— Verified

四张图逐一读图：
- e2e `screenshots/e2e/mcp-center/gateway-tab.png`：页签头「网关配置（激活）｜工具管理」顺序正确，v2.9.1 徽标 + 地址 chip；
- e2e `tools-tab.png`：切「工具管理」后工具表（execute_query/list_tables）正常；
- 走查 `screenshots/live-dev/mcp-center-gateway.png`：**无任何点击的默认态** = 网关配置页签激活 + 3 状态卡（地址/正在监听/令牌）+ 修改表单完整；
- 走查 `mcp-center-walkthrough.png`：工具管理页签激活后 ForgeSelf Local 工具表 171 工具渲染正常。
核对项：页签头顺序、激活态下划线、卡片对齐、无截断/遮挡/溢出/破图 — 全过。

## E8 发布（门禁④本地目录更新源）— Verified

- 命令：`release-local.ps1 -UpdateDir <仓库父目录>\updates -Sign`（即历史惯例 `..\updates`；§5.0 前置：TEMP→.temp；水位 13.29 GB > 2.5 GB 门）。
- 日志：`build-frontend: all done` → `Authenticode signing ok (8.1s)` → `UpdateDir SHA256 verification PASS (1 file(s) rechecked)` → **`ALL DONE in 93s`**（`.temp/mcp-tab-release.log`）。
- 产物：`OpenForgeSelf-2.4.22.2611161154-win-x64.zip`（183.0 MB）+ RELEASE-NOTES + SHA256SUMS.txt 落 updates 目录。
- 包内探针（ZipFile 直读）：`versions/2.4.22.2611161154/plugins/McpCenter/plugin.json` → **`"Version": "2.9.1"`**。

## E9 门禁⑤（运行实例只读复验）— Unknown（依赖用户）

:51888 实例尚未更新到含 McpCenter 2.9.1 的包（`2.4.22.2611161154`）。待用户在页面「检查更新 → 下载 → 重启并更新」后做只读复验（token → 版本徽标 → 默认页签走查）。本批不代做、不停用户宿主。
