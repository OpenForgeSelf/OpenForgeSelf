# 05 · Evidence — 证据记录（只记实际发生 · 2026-10-28）

> 来源等级：✅ Verified（亲自跑过，拿到真实输出）/ ⚠️ 部分 / ❌ 阻塞。
> 环境前置（AGENTS §5.0）：`NO_PROXY=localhost,127.0.0.1,::1` + `TEMP=<repo>\.temp\tmp`；`ESBUILD_BINARY_PATH=<repo>\.temp\esbuild\esbuild-0.25.0.exe`（用户输入28 指示把二进制复制进项目内再用环境变量指向）。
> 判定一律读日志正文（AGENTS §2.3：pwsh 重定向块缓冲，exit code 不当证据）。

## 门禁（步1）✅ Verified（含 1 处环境阻塞的修复过程）

### ① 宿主后端编译与过滤集 ✅（带 1 处并行会话 WIP 说明）

| 验证项 | 命令 | 结果 |
|--------|------|------|
| 宿主+测试工程编译 | `dotnet build ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj -v m` | **0 个错误**（337 warnings 存量） |
| 本批过滤集（14 组 + Sems/provider/AIService 追加） | `dotnet test ... --no-build --filter "<19 过滤器>"` | **通过 320 / 失败 4 / 总计 324** |
| 宿主前端类型+lint | `pnpm --dir ForgeSelf.Web run check` | `CHECK_EXIT=0` |
| 宿主前端单测 | `pnpm --dir ForgeSelf.Web run test` | **89 test files passed**，`TEST_EXIT=0` |
| 设计系统基线 | `pnpm --dir ForgeSelf.Web run design:check` | `DESIGNCHECK_EXIT=0` |
| 插件前端构建 | `pnpm --dir Plugins/AIAgent/web run build` / `Plugins/McpCenter/web` | **EXIT=0**（AIAgent 275.26 kB / McpCenter 75.19 kB） |

**4 例失败的定性（⚠️ 非本批）**：全部为 `ForgeSelf.Api.Tests.Unit.OpenAISdkAdapterTests.Anthropic_Type_Chat_Goes_To_OpenAICompatEndpoint` 的 4 个 Theory 数据集。断言期望 `/v1/chat/completions`，实测请求 URI 为 `/v1/messages/chat/completions`。
判据：该文件属并行会话在跑的 `2026-10-27-openai-responses-upstream` 批（`ForgeSelf.Api/Services/AI/Providers/OpenAISdkAdapter.cs` 在 14:38、15:36 两次被写盘），且 `TODO.md` 已登记同域待办「Anthropic 类型出站聊天体未转 Anthropic 原生格式」。⇒ 不属本批域内，**但会随工作树快照进包**（已在放行卡片向用户交代）。

**并行会话导致的二次阻塞（已自愈）**：首轮门禁时宿主后端编译不过（`OpenAISdkAdapter.cs(39)` CS1503 + `AIProviderService.cs(245)` CS0103）。采样确认 `AIProviderService.cs` 14:52:25、`OpenAISdkAdapter.cs` 14:53:15 被并行会话写盘 ⇒ 半成品代码。14:55:03 轮询到 `已成功生成`，本任务等其落定后才继续。**教训：打包是工作树快照 ⇒ 发布必须在并行会话落定后进行。**

### ② 构建链阻塞：esbuild 版本错配（❌ → ✅ 已修）

- **现场**：`pnpm --dir Plugins/AIAgent/web run build` 报
  `Cannot start service: Host version "0.25.0" does not match binary version "0.25.12"`，
  `failed to load config from Plugins/AIAgent/web/vite.config.ts`。
- **根因（逐层取证）**：
  1. 用户级环境变量 `ESBUILD_BINARY_PATH=C:\Users\12504\esbuild-bin\esbuild.exe`，该二进制 `--version` = **0.25.12**；
  2. esbuild 的 `install.js` 会尊重该变量：把 `lib/main.js` 首行改写成 `var ESBUILD_BINARY_PATH = "C:\\Users\\12504\\esbuild-bin\\esbuild.exe";`，并把 `bin/esbuild` 替换成 150 字节 shim（`execFileSync` 指向同一路径）；
  3. `lib/main.js:1605` 是 `process.env.ESBUILD_BINARY_PATH || ESBUILD_BINARY_PATH` ⇒ 机器级变量优先级高于包内值；
  4. 12 个插件前端 pin esbuild **0.25.0**（`@esbuild+win32-x64@0.25.0` 本地平台包自带 0.25.0 二进制，报告版本正确），而原生二进制在 `--service=0.25.0` 时自检版本不符即退出 ⇒ **12 个插件前端构建全挂**；
  5. 宿主 `ForgeSelf.Web` pin 0.25.12 ⇒ 恰好与全局二进制同版 ⇒ 侥幸不受影响（易误判为"只有插件坏了"）。
- **修法（落仓，不做一次性脚本 · AGENTS §5.0）**：`scripts/release/patch-esbuild.mjs` 增加 workaround 2 ——
  ① 定位本 web 目录的 esbuild 包与其本地平台包；② 若本地平台包存在（版本必与 JS 包一致），把 `lib/main.js` 的硬编码路径置空 + 去掉 `process.env` 优先级，让 esbuild 回落到**本地**平台二进制；③ 重生成 `bin/esbuild` shim，运行时用 `require.resolve("@esbuild/<platform>/package.json")` 解析本地二进制；④ 本地平台包缺失时**保留**环境变量覆盖（不破坏无 optional deps 的安装）。
- **验证**：host + AIAgent + McpCenter 三个目录跑 patch 后，插件前端 `vite build` **EXIT=0**；patch 日志逐目录打印 `local binary pinned: esbuild@0.25.0 -> @esbuild/win32-x64/esbuild.exe`。
- **用户指示的补充动作**：两份 esbuild 二进制已复制进项目内 `.temp/esbuild/`（`esbuild-0.25.0.exe` / `esbuild-0.25.12.exe`，`--version` 实测分别为 0.25.0 / 0.25.12），发布链以 `ESBUILD_BINARY_PATH` 指向项目内副本。

### ③ 本插件冒烟 e2e ✅ Verified（零 mock，真实后端）

`node ForgeSelf.Web/node_modules/@playwright/test/cli.js test --config=ForgeSelf.Web/playwright.config.ts e2e/plugins/mcp-center/mcp-center.spec.ts --output=<空目录>`
→ **8 passed (2.0m)**，`E2E_EXIT=0`。覆盖：宿主加载 mcp-center + MCP 端口全链路 + 网关配置 API / 插件自带界面渲染（双 tab + 版本徽标 + 工具表格 + 网关配置）/ 外部 MCP 三传输接入 + universal_tool 转发 / stdio npx 真实世界握手 / 工具测试台 inputSchema 动态表单真实调用 / 外部 MCP UI 主链路（预设新增→连接→测试→编辑回显→删除）/ 执行超时可配置（5s 掐断 / 60s 放行）/ list_tools 枚举含外部工具全名并立即可调。

## 起源码走查（步2）✅ Verified

- **起源码实例**：`pwsh scripts/dev-stack.ps1` → 后端 `http://localhost:7301`（PID 64648）、前端 `http://localhost:7399`（PID 46264）、令牌 `sk-c328e8d234ec29d15fb0877e5e0a6aab`、状态文件 `.temp/dev-stack.json`。
- **令牌有效性先验**（避免把空页当缺陷）：`/api/plugin` 200、`/api/plugin/frontend-manifest` 200、`/api/plugin/detail/mcp-center` 200；不带 token 的 `/api/plugin` → **401** ⇒ 令牌有效。
- **走查驱动**：`playwright.live.config.ts` + `E2E_FRONTEND_URL=http://localhost:7399` + `E2E_API_TOKEN=<dev-stack 明文>`，新增 `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.live.spec.ts`（只读；只渲染 + 截图，不点不可逆动作）。
- **结果**：`1 passed`，**0 控制台错误**。
  - 插件标题「MCP 中心」渲染、**版本徽标 v2.7.0**（与 `Plugins/McpCenter/plugin.json` 一致）；
  - 左栏 MCP 服务器清单：`Database=2 | Filesystem=3 | ForgeSelf Local=131 | GitHub API=3`；
  - 切到「ForgeSelf Local」后工具表 **131 行，其中 116 行工具名带命名空间前缀**（`design_*` / `aiagent_plan_workflow` / `complete_step` / …）；
  - 「网关配置」页签渲染三张状态卡（MCP 服务地址 / 运行状态 正在监听 / 访问令牌）+ 修改网关配置表单（端口 18895 / 地址 127.0.0.1 / 访问令牌 / 允许跨域来源 / 会话模式）。
  - 截图：`ForgeSelf.Web/screenshots/live-dev-7399/mcp-center-walkthrough.png`、`mcp-center-gateway.png`（均已读图核对：无溢出/遮挡/错位；右下角悬浮助手按钮与表格末行「测试」按钮有重叠，属宿主既有固定定位元素，非本批引入）。

### 运行时工具命名空间实证（改名表 ⇄ 实际注册）

数据源：dev-stack 后端日志 `.temp/dev-stack-backend.log`（**GBK 编码**，需按 936 解码，否则中文乱码）。

| 判据 | 结果 |
|------|------|
| `[ToolRegistry] 注册工具成功` 行数 | **127** |
| 工具名去重后 | **127** ⇒ **零重名** ✅ |
| 改名表（`ForgeSelf.Abstractions/ToolNamespace.cs`，81 行）**旧名** ∩ 运行时注册名 | **零命中** ✅（81 个旧名全部退役） |
| 改名表**新名** ∩ 运行时注册名 | **81 / 81 全部落地** ✅ |
| 用户拍板豁免名单 8 个（universal_tool / search_tools / list_tools / request_help / run_terminal_command / read_file / write_file / list_files） | **8/8 仍在** ✅ |
| 前缀分布 | dev 26 · sems 22 · script 11 · design 8 · todo 8 · file 6 · hub 5 · sys 5 · memory 5 · text 5 · workflow 5 · link 4 · schedule 3 · mcp 2 · list 2 · 其余单件 |

## 发布与产物核对（步3 / 步5）✅ Verified

### 步2.5 闸门放行

- 起源码走查证据（地址 + 令牌 + 两张读图后的截图）已交用户过目；用户 **2026-10-28 17:25 明确回复「可以发了，出 2.4.16」**（问卷 ask_86b396dd02f08fda3e5a7262，选中 gate25_opt1）⇒ 放行记录在案。

### 步3 发布链（release-local）

命令：`pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/release/release-local.ps1 -Version 2.4.16 -Sign -UpdateDir D:\src\my-proj\OpenForgeSelf\updates`
（环境前置：`ESBUILD_BINARY_PATH=<repo>\.temp\esbuild\esbuild-0.25.0.exe`、`NO_PROXY=localhost,127.0.0.1,::1`、`TEMP=<repo>\.temp\tmp`）

| 段 | 日志标记 | 结果 |
|----|----------|------|
| 版本推导 | `release-local: Version=2.4.16 -> release=2.4.16.2610281727` | 时间码由脚本补 ✅ |
| 前端 | `==> frontend: host web` → `==> frontend: plugin webs` → `build-frontend: all done.` | 全绿（esbuild workaround 生效）✅ |
| 宿主 | `==> host: dotnet publish (Release, win-x64, selfContained=false, SINGLE-FILE, Version=2.4.16.2610281727)` | ✅ |
| 启动器 | `==> boot: launcher publish` + `==> boot: runtime assembly (DOTNET_ROOT, fxr=10.0.13)` | ✅ |
| 组布局 | `==> package: assemble QQNT layout` → `sanitize output` → `write versions/current` | ✅ |
| 签名 | `==> package: Authenticode signing` → `签名完成：新增 2 / 跳过 1 / 失败 0`、`校验通过：3 / 3` | ✅ |
| 打包 | `==> package: zip + sha256` → `ok (8.8s)` | ✅ |
| 收尾 | `release-local: ALL DONE in 146s`，`RELEASE_EXIT=0` | ✅ |

产物（更新源 `D:\src\my-proj\OpenForgeSelf\updates`）：
- `OpenForgeSelf-2.4.16.2610281727-win-x64.zip`（168,583,016 bytes）
- `SHA256SUMS.txt`
- `RELEASE-NOTES-2.4.16.2610281727.md`（中文无乱码 ✅ —— 用 pwsh 而非 PS 5.1）

### 步5 产物核对

| 判据 | 结果 |
|------|------|
| SHA256 复核 | 实际 `f146956eaf6344f960e32b4780b20aa81600cfa9ba86994ddb0284f2eabfeea3` = 清单记录 ⇒ **MATCH** ✅ |
| 签名有效性 | `artifacts/layout/ForgeSelf.exe` 与 `artifacts/layout/versions/<ver>/ForgeSelf.exe` 均 **Status=Valid**，签名主体 `CN=OpenForgeSelf 铸己匣`，时间戳 `Sectigo Public Time Stamping Signer R37` ✅（`layout-root/`、`publish/` 两处为签名前暂存副本，NotSigned 属预期） |
| **L1** 内置插件随版本走 | `versions/2.4.16.2610281727/plugins/*/plugin.json` = **20 条** ✅ |
| **L2** 无逐代嵌套 | `versions/*/versions/*` = **0 条** ✅ |
| **L3** 安装根 plugins 非扫描点 | 顶层 `plugins/*/plugin.json` = **0 条** ✅ |
| 包顶层布局 | `ForgeSelf.exe`（启动器）· `host/` · `hostfxr.dll` · `shared/` · `update-agent.ps1` · `versions/` ✅（QQNT 布局）；`versions/current` 指针存在 ✅ |
| 包内 plugin.json 版本 | AIAgent 1.9.6 · McpCenter 2.7.0 · Sems 1.2.0 · TodoTracker 1.2.0 · ToolBridge 1.0.4 · DesignSystem 3.1.0 等 20 个，与工作树一致 ✅ |
| 包内插件前端 = 工作树构建产物 | AIAgent `web/dist/index.js` 275,258 B、McpCenter 75,186 B，**SHA256 与工作树 dist 完全一致** ✅ |
| **包内 dll 真含本批改动**（`scripts/probe-dll-string.cjs`，UTF-8+UTF-16LE 双检） | AIAgent.dll：`aiagent_plan_workflow` FOUND；`run_terminal_command` / `search_tools` / `universal_tool` **ABSENT**（公共工具已迁出）✅<br>McpCenter.dll：`mcp_calculate` / `mcp_get_current_time` / `run_terminal_command` / `search_tools` / `universal_tool` / `list_files` / `read_file` / `write_file` **全部 FOUND** ✅ |
| 更新源配置 | `%USERPROFILE%\.forgeself\config\update-settings.json` → `Provider=local`、`LocalDir=D:\src\my-proj\OpenForgeSelf\updates` ⇒ **与包落点一致，用户无需改配置** ✅ |

### 步4 通知（用户侧动作，agent 不代劳）

宿主升级由 **update-agent 自更新**完成，agent 不代点、不停/启/杀 `:51888`（铁律 1）。用户在 设置 → 版本更新：「检查更新」→（源=本地目录，已指向 `..\updates`）→「下载」→「重启并更新」。

### 未完成项（移交下一轮）

- **运行实例只读复验（⑤）**：等用户升级到 2.4.16.2610281727 后再跑（token 先验 → 版本徽标 → 一条主链路 + 中间态 → 截图读图，存 `ForgeSelf.Web/screenshots/live-51888/`）。已登记 TODO。
- **并行会话的 4 例红测**：`OpenAISdkAdapterTests.Anthropic_Type_Chat_Goes_To_OpenAICompatEndpoint` ×4 属 `2026-10-27-openai-responses-upstream` 批，已随包进入 2.4.16；待该会话落定后随下一版修正。

---

# 第二轮证据 — 2.4.17 发布（输入11 · 2026-10-28 23:20 → 10-29 00:2x）

## 门禁（R1 中档）✅ Verified

| 判据 | 结果 |
|------|------|
| 全量 `dotnet test`（宿主源码被触 → 中档，独跑） | 12 红 = **8 基线红对表**（WorkflowPlanning 6 + ScriptRunnerDi 1 + BOM 1）+ **3 批次夹具红当场修**（`XCodeConfigTests` 删 text-tools 陈旧 InlineData；`ExtensionPointManagerTests` ×2 假工具名改保留名 `list_files` 过 `ToolNamespace.Validate`）后复跑 45/45 绿 + 1 例**存量 flake**（ForgeConfigTests，隔离复跑 1/1 绿，根因与修法记 TODO）→ **新增红=0** ✅ |
| 前端 `check` / `test` / `design:check` | 三绿 ✅（日志 `.forgeself/tmp-pub217-{build,check,vitest,design}.log`） |

## e2e（R2）⚠️ 17/18 + 归因 ✅ Verified

- 一轮 4 worker：18 passed / 8 failed —— 6 ai-providers + 1 file-tools 为 `net::ERR_INSUFFICIENT_RESOURCES`/浏览器崩溃资源饥饿假红；单 worker 复跑 **17/18**（ai-providers 全数复绿，产品无回归）。
- 唯一真红：`file-tools.spec:232` 取消链路期望 `Cancelled` 得 `Failed`（run1 `partial.state` expected 4 got 3）。归因 = **存量 P2 缺陷「folder_stats ACL 拒绝目录→整体 Failed」**：SLOW_SCAN_DIR=`C:\Program Files` 命中 ACL 拒绝目录（e2e 宿主日志 23:44:33 `[FileTools] 目录扫描失败 … Defender ATP\Classification\Configuration … Access denied`）；`FolderScanService.cs` 本批零触碰（git clean）、`FolderStatsToolFunction.cs` 仅改名（git diff 实证）→ **非本批回归**。闸门2.5 向用户披露后放行；修复该缺陷即解此红（TODO 已挂关联注记）。
- mcp-center spec 全绿（本批工具豁免域冒烟达标）。

## 起源码走查（R3）✅ Verified（7 图 + 读图）

| 面 | 实证 | 图 |
|----|------|----|
| 插件 API / 市场 | `/api/plugin` 200 · **19 插件、text-tools 消失**；市场 19 卡（原 20）、dev-tools 卡描述含「吸收 text-tools 全部能力」 | 01/02 |
| AIAgent | 徽标 **v1.9.6**；composer 名册：`read_file`/`write_file`→**file-tools**、`calculate` 裸名→**mcp-center**、`dev_text_stats`→**dev-tools** | 03/04 |
| FileTools | 徽标 **v1.2.0**；真实扫描 `.temp/walk-scan`：总 3.24KB/2 目录/8 文件，sub2 54.46% + sub1 45.54% = **100.00% 闭合**、字节数与磁盘一致（1800B/1500B） | 05 |
| MCP 中心 | 徽标 **v2.8.0**、服务运行中 | 06 |
| 提供方 | 类型下拉 4 项：OpenAI / **OpenAI Responses** / Anthropic / Custom（弹窗取消未保存） | 07 |

截图落 `ForgeSelf.Web/screenshots/live-dev-7399/`（01-07 共 7 张）；02/05/07 已读图复核。ForgeSelf 页面全程 0 控制台错误。走查后 `dev-stack.ps1 -Stop` 收尾（PIDs 71172/113220 退出确认）。

## 闸门2.5（R4）✅ 用户放行

选项原话：**「放行，发 2.4.17（Recommended）」**（ask_user_question 回执 `gate25_release`）。已披露：file-tools e2e 红的归因与「存量缺陷随包（2.4.16 同样带着）」。

## 发布与产物核对（R5/R6）✅ Verified（3 轮发布 + 根因链）

| 轮次 | 版本串 | staged | 结果与根因 |
|------|--------|--------|-----------|
| run1 | 2.4.17.2610282355 | 20 | ❌ 包内 `plugins/TextTools/`（DLL+清单）复活。根因：`artifacts/publish/Plugins/TextTools/` 陈旧构建输出（solution 已删项目，publish 不清输出目录）→ 清残留撤包 |
| run2 | 2.4.17.2610282359 | 20 | ❌ 清了输出仍复活：**ForgeSelf.Api/obj 的 MSBuild publish 增量缓存**按陈旧清单复制（新生文件时间戳=旧值实锤）→ 清 Api obj/bin 撤包 |
| run3 | **2.4.17.2610290009** | **19** | ✅ **终版**（161.8 MB，ALL DONE 111s，签名 新增2/跳过1/失败0、signtool 校验 3/3） |

终版判据（全部机器验证）：

| 判据 | 结果 |
|------|------|
| SHA256 | zip `3ebb41ea…` = SHA256SUMS.txt 记录 ⇒ **MATCH** ✅ |
| 签名（独立复核） | 两枚 exe `Get-AuthenticodeSignature` **Status=Valid** + 时间戳 `CN=Sectigo Public Time Stamping Signer R37` ✅ |
| L1/L2/L3 | L1=**19** 条版本目录内插件清单 ✅ · L2 嵌套 versions=0 ✅ · L3 安装根 plugin.json=0 ✅ |
| 包内插件版本 | dev-tools **1.1.0** · file-tools **1.2.0** · mcp-center **2.8.0** · ai-agent **1.9.6**（19 插件全列核对）✅；新版 TextTools **缺席**（`plugins/` 下无该目录）✅ |
| exe 版本串 | 两枚 exe FileVersion/ProductVersion = `2.4.17.2610290009` ✅ |
| DLL 探针（`scripts/probe-dll-string.cjs`） | `dev_text_stats`∈DevTools.dll ✅；`list_files`/`read_file`/`file_dir_stats`∈FileTools.dll ✅。注：`text_stats`/`folder_stats` 也 FOUND——前者是 `dev_text_stats` 的子串、后者是类名 `FolderStatsToolFunction` 的元数据串（探针子串语义），注册名以走查名册与 `file_dir_stats` 命中为准 |
| 更新源 | `updates/` 仅一枚 2.4.17（2610290009）；缺陷包 2355/2359 已移 `.trash/2026-10-28-stale-texttools-artifacts/`（连 api-obj/api-bin 陈旧缓存与四处残留拷贝，可回溯）✅ |
| **存量残留（不阻断）** | `versions/<ver>/publish/` 子树 27 条（旧式 `.plugin` 清单 ×13 含 texttools.plugin + 6-8 月旧文件）——**2.4.16 同款**、宿主只扫 `plugins/`+数据根两路、惰性；源头 `ForgeSelf.Api/publish/` 古董输出 + PublishDir 相对路径双层倍增（`.temp/e2e/*/publish/publish` 几十份同构为旁证）→ **已记 TODO（发布链产物残留治理）** ⚠️ |

## 通知与复验（R7 后续）

用户侧动作不变：`:51888` 设置 → 版本更新 → 检查更新 → 下载 → 重启并更新（升到 2.4.17.2610290009）。之后做 **⑤运行实例只读复验**（合并 2.4.16/2.4.12/2.4.13 积压项）：token 先验（401 立即上报）→ 版本徽标 → 主链路 + 中间态 → 截图读图存 `live-51888/`。
