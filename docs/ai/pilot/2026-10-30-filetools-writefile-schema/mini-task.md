# Mini-Task：file-tools write_file schema 重复键修复（PC-017）

> 任务 ID：2026-10-30-filetools-writefile-schema ｜ 类型：缺陷修复（≤3 文件，轻量 mini-task）｜ 闸门1：用户 goal 拍板（原文「先复现这个问题，增加对应的测试用例，再修复，改完之后发布一把到本地zip，按照技能流程控制浏览器点击升级，升级之后，再次通过fs进行测试」——全链路含发布与升级授权）。

## Intent（为什么/做什么/到什么程度）

- **背景**：2026-10-30 万能工具实测发现 `write_file` 对任何参数必挂（`:51888` 三连挂，`Expected depth to be zero…LineNumber 26`），root cause＝`Plugins/FileTools/Services/FileToolFunctions.cs` 242-243 行 `"path": {` 重复键致 schema JSON 少一个闭合括号（已提交源码，构建全绿带病）。详见 `docs/11-troubleshooting/cases/2026-10-30-tool-schema-json-duplicate-key.md`。
- **做什么**：①G2 守卫测试先行（红）→ ②删重复键（绿）→ ③发布本地 zip → ④浏览器控制 ：51888 自动更新 → ⑤升级后经 fs 万能工具端到端复测。
- **成功判据（可验证）**：`dotnet test --filter ToolSchemaJsonGuardTests` 3/3 绿；FileTools 域过滤集全绿；updates/ 出包带新版本号且 SHA256SUMS 复核一致；:51888 设置页版本徽标升到新串；fs `write_file` → `read_file` 回读逐字符一致。

## Spec（关键节摘录）

- **不变量**：见 PC-017 front-matter（工具 schema 必须可解析）。
- **修复方案**：删 243 行重复键；`plugin.json` 1.2.0→1.2.1；新增 `ForgeSelf.Api.Tests/Architecture/ToolSchemaJsonGuardTests.cs`（G2：全插件 verbatim schema 枚举解析 + file-tools 实例级 + write_file 五参锁）。
- **Interaction Design（交互设计）**：N/A——本批无任何用户可见 UI/交互改动（schema 字符串内容修复 + 测试），插件前端零改动；升级后经工具面（fs 万能工具）做功能验收，走查清单不适用，以 e2e 冒烟 + fs 端到端复测替代并在 07 说明。
- **验收标准**：工具面行为 = write_file 新建/改写/回读闭环恢复；其余工具（read_file/list_files/run_terminal_command）行为不回归（同批回归绿）。

## Plan（文件级）

1. `ForgeSelf.Api.Tests/Architecture/ToolSchemaJsonGuardTests.cs`（新增，先红）——已落。
2. `Plugins/FileTools/Services/FileToolFunctions.cs:243`（删重复行）——已落。
3. `Plugins/FileTools/plugin.json`（1.2.0→1.2.1）——已落。
4. 文档：PC-017 + case + 三处索引 + 本 mini-task + 05/06/07。

## Task（Allowed / Forbidden + 验证命令）

- **Allowed**：上列 3 个代码文件；docs/11-troubleshooting、docs/ai/pilot、.forgeself/memory、TODO.md。
- **Forbidden**：不碰 ForgeSelf.Api 宿主源码、不动其他插件、不推送 GitHub、不打 git tag、不手动停/启/杀 ：51888 用户宿主（升级只经页面代点自动更新）、不动 dev-stack 之外的任何实例。
- **验证命令**：
  - `dotnet test ForgeSelf.Api.Tests --filter "(FullyQualifiedName~ToolSchemaJsonGuardTests)|(FullyQualifiedName~FileTools)"`
  - `pnpm exec playwright test --config=playwright.config.ts e2e/plugins/file-tools --output=../.pw-out-filetools`（ForgeSelf.Web/）
  - 发布：`pwsh scripts/release/release-local.ps1 -Version 2.4.20 -Sign`（默认 -UpdateDir → `../updates`）
  - 机器校验：`pwsh scripts/check-problem-classes.ps1` + `dotnet test --filter FullyQualifiedName~ProblemClassRegistry`

## 追加（2026-10-30 复测中发现的第二缺陷，PC-018）

- **发现**：2.4.20 升级后 fs 复测揭出 write_file 报 success 但磁盘无文件——成功分支缺 `File.WriteAllText`（坏合并修复批弄丢，git 首提交 68df669 即如此，被 PC-017 网关错误遮蔽）。
- **修复**：补落盘调用；新增 G2 `FileToolsWritePersistTests`（4 用例，磁盘回读为准；红探针实证 3 failed/1 passed → 恢复后绿）。plugin.json 1.2.1 → **1.2.2**。
- **再发布/再升级**：2.4.21.2610301259（-Sign，SHA256 `e23fd9a8…` 一致，包内 1.2.2）→ 浏览器二轮代点升级 → PID 43580 = `versions\2.4.21.2610301259`。
- **fs 终验全绿**：新建+回读逐字符一致 + pwsh 磁盘独立复核（127 字节）；start_line=2 区间替换回读确认；exec 正例/负例；必填校验；search_tools。
- 归档：PC-018 + case `2026-10-30-tool-success-without-side-effect`（详见 05/06/07）。
