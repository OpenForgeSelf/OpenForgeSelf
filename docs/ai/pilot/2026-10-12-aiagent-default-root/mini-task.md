# mini-task：AI-agent 插件文件三件套默认根修复（输入4 · 2026-10-12）

> 级别：轻量（≤3 文件缺陷修复）。Intent/Spec/Plan/Task 合并本文件（五要素）；05-evidence.md 留待执行后单独产出（06-review 同）。
> 闸门1：用户输入4 指令即批（2026-10-12：用户直接下达两文件实施指令，含明确行为规格）；范围=改造守卫语义 + 错误响应，不含 folder_stats/e2e/发布。

## Intent（为什么 / 做什么 / 到什么程度）
- **Problem（探针实证，2026-10-12 输入2，见 `.forgeself/memory/2026-10-12.md`）**：AIAgent 插件文件三件套 `read_file` / `write_file` / `list_files` **无条件前置守卫**，凡「GUI 会话未选目录」即拒。adversarial 实证：绝对路径（workspace 内 `ForgeSelf.slnx` / workspace 外 `C:\Windows\win.ini`）与无参调用一律在**参数解析之前**返回「尚未选择项目目录」；冷启动 100% 首调失败，直至用户人工 `POST api/project/directory` 选目录（`ProjectController.cs:30-42`）。
- **Why（根因）**：目录作用域被放在 **GUI 会话状态**（仅记忆于 `ProjectWorkspaceService.TrySetProjectRoot`），服务端无默认根解析——**违背标准 MCP filesystem 约定**（工作目录由服务器配置/环境决定客户端默认值；对照标准参考实现含 `list_allowed_directories` 锚定根，本实现 `list_tools` 112 工具中 0 命中）。绝对路径本可直达文件，却被会话守卫先杀。
- **Expected Outcome**：「选定目录」从**硬前置降级为默认根**——未选目录时三件套按优先级解析默认根后正常工作；「GUI 选定」仍最优先（不倒退）。绝对路径在默认根内直达；根外仍拒（防穿越语义不放松）。
- **Scope（不改）**：folder_stats（FolderScanService）计数/Failed 问题（另一待办）、未知工具信封增强、MCP 网关、web/、宿主 `ForgeSelf.Api/**`。
- **Success Criteria**：
  1. 冷启动（未选目录）`read_file`/`write_file`/`list_files` 直接可用：默认根=环境变量 `FORGESELF_DEFAULT_PROJECT_ROOT`（缺省=当前进程工作目录 CWD）。
  2. 优先级链恒成立：**GUI 选定 > `FORGESELF_DEFAULT_PROJECT_ROOT` > 当前进程工作目录**（环境变量越权 GUI 选定失败即红）。
  3. 绝对路径在**当前生效根内**直达（免守卫拦截）；目标解析在根外 → 仍拒（防穿越不放松）。
  4. 「尚未选择项目目录」错误成为不可达状态：三件套在「无 GUI 选定 + 无默认根可用」时也返回带修复提示的错误封套，而非裸拒。

## Spec（做什么/到什么程度）
- **Functional Requirements**：
  1. `IProjectWorkspaceService` 新增**默认根解析**（未选目录时）：优先级 = ① GUI 选定（TrySetProjectRoot，语义不变）→ ② 环境变量 `FORGESELF_DEFAULT_PROJECT_ROOT`（未设或空白视为未提供）→ ③ 当前进程工作目录（`Environment.CurrentDirectory`/进程 CWD）。
  2. `ResolveSafePath` 放行**绝对路径**：目标解析后落在当前生效根内 → 直达；落在根外 → 仍拒（防穿越语义不变，拒绝理由统一措辞为「路径越界」）。
  3. 三件套错误响应统一带结构化封套：`{ success:false, error, hint:"set directory via POST api/project/directory", usedRoot:<当前根|null> }`。
  4. `usedRoot` 字段恒在（三件套错误封套必须含）：有生效根=根路径字符串；无可用根=null。
  5. 文档同步：本目录 `MINI-TASK` 留档根因+方案（已满足——本文件）；插件 README/CHANGELOG 不在必改清单（如实施中涉及再补记）。
- **Boundary**：不实现 `list_allowed_directories`/`get_working_directory` 标准工具（记入 TODO 另批）；不改 folder_stats；不动 web/。
- **Error Handling / Compatibility**：GUI 已选目录行为零变化（回归）；旧语义「未选目录→拒」仅收窄到「未选目录→默认根可达」，不新增成功路径以外的语义漂移；越界拒绝理由与今一致（防穿越），仅提示更友好。
- **Acceptance Criteria**：见 Intent·Success Criteria 1-4。

## Plan（具体到真实文件）
1. **修改** `Plugins/AIAgent/Services/ProjectWorkspaceService.cs`：
   - 加默认根解析（环境变量 `FORGESELF_DEFAULT_PROJECT_ROOT` → 进程 CWD，GUI 选定恒最优先）；
   - `ResolveSafePath` 对绝对路径放行（根内）/仍拒（根外）。
2. **修改** `Plugins/AIAgent/Services/ProjectFileToolFunctions.cs`：
   - 「尚未选择项目目录」分支改走默认根（不可达化）+错误响应加 `hint` / `usedRoot` 字段。
3. **新增** `ForgeSelf.Api.Tests/Plugins/AIAgentTests/ProjectWorkspaceDefaultRootTests.cs`（xUnit）：默认根三级优先级、绝对路径根内/根外、usedRoot=null 封套。
4. **禁改**：`web/`（AIAgent 及一切插件）、其他插件、宿主 `ForgeSelf.Api/**`（含 `Program.cs`/`AppBuilder`/`XCodeConfig`）。`plugin.json` 版本号 bump 不在本批（按输入4 范围只修 #1）。
5. **不碰**：`FolderScanService`（folder_stats ACL/Failed 另批修）、`ProjectController.cs`（GUI 选定端点语义不变）。

## Task（工作单元）
- **Objective**：三件套冷启动零首调失败：未选目录时按优先级解析默认根正常读写列目录，越界仍拒，错误封套带 hint/usedRoot。
- **Scope Allowed**：上述 2 修改 + 1 新增测试；跑后端构建 + 过滤集测试；更新日记/TODO/工件。
- **Scope Forbidden**：手改生成件；删测试/改断言放水；改 `web/`；改其他插件与宿主 `ForgeSelf.Api/**`；停/启/杀用户运行中的宿主进程；未授权 git commit/push/tag；发布动作。
- **Acceptance Criteria（checkbox）**：
  - [x] 三件套默认根解析落地（GUI 选定 > 环境变量 > 上溯祖先），优先级用测试钉住（实现侧；过滤集因存量 CS0234 未实跑，见下）
    > 实施偏差留痕（2026-10-12 实施子会话）：用户指令（输入4 原文）对候选 ③ 的口径为「AppContext.BaseDirectory 上溯找含 ForgeSelf.slnx 或 .git 的祖先目录」，与本文件早版写的「进程 CWD」不一致——**以用户指令为准实施**（上溯版比 CWD 更稳：CWD 随启动方式漂移）。工具类字段以用户指令为准：三态封套、成功响应加 usedRoot（=ProjectRoot ?? DefaultRoot）、hint 仅在 root 可得时出现——未采纳本文件早版的「usedRoot 恒在」。
  - [x] ResolveSafePath 绝对路径根内直达/根外仍拒（防穿越不放松）——代码落地，Path.IsPathRooted 直通 + root 前缀检查
  - [x] 错误封套三态 { success:false, error, hint?, usedRoot }（workspace 不可用/无根可兜两态带 usedRoot=null；越界态 root 可得时带 hint）——代码落地
  - [x] 新增 xUnit 测试 `ProjectWorkspaceDefaultRootTests.cs` 全绿 —— 24/24 通过（新增 16 + 旧类回归 8），见执行记录节
  - [x] `Plugins/AIAgent && dotnet build` 0 error（已成功生成 / 94 warnings 全为既有实体生成件）；过滤集 dotnet test 24/24（`-p:OutDir` 旁路绕开宿主文件锁）；`_Custom` csproj 不存在（glob 仅命中 AIAgent.csproj），该步骤按指示跳过
  - [x] 因果链留档：执行记录节已贴 build/test 原文（日志落 .temp/tmp/）；05/06/07 工件已产出（2026-10-13 收尾补齐）

- **Verification Commands**（写进文档，执行后把原文贴进 05-evidence.md）：
  - 后端构建：`cd ForgeSelf.Api && dotnet build` → 判据：Build succeeded，0 个错误
  - 目标测试：`cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~ProjectWorkspace"` → 判据：新增用例全数通过（Passed! 数=过滤集用例数），无新增失败
  - 基线对照：跑前用同一过滤集记录基线（红属 worktree 基线的对表，新增红才是本批的）

## Evidence 计划（05 留待执行后产出 `05-evidence.md`）
- 记录并贴原文：① `dotnet build` 输出（0 error / Build succeeded）；② 过滤集测试输出（Total tests / Passed / Failed 数 + 新增用例名一一对应）；③ 复现命令与判据（勿用 exit code，看日志正文）。
- 分级标注：Verified（实跑输出贴原文）/ Inferred（如回归影响面推断）/ Unknown（如「不确定默认根对 AIAgent 长会话下拉重置的影响」）。
- 本批**未做**（供复审对照，不算缺口）：②-⑥ 维护闭环（插件 e2e / dev-stack 起源码走查 / 发布 / 运行实例只读复验 / 插件文档回写）——按输入4 范围只修 #1，闭环收口见 TODO.md。

## 执行记录（2026-10-12 · 门禁复跑，只读日志正文判据，§5.0）

> 判据：`grep` 日志正文取 `error`/`通过!`/`失败:` 行，**不拿 exit code 当证据**。日志原文落盘 `.temp/tmp/`（build-api-20261012.log / build-api-gate-redirect.log / test-pwsd-20261012-r1.log / test-pwsd-20261012-r2.log），可复核。

### 结果总览
- `buildOk = true`（build log 正文 `0 个错误` / 1348 个既有警告，无 `: error CS` 编译错）
- `testOk = true`（test log 正文 `已通过! - 失败: 0，通过: 24，已跳过: 0，总计: 24` —— 24 例全数通过，含本次的 `ProjectWorkspaceDefaultRootTests`）
- `miniTaskUpdated = true`

### 命令 1：`dotnet build`（ForgeSelf.Api）
```text
$env:TEMP='D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\tmp'
$env:TMP=$env:TEMP
cd ForgeSelf.Api; dotnet build --nologo -v q 2>&1 | Select-Object -Last 30
```
- **第 1 轮（标准 bin 输出）失败**，但失败原因非代码编译错，而是宿主运行实例锁文件：` ForgeSelf.Api\bin\Debug\net10.0-windows\` 下 `ForgeSelf.dll`/`ForgeSelf.Core.dll`/`ForgeSelf.Abstractions.dll` 被 `.NET Host (102508)` 锁定（`dev-stack.json` 记录 102508 = 起源码 dev-stack 实例 :7301，2026-10-11 19:56 起）。日志正文：
  ```text
  C:\Program Files\dotnet\sdk\10.0.302\...(5096,5): error MSB3027: 无法将"...ForgeSelf.Core.dll"复制到"bin\Debug\net10.0-windows\ForgeSelf.Core.dll"。超出了重试计数 10。失败。文件被".NET Host (102508)"锁定。
  C:\Program Files\dotnet\sdk\10.0.302\...(5096,5): error MSB3021: 无法将文件"..."。Could not find file 'ForgeSelf.Api\bin\Debug\net10.0-windows\ForgeSelf.Core.dll'.
      349 个警告
      4 个错误
  ```
  ⇒ **MSB3021/MSB3027 全为文件锁**，非 `CSxxx` 代码错；且铁律禁止杀宿主 → 不修代码，改**改输出目录绕锁**（第 2 轮）。
- **第 2 轮（`-o .temp\tmp\gate-build-api` 重定向输出）通过**：
  ```text
  D:\...\ForgeSelf.Api\Services\...\UpdateService.cs(551,31): warning CS8601: 可能的 null 引用赋值。
  D:\...\ForgeSelf.Api\Plugins\Dev\DevMode.cs(70,25): warning CS0414: 字段"DevMode._initialized"已被赋值，但从未使用过它的值
      1348 个警告
      0 个错误
  ```
  判据：日志正文 `0 个错误`，grep `: error (CS|MSB|NETSDK)` 零命中；产物锚点 `.temp\tmp\gate-build-api\ForgeSelf.dll` 等三件套存在（阳性对照，规避同源自洽）。

### 命令 2：`dotnet test --filter 'FullyQualifiedName~ProjectWorkspaceDefaultRoot'`（ForgeSelf.Api.Tests）
```text
$env:TEMP / $env:TMP 同上（.temp\tmp）
cd ForgeSelf.Api.Tests
第 1 轮: dotnet test --filter 'FullyQualifiedName~ProjectWorkspaceDefaultRoot' --nologo
第 2 轮: 加 -p:OutDir=<repo>\.temp\tmp\gate-test-outdir
```
- **第 1 轮失败**：测试 build 阶段同样被 `.NET Host (102508)` 锁 `ForgeSelf.Api\bin\...` 拷贝（MSB3026 重试 ×10→MSB3027），未到测试执行段，无 `Passed!`/`Failed!` 统计行。
- **第 2 轮（`-p:OutDir` 旁路；仓内既有规避方案）通过**，日志正文最后一行：
  ```text
  已通过! - 失败:     0，通过:    24，已跳过:     0，总计:    24，持续时间: 198 ms - ForgeSelf.Api.Tests.dll (net10.0)
  ```
  判据：grep `Passed!|Failed!|通过!|失败:` 仅命中该统计行；`失败: 0`。
  说明：过滤集 `ProjectWorkspaceDefaultRoot` 命中 24 例 —— 本批新增 16 例 + 旧类内回归 8 例（早前旧版 `ProjectWorkspaceTests` 的既有同名系列，非本批文件）。**无 CS0234**（mini-task 早先记录的 `WorkflowEnginePluginRegistrationTests.cs:63 CS0234 引用不存在的 WorkflowServiceResolver` 已不再命中：`WorkflowServiceResolver` 现存在于 `Plugins/AIAgent/AIAgentPlugin.cs:172`（internal + csproj `InternalsVisibleTo` 已配），本批未提交改动已含）。

### 修复轮次
- build：0 轮代码修复（锁文件属环境问题，用 `-o` 目录绕开，未改实现）；test：0 轮代码修复（同因，用 `-p:OutDir` 绕开）。两处均**远低于 2 轮上限**。