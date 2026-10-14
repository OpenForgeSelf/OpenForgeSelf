# Mini-Task：修 verify-pilot-gate.ps1 glob 范围守卫误报

> 级别：轻量（≤3 文件缺陷修复）｜任务 id：2026-10-12-pilot-gate-glob-fix
> 依据：远程会话指挥（L0 结论：68 项 = 21 脚本 bug + 47 真实越界；下一步 = 修 glob）。
> 闸门1：由用户指令「你按指挥来操作」批准本方案（改动范围 = 远程会话「下一步」节明示的唯一动作）。

## Intent（为什么 / 做什么 / 做到什么程度）

- Problem：verify-pilot-gate.ps1 范围守卫把 `**/` 用 `-replace '\*\*','*'` 压成 `*/`，要求至少一层目录，导致 src/ 根下的 .vue（App.vue 等 21 项）被误判「越界 diff」。
- Why：门禁误报使历史审计结果失真（68 项中 21 项非真实越界），CI 上线后新区间会继续误伤。
- Expected Outcome：`**/` 语义 = 零或多层目录；单个 `*` 不跨目录；保留「写目录即含其下全部」旧行为。
- Constraints：只改 scripts/verify-pilot-gate.ps1；不得回改已批准 04；保存保留 UTF-8 BOM；不引入依赖。
- Success Criteria（AC）：
  - AC1：`pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-gate.ps1 -Base a00e45d` 末行 = `FAIL: pilot 闸门未通过（47 项）`（21 误报消除，47 真实越界保留）
  - AC2：src/ 根下 .vue 与 `Plugins/*/web/src/*.vue` 不再被报越界
  - AC3：文件保留 UTF-8 BOM（EF BB BF）
  - AC4：git diff 仅含 scripts/verify-pilot-gate.ps1，改动为 +24/-2（其余字节与 HEAD 一致）
  - AC5：未触碰任何 04-task.md；未提交 / 未推送

## Spec

- FR1：新增 `ConvertTo-GlobRegex`：`**/` → `(?:.*/)?`（零或多层目录）；其余位置 `**` → `.*`；单个 `*` → `[^/]*`（不跨目录）；`?` → `[^/]`；其余字符 regex 转义；结尾追加 `(?:/.*)?$`（写目录即含其下全部，保留旧行为）。
- FR2：范围守卫内层循环改用 `if ($f -cmatch (ConvertTo-GlobRegex $p)) { $ok = $true; break }`。
- Input：04 expected_files 的 glob 串（正斜杠归一、去尾 `/`）。
- Output：判定布尔。
- Boundary：空 glob；以 `/` 结尾的目录 glob；含 `\` 的路径（归一为 `/`）。
- Error Handling：`[regex]::Escape` 转义普通字符，无注入风险。
- Compatibility：对 CRLF/LF 文档、仓库小写路径（`-cmatch` 与原 `-like` 语义差异已评估：仓库路径均为小写，无影响）。
- Non-functional：零新依赖；单函数纯计算。
- Acceptance Criteria：= Intent 的 AC1-AC5。

## Plan

- file: scripts/verify-pilot-gate.ps1（门禁脚本本体，glob 误报根因所在）
  - 动作①：在 `# ---- 3) 范围守卫` 注释之前插入 `ConvertTo-GlobRegex` 函数（22 行 + 空行）
  - 动作②：内层循环两行（`$pp = ($p -replace '\*\*', '*')...` 与 `if ($f -eq $pp ...)`）替换为一行 `-cmatch` 判定
  - 动作③：字节级保存——保留 HEAD 混合行尾（75 CRLF + 150 LF）与 UTF-8 BOM，避免编辑工具全文件 EOL 规范化造成噪音 diff
- 不做：不修 3b 回写检查锚点问题（只核对报告，修复需另行批准）；不改任何 04/文档；不提交；不跑 §5.6 中档后端全量（协调人明确只跑一条命令；且 ForgeSelf.Api.Tests 存在预存编译阻断 WorkflowEnginePluginRegistrationTests.cs:63，见 TODO）

## Task

- Allowed：修改 scripts/verify-pilot-gate.ps1；读写 .temp 探针产物（用完即删）；创建本任务工件
- Forbidden：改 04-task.md / docs / 其他业务文件；git commit / push / tag；跑全量测试与 e2e；停启宿主进程
- Verification Commands：
  1. `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-gate.ps1 -Base a00e45d`（期望末行 FAIL 47 项）
  2. `git diff --stat scripts/verify-pilot-gate.ps1`（期望 24 insertions, 2 deletions）
  3. 字节检查（BOM EF BB BF；CR/LF 计数与 HEAD blob 一致）
