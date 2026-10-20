# AI-Native Pilot 结果汇报（闭环）

> 状态只裁事实，禁止模糊（AGENTS.md §10.4/§10.5）。本文件为闸门2 交付底稿，聊天正文只报路径+摘要+待拍板。

## 1. Repository Understanding

已确认：外部 MCP 执行超时原为三传输写死 30s（HTTP 传输另有把毫秒当秒的单位 bug）；配置读写链 = McpGatewayConfig(Load/env) → McpCenterRuntime(GET/PUT/Persist) → McpCenterView.vue；建连唯一入口 McpClientManager.CreateTransport；Stateless v2.4.0 为同款贯穿模板。详见 00/01。

## 2. Selected Task

McpCenter v2.5.2：所有外部 MCP 执行默认超时改可配置，默认 5 分钟（用户输入「按你推荐的来」= 闸门1 批准全局单值 + 1 秒~1 小时合法域）。

## 3. Changed Files

后端 8（Config/三传输/Manager/Plugin 入口/DTO/Runtime）+ 前端 2（types/gateway.ts、McpCenterView.vue）+ 版本 2（plugin.json、csproj）+ 测试 3（新 TimeoutTests、RuntimeTests 追加、mock fixture --with-slow）+ e2e 1（mcp-center.spec.ts v2.5.2 用例）。全表见 05-evidence。

## 4. Validation

Build: `dotnet build ForgeSelf.Api` exit=0；插件前端 `pnpm run build` 成功（dist/index.js 73.72 kB）。
Unit Test: `dotnet test --filter FullyQualifiedName~McpCenter` **169/169 绿**；前端 vitest **23/23 绿**；`pnpm run check`（vue-tsc）0 错。
E2E: `playwright test mcp-center.spec.ts -g "v2.5.2"` **1 passed (1.2m)**（5s 掐断/60s 放行/越界 400/UI 回显 60）。

## 5. Evidence

见 `05-evidence.md`：三级优先序与回落、Lazy 生效链（DELETE+POST 重连放行 vs 原连接掐断）、单位修复断言（HttpTimeout==FromMilliseconds(45000)）均为 Verified；截图 `ForgeSelf.Web/screenshots/e2e/mcp-center/execution-timeout-ui.png` 已读图。

## 6. Review

见 `06-review.md`：Final Decision = **APPROVED**（Critical/Major 0；CodeReview 子代理 2 条 Minor 已当场修复并在 dev-stack 复验：清空→后端 400 零污染、非整千值不再静默回写）。

## 7. Risk

L1 — 插件内配置面扩展；回滚 = git 还原本批文件回 2.5.1（原子）。

## 8. Problems Found

- 走查发现并修复：窄视口 hint 溢出裁切/输入框挤压（本批新行引入，已修 + 截图复核）。
- 走查附带发现（**非本批引入、已登记 TODO.md 不顺手修**）：McpCenterView `v-loading` 未注册指令（TODO 37 行③，2026-10-16 已记）。
- 环境插曲：agent 自起 dev-stack 锁构建 DLL（-Stop 解决）；Roslyn 编译服务器一次崩溃（build-server shutdown 解决）——均非产品缺陷。

## 9. Process Evaluation

| 阶段 | 结论 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS（三处实施偏差已回写 03-plan「实施偏差记录」节） |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

## 10. 问题类归档

本批为功能增强任务，**无新缺陷类**（problem-class-closure 不触发；门禁校验不适用）。相关既有 PC-010 lens 不变量（帧协商 5s / endpoint 发现 15s 保持常量）由既有守卫测试复跑绿保护。测试写法教训（非缺陷类）入 §12。

## 11. 重构提案

**R0 不重构**。理由：超时注入点已收敛到 `CreateTransport` 单点 + `IsValidExecutionTimeout` 单点判据，与 Stateless 模板同构，无重复/散落；再抽象（如超时策略对象）超出当前需求规模。

## 12. 最终教训清单（不得为空）

1. **单测临时目录必须显式 CreateDirectory**（Load 只在缺文件分支自建）——首跑 9 例 DirectoryNotFound；已修 MakeTempDir。
2. **异步断言用 `Assert.ThrowsAsync(...).Result`**（`.Wait()` 被 AggregateException 包裹失配）——与既有端口用例同法。
3. **转发 error 封套中文被 \u 转义**，e2e 断言必须先 `JSON.parse` 再 `toContain`（与既有坏格式用例同法）——首跑红一次。
4. **UI 走查不能只在宽视口看**：窄视口 flex 挤压 + `overflow:hidden` 裁切是真实缺陷（hint 长英文 token 撑 min-content）；`.form-hint` 已加通用守卫。
5. **保存脏比较须以加载基准值为参照**（反算比较会静默规范化未编辑字段 + NaN 恒真）——CodeReview Minor 闭环。
6. 悬空项（完成后勾）：门禁④发布、⑤运行实例复验、⑥034 文档回写、闸门2 批准、git 提交——见 TODO/日记。
