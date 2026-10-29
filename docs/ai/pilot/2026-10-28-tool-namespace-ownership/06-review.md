# 06 · Review — 审查（2026-10-28）

## 八问

1. **需求是否全部满足？** 用户拍板四点全部落地：①豁免清单（11 保留名）②公共工具迁 mcp-center（8+1 件，get_current_time/calculate 加 mcp_ 前缀）③其余 81 工具加前缀 ④无别名层无存量迁移。✅（AC-4 运行期 127 工具实证）
2. **实现是否自洽？** ToolNamespace 单一真源（11 保留名 + 16 前缀 + 81 映射行）→ ExtensionPointManager 注册口校验 → 守卫 S1-S5 CI 钉死 → 前端名册/提示词/e2e/文档四面同步。契约 IProjectWorkspaceService 上移 Abstractions，CWD 门语义原样随迁。
3. **验证是否真实充分？** Verified 清单见 05-evidence：快档 251/251、vitest 1034/1034、mcp-center 冒烟 8/8、ai-agent 冒烟 6/7（唯一红 LLM 401 环境面）、e2e 宿主 127 工具注册日志逐名核对。⚠️ 未覆盖：全量 dotnet test（存量 testhost 崩溃，4 次尝试 + 二分定位记录在案）；tool-gateway-031 四场景的 LLM 端到端（上游 401 环境面）。
4. **是否偏离计划？** 两处已记录的偏差：① cost-scope 未加 toolPrefix——它实际无工具（ToolExtensions 空表），S1 误红系注释误判，扫描器层修复后无需前缀（比 03-plan 更优解）；② 04-task 预估 45 文件，实际改动面略大（守卫扫描器 4 处同类修复 + e2e 2 个 spec），均在 Scope Allowed 内。
5. **有无隐藏风险？** ① 旧名彻底退役：未同步的外部集成/脚本将收到 unknown tool（用户拍板接受）；② 全量 testhost 崩溃未修——若 48h 内无外部因素解释，需专项（已记 TODO P1）；③ e2e LLM key 面在并行会话 provider WIP 落定前无法端到端复验 tool-gateway-031。
6. **是否顺手改了无关代码？** 无。唯一跨域文件 HostProjectRegistry 竞态**只记 TODO 未动**（git diff 证实）；外部 WIP 文件零触碰。
7. **测试可否防复发？** 守卫 S1-S5 常驻（新工具违规 CI 即红 + S5 mutation 防守卫失效）；composerContext.test.ts 锁前端名册；TodoTracker 网关回归测试锁 list_todos→todo_list 迁移语义。
8. **经验是否沉淀？** 守卫扫描器「注释剥离 + DTO 行过滤」三层防误报模式已写入 ADR-004 依据节；RenameRows/RenameMap 文本顺序陷阱已固化在 ToolNamespace.cs 注释。

## Final Decision

**APPROVED**（有条件交付：AC-8 全量档与 AC-7 的 LLM 端到端两处标 Unknown，均有明确根因记录与 TODO，不阻塞闸门2 提请——本批全部域内可验证面已绿）。
