# Evidence

> 阶段：Stage 7｜**只记录实际发生的事**，不根据代码推测结果。
> 来源等级：Verified（亲自跑过、有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。⛔ 禁用「应该可以 / 理论上 / 大概率」。

## Task

PILOT-052（2026-10-05-mcp-center-endpoint-url）

## Changed Files

- `Plugins/McpCenter/web/src/McpCenterView.vue`（+ `mcpEndpointUrl` computed；chip / 「MCP 服务地址」卡 / 复制按钮三处改用端点；`data-mcp-url` 挂点；一行 `/mcp` 说明）
- `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`（弱断言升级为「chip == `listenUrl` + `/mcp`」等值判据 + `[data-mcp-url]` 同值 + 根地址 404 反向腿 + 证据行）
- `Plugins/McpCenter/plugin.json`（`Version` 2.2.0 → 2.2.1）
- `Plugins/McpCenter/McpCenter.csproj`（`Version`/`AssemblyVersion`/`FileVersion` → 2.2.1 / 2.2.1.0）
- `docs/02-features/034-mcp-center.md`（前端界面节地址口径 + ⚠ 地址口径说明 + 验证记录 v2.2.1）
- 运行产物（**不入库**，`.gitignore` 忽略但必须重建）：`Plugins/McpCenter/web/dist/{index.js,style.css}`

> `git diff --stat` 实读：5 files changed, 74 insertions(+), 16 deletions(-) —— 与 04-task 的 Allowed 列表逐一对齐，无越界。

## Build

Command:

```bash
cd Plugins/McpCenter/web && pnpm run build
```

Result: **PASS**（来源等级：Verified，EXIT=0）

```text
$ vite build
vite v6.4.3 building for production...
✓ 46 modules transformed.
dist/style.css  114.88 kB │ gzip: 17.05 kB
dist/index.js    49.62 kB │ gzip: 10.79 kB
✓ built in 14.30s
```

产物内容抽检（来源等级：Verified，`Select-String` 实读 `dist/index.js`）：

```text
line 734: ... c(b.value ? R.value || "（未运行，暂无地址）" : "加载中...") ...
line 735: ... t("div", { class: "card-sub" }, " 客户端须使用 /mcp 路径（根路径 404） ", -1) ...
```

## Unit Test

Command:

```bash
# 该插件前端无测试框架（实测 package.json 无 test 脚本 / 无 vitest / 无 *.test.ts），本任务不引入依赖，判据落插件层 e2e
```

Result: **N/A**（依据：`Plugins/McpCenter/web/package.json` 无测试脚本；本任务范围明确禁止新增依赖）

`SchemaForm` 之类纯逻辑单测属另一批（worktree `wt-mcp-playground`），与本批无关。

宿主单测（§5.6 快档必跑项，本批未改宿主 `src/`，作为回归）：

```bash
cd ForgeSelf.Web && pnpm run test
```

Result: **PASS**（来源等级：Verified，EXIT=0）

```text
Test Files  67 passed (67)
     Tests  756 passed (756)
  Duration  128.37s
```

## Integration Test

后端回归（不涉及后端改动，作为版本号/构建回归）：

```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~McpCenter"
```

Result: **PASS**（来源等级：Verified）

第一次运行（未重定向 TEMP）：**失败 22 / 通过 74 / 总计 96**，22 条全部为同一环境原因，例如：

```text
失败 ForgeSelf.Api.Tests.Plugins.McpCenterTests.McpGatewayConfigTests.Load_EnvPortOverridesConfigFile
  System.UnauthorizedAccessException : Access to the path 'C:\Users\Administrator\AppData\Local\Temp\mcpcfg_48ad6bd7321845e7a3852a82e7ed0318' is denied.
```

→ 本机环境不允许在用户 `%TEMP%` 下新建目录（测试自建 `mcpcfg_*` 隔离目录）。**把 TEMP/TMP 重定向到仓库内 `.temp/api-tests-tmp` 后重跑**：

```text
已通过! - 失败: 0，通过: 96，已跳过: 0，总计: 96，持续时间: 8 s - ForgeSelf.Api.Tests.dll (net10.0)
```

即 22 红为**环境限制**（`Path.GetTempPath()` 写入被拒），非代码缺陷；重定向后 96/96 全绿。日志：`.temp/api-tests-mcpcenter.log`。

## E2E

Command:

```bash
cd ForgeSelf.Web && pnpm exec playwright test e2e/plugins/mcp-center/mcp-center.spec.ts --workers=1
```

Result: **PASS**（来源等级：Verified，EXIT=0）

```text
3 passed (2.1m)

[evidence] host plugin: mcp-center v2.2.1
health: {"status":"ok","version":"2.2.1","tools":1,"listen":"http://127.0.0.1:19483"}
config GET: {"port":19483,"listenHost":"127.0.0.1","listenUrl":"http://127.0.0.1:19483","hasToken":false,"tokenMasked":"","isRunning":true,"version":"2.2.1"}
[evidence] 地址展示：config.listenUrl=http://127.0.0.1:19483 → 界面展示=http://127.0.0.1:19483/mcp；根地址 404 / 端点 200（tools[0]=universal_tool）
外部 MCP 接入：stdio toolCount=2 / streamable-http echo / http-sse add(10,32)=42 全部通过（回归未破坏）
```

本次新增判据逐条对上 04-task / 02-spec 的 AC：

| AC | 判据 | 实测输出 | 等级 |
| --- | --- | --- | --- |
| AC2 | chip 文本 == `listenUrl` + `/mcp`；`[data-mcp-url]` 同值；chip `title` 同值 | `toHaveText(expectedEndpoint)` 通过；证据行「界面展示=http://127.0.0.1:19483/mcp」 | Verified |
| AC3 | 状态卡数量 3 + 运行状态卡仍显示「监听 host:port」 | `toHaveCount(3)` + `.card-sub` toContainText「监听 127.0.0.1:19483」通过 | Verified |
| AC4 | 端点 `POST /mcp` → 200 且 `tools[0]=universal_tool`；根地址 → 404 | 「根地址 404 / 端点 200（tools[0]=universal_tool）」 | Verified |
| AC1 | 插件前端 build 成功 | 见 Build 节 | Verified |
| AC5 | `pnpm run check` 0 error + 后端过滤集全绿 | 见 Static Analysis / Integration Test | Verified |
| AC6 | 版本同串 2.2.1 + 文档同步 | 见 Version Check | Verified |
| AC7 | 视觉核对 | 见 Screenshots（读图） | Verified |

**环境障碍与处置（重要，勿删）**：首次与第二次运行均在 `Error: Timed out waiting 120000ms from config.webServer.` 处失败。用 `DEBUG=pw:webserver` 拿到决定证据：vite 实际已在 7002 就绪（`VITE v6.4.3 ready in 4827 ms ➜ Local: http://localhost:7002/`），但 Playwright 的可用性探测恒返回 **502** 并无限等待：

```text
pw:webserver HTTP GET: http://localhost:7002/
pw:webserver HTTP Status: 502
```

根因：本机环境设了 `HTTP_PROXY=http://127.0.0.1:10808` / `HTTPS_PROXY=…10808` 且**没有 `NO_PROXY`**，Playwright 的探测走了代理，代理到不了 localhost ⇒ 502。控制实验：直接 `Invoke-WebRequest http://localhost:7002/` → 200（.NET 自动旁路 localhost）；`node` fetch 带 `NO_PROXY=localhost,127.0.0.1` → 200。**加 `NO_PROXY=localhost,127.0.0.1,::1` 后同一命令 3 passed**（未改仓库任何配置/代码来绕过）。

## Static Analysis

```bash
cd ForgeSelf.Web && pnpm run check
```

Result: **PASS**（来源等级：Verified，EXIT=0）

```text
✖ 81 problems (0 errors, 81 warnings)
```

（81 warnings = 仓库既有基线量级，均为 `no-explicit-any` / 单文件多组件 / 换行风格等既有项，本批未新增。）

## Version Check

（来源等级：Verified，`Grep` 实读）

```text
Plugins/McpCenter/McpCenter.csproj:9:    <Version>2.2.1</Version>
Plugins/McpCenter/McpCenter.csproj:10:   <AssemblyVersion>2.2.1.0</AssemblyVersion>
Plugins/McpCenter/McpCenter.csproj:11:   <FileVersion>2.2.1.0</FileVersion>
Plugins/McpCenter/plugin.json:4:  "Version": "2.2.1",
```

编码核对（来源等级：Verified，字节级实读）：5 个被改文件首 3 字节均**不含 BOM**（`plugin.json` = `7B 0D 0A`，其余为 ASCII 起始），`git diff` 无整文件重写/patch 噪声。

## Screenshots

- `ForgeSelf.Web/screenshots/e2e/mcp-center/gateway-tab.png`（**读图核对**：顶部 chip = `http://127.0.0.1:19483/mcp`；版本徽标 `v2.2.1`；「MCP 服务地址」卡 = 同值 + 说明行「客户端须使用 /mcp 路径（根路径 404）」；「运行状态」卡仍显示 `监听 127.0.0.1:19483`；状态卡 3 张；chip 未截断/未溢出）
- `ForgeSelf.Web/screenshots/e2e/mcp-center/tools-tab.png`（读图核对：工具管理 tab 下 chip 同样为 `/mcp` 端点，无截断）

## Known Limitations

1. **剪贴板未做自动读回断言（原 02-spec U1 降级，已如实登记）**：headless 读回剪贴板需 `clipboard-read` 权限，本批未做。已保证的事实是：chip、`[data-mcp-url]`、复制按钮**共用同一个 `mcpEndpointUrl`**（复制函数体内即 `mcpEndpointUrl.value`），且复制按钮 `:disabled="!mcpEndpointUrl"`；因此界面展示的字符串与复制内容必然同值（Inferred + 代码事实），但「点击后剪贴板字符串 == 端点」**未单独自动化验证**（Unknown）。
2. **`listenHost = 0.0.0.0` 时照直展示 `http://0.0.0.0:<port>/mcp`**（不替换为 localhost）——与既有行为一致，属"不擅自引入新语义"的刻意选择，但用户在局域网场景可能仍需自行替换主机名（Unknown 影响面）。
3. **卡片说明行在 1280 宽下折成两行**（`…（根路径` / `404）`），无截断、无溢出；窄屏排版未做进一步优化。
4. **发布与运行实例复验均已完成**：本地离线整包已出到用户指定目录（见下节「发布证据」），用户已在 `:51888` 完成升级 ⇒ 「运行实例只读复验」已执行（见下节同名小节）。仍未做的只有 `git commit`（等用户指示；本轮用户已明确「提交且仅提交本次修改」）——**未 push / 未打 tag、未停启任何宿主进程**。运行实例复验里的「浏览器截图」一格以「伺服产物与仓库产物逐字节一致」作为**显式替代**判据（理由见同名小节）。

## 发布证据（本地离线整包 → 用户指定更新目录）

用户指令（2026-10-05 输入2）：「D:\src\my-proj\OpenForgeSelf\updates 指定发布到这里 完整打包即可」。

命令（`pwsh` 7.6.6；依 `AGENTS.md` §2.3 禁用 5.1）：

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/release/release-local.ps1 `
  -Version 2.7.3 -Sign -UpdateDir D:\src\my-proj\OpenForgeSelf\updates
```

- **首次失败（环境，非代码）**：`build-frontend` → 宿主前端 vite build → `[vite:esbuild-transpile] remove C:\Users\Administrator\AppData\Local\Temp\esbuild-<hash>: Access is denied`；把 `TEMP`/`TMP` 指到仓库内 `.temp/tmp` 后重跑 ⇒ **EXIT=0，346s**（读数原文 `.temp/release-local-2.7.3-retry.log`）。
- **产物**：`updates\OpenForgeSelf-2.7.3.2610051746-win-x64.zip`（106.7 MB，写入 2026-10-05 17:52:43）+ `RELEASE-NOTES-2.7.3.2610051746.md` + `SHA256SUMS.txt`。

按包内容验真（机器判据；读数原文 `.temp/pub-verify.log`）：

| 项 | 判据 | 读数 | 等级 |
| --- | --- | --- | --- |
| SHA256 | zip 实测 hash == `SHA256SUMS.txt` 首行 | `43f9d4d1b1ad…f61679` **MATCH** | Verified |
| L1 内置插件随版本 | 包内 `versions/<ver>/plugins/<Id>/plugin.json` | 仅 `versions/2.7.3.2610051746`；**18 个内置插件全在该目录下** | Verified |
| L2 无逐代嵌套 | `versions/<ver>/versions/` 条目数 | **0** | Verified |
| L3 安装根不含插件 | 顶层 `plugins/` 条目 | **不存在** | Verified |
| 入口 | 顶层 `ForgeSelf.exe` | 存在（401 KB） | Verified |
| 目标插件 | 包内 `plugins/McpCenter/plugin.json` | **mcp-center v2.2.1**（`web=True`） | Verified |
| 前端产物一致性 | 包内 dist vs 仓库 dist 的 SHA256 | `index.js` / `style.css` 均 **identical=True**（即 e2e 3 passed 的那一版产物） | Verified |
| 签名 | `Get-AuthenticodeSignature` | 根 `ForgeSelf.exe` 与 `versions/<ver>/ForgeSelf.exe` 均 **Valid**，signer `CN=OpenForgeSelf 铸己匣` | Verified |
| 版本同串 | exe FileVersion/ProductVersion == zip 名 == versions 目录 == notes 标题 | `2.7.3.2610051746`（ProdVer 尾带 git 短哈希 `+5dd975e…`） | Verified |

「页面能否检出可升级」：`UpdateChecker.CheckLocalAsync` 扫描更新目录内 `OpenForgeSelf-*-win-x64.zip` 按 semver 取最新，再 `CompareSemVer` 逐段比 4 段数值（同属 date-coded 世代）⇒ `2.7.3.2610051746 > 2.7.3.2610042326`（实例当时版本）⇒ `HasUpdate = true`；该链路有既有单测（`UpdateCheckerTests` 等 `--filter "FullyQualifiedName~UpdateChecker|FullyQualifiedName~UpdateSettings"` **61/61 通过**）。**已实读用户实例的更新源配置** `%USERPROFILE%\.forgeself\Config\update-settings.json` → `Provider=local` / `LocalDir=D:\src\my-proj\OpenForgeSelf\updates` / `Channel=stable`（与本包目录一致；包名无 `-preview` 后缀 ⇒ 属稳定版，能被 stable 通道接住）。

## 运行实例只读复验（2026-10-05 已执行 · 用户升级到 2.7.3.2610051746 之后）

触发条件已满足（用户回复「升级完了」，且实读进程/`current` 证实落到本包版本）。**全部只读**：未停/启/杀任何进程，未点不可逆动作，未调改状态的端点。读数原文 `.temp/live-verify.log`。

| 步 | 判据 | 读数 | 等级 |
| --- | --- | --- | --- |
| 0 落地版本 | 业务层 exe 路径 + `versions/current` | 业务层 `D:\src\tools\ForgeSelf\versions\2.7.3.2610051746\ForgeSelf.exe`（pid 20920）；根启动器 `D:\src\tools\ForgeSelf\ForgeSelf.exe`（pid 90116）；`current=2.7.3.2610051746` | Verified |
| 0 token 有效性 | 打一个**需鉴权**的只读端点 | `GET /api/plugin/detail/mcp-center` → **200**（非 401）；token 经 `node scripts/get-forge-token.cjs` 取，`len=35` | Verified |
| 1 插件已加载 | `GET /api/plugin` | `plugins_total=18`；`mcp-center` = **version `2.2.1`** / `state=5` / `isEnabled=true` | Verified |
| 2 网关配置（界面数值来源） | `GET /api/mcp-center/config` | `{port:18890, listenHost:"127.0.0.1", listenUrl:"http://127.0.0.1:18890", hasToken:false, isRunning:true, version:"2.2.1"}` ⇒ 界面按 `mcpEndpointUrl` 展示 **`http://127.0.0.1:18890/mcp`** | Verified |
| 3 前端产物一致（视觉等价判据） | 宿主伺服的 `GET /plugins/mcp-center/web/dist/index.js` sha256 vs 仓库产物 | 49624 B；`5C96F7F4B7A4BC3F4A9510A34019D8D82BCAEBC37366D14219AE941E8C1F9627` **== 仓库 `Plugins/McpCenter/web/dist/index.js`**（逐字节一致 ＝ e2e 3 passed 的同一份产物） | Verified |
| 4 浏览器截图存档 | 按 `plugin-publish-verify` 步骤 4（注入 localStorage → 核版本徽标 → 走主链路 → 截图读图） | **未做** —— 见下方说明 | Unknown |

> **步骤 4 为何以步 3 替代（显式替代，不是漏做）**：截图本身只读，但会驱动**用户正在使用的浏览器会话**（抢焦点/切页）；而本次要确认的事实——"运行实例跑的正是本批修改后的那版界面"——已由**产物字节级一致**给出比截图更强的判据（截图是像素证据，字节一致是同一性证据），且该产物的视觉结论已在插件层 e2e 中被读图确认过（同一 sha256）。因此本批以步 3 收口，并在 034 文档与本节如实登记。需要一张运行实例截图时再单独执行。

05-evidence 中此前对 `.temp/pub-verify.log` / `.temp/live-verify.log` 的引用：两份日志已按"一次性证据不入库"的惯例移入 `.trash/2026-10-05-mcp-center/`（原文可查）。

## Unresolved Issues

1. **环境级（非本批代码）**：① `HTTP_PROXY` 无 `NO_PROXY` ⇒ 本机所有 Playwright e2e 的 webServer 探测恒 502（本批靠运行时 `NO_PROXY` 绕过，**仓库内未加环境守卫**）；② 本机 `%TEMP%` 下新建目录/删除临时文件被拒 ⇒ 三个不同面各中一次：依赖 `Path.GetTempPath()` 的后端用例整片假红、vite 预构建 `deps_temp_*` 写入被拒、**本次发布链的宿主前端 esbuild 临时文件 `remove … Access is denied`**（均靠把 TEMP 指到仓库 `.temp/` 绕过）。两条已登记 TODO 待用户决定是否做成"跑 e2e / 跑发布链的规范化入口或环境守卫"。
2. **版本号协调**：并行 worktree `wt-mcp-playground`（未提交）把 MCP 中心写到 2.3.0；两批合并时版本取高者并需复跑插件门禁（本批 2.2.1 已记事，尚无冲突动作）。
3. **包内容验真目前靠 `.temp` 一次性脚本**：本轮为出包验真写了 `.temp/verify-package.ps1`（用完即删、不入库）；按"重复动作脚本化优先"应固化为 `scripts/release/verify-package.ps1`（带 UTF-8 BOM，过 `RepositoryScriptTests`），已记 TODO。

