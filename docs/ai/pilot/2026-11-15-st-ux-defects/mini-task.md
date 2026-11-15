# mini-task — 智能终端实测三缺陷批（输入22，2026-11-15）

## Intent（为什么/做什么/到什么程度）
用户在 ：51888 v2.0.1 实测发现三缺陷并直接下令修复补测（直接指令=闸门1 依据）：
- **A）新增会话默认起始目录 `~` 报「工作目录不存在」**：2026-11-13 走查 BUG#1 的延期债到期（06-review 分流「默认目录解析/前端占位展开 + 错误信息脱敏」TODO P1）。用户点名「这个没有对应测试，要补」。程度：`~`/`~/x` 正确展开到用户主目录；错误信息不再泄露宿主进程路径；有测试。
- **B）`ls` 输出特殊字符**：实测复现 = PS5.1 GBK「目录:」→ `Ŀ¼:`。根因 = PC-011 严格 UTF-8 仲裁的盲区（GBK 双字节偶合为合法 UTF-8 拉丁扩展字符）。属 **PC-011 逃逸**（二次复发），按 problem-class-closure 逃逸处理。
- **C）`cd` 后页面当前目录不更新**：cmd 包裹命令不采集 cwd（只有 PowerShell/POSIX 带 `::cwd`）+ 命令响应不带 cwd 字段 + 前端不回写。程度：每条命令后 tab 与会话态 cwd 同步更新（cmd/PowerShell 均）。

## Spec（九节精简）
- 行为：新建会话 `~` → connected 且 cwd=主目录；`ls` 中文表头无乱码；`cd` 后 tab 徽标与助手上下文 cwd 更新。
- 不确定点：Unknown —— 真实 UTF-8 带拉丁重音字符（café）单独出现时与 GBK 判别原理性不可分（按 CJK 区域设置偏向 ANSI，已记 Known Limitation）。
- Interaction Design：无新增界面元素（既有 tab 徽标/错误 toast 行为修正）；新建会话弹窗默认值 `~` 保持不变（后端展开后合法）。

## Plan（具体到真实文件）
1. `Plugins/SmartTerminal/Services/TerminalSessionService.V2.cs` — ResolveLocalCwd 展开 `~` + 脱敏；SubmitCommand/ResolveApprovalV2 响应补 Cwd
2. `Plugins/SmartTerminal/Services/SessionShell.cs` — cmd 包裹改 `call echo {done}%ERRORLEVEL% :: %CD%`（顺带修 `!ERRORLEVEL!` 无延迟展开的既有缺陷）+ cmd 尾解析（抽纯函数便于测试）+ RefreshCwd 按 kind 用 `cd`
3. `ForgeSelf.Core/ProcessOutputText.cs` — 严格 UTF-8 采信前加「可疑拉丁扩展且无 CJK」→ ANSI 往返含 CJK 则采信 ANSI（ suspicion gate，PC-011 逃逸修复）
4. `Plugins/SmartTerminal/web/src/types.ts` + `SmartTerminalView.vue` — CommandResult.cwd + 运行后回写 s.cwd
5. 测试：`ForgeSelf.Core.Tests`（B 红转绿）+ `ForgeSelf.Api.Tests/Plugins/SmartTerminalTests`（A 展开/脱敏 + cmd 尾解析）
6. `Plugins/SmartTerminal/plugin.json` 2.0.1→2.0.2 + 构建产物更新 + 运行实例侧载 + live 实控复验

## Task
- Allowed：上列文件 + 对应测试 + 工件/索引/日记
- Forbidden：不动 PTY 设计（会话层不以完整 PTY 为验收标准）；不动宿主 :51888 进程；不动 PC-011 既有语义（只加盲区门）
- 验证命令：`dotnet build` + 定向测试 + `pnpm run check` + 插件 e2e 冒烟 + `check-problem-classes` + `check-commit-hygiene -Staged`；实控走查 live spec
- 裁定：三缺陷同源（会话层输入/输出处理），合并轻量 mini-task（任务协调人依 §11 裁剪规则裁定）

## Evidence / Review
完成与验证后补记于 05-evidence（本目录）与日记；Review 结论在 06-review.md。
