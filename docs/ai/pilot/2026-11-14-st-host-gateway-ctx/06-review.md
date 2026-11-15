# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链（2026-11-14）。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？** 是。助手/凭据解析从「子容器自解析」改为 IContext 通路（共享表 + 宿主 seed），G1 断言从子容器解析出的 gateway.TryChat() 与 ctx.Get<IChatCompletion>() 同一实例；同根因命中（WorkflowExecutor/TaskExecutor/WorkflowTaskHandler）随批收敛。
2. **实现是否符合 Spec？** 符合。FR-1 ctx.Get<T>() 直取（与 ImGatewayRouter 同款）；FR-2 接线守卫红→绿；FR-3 同类扫查全插件完成并升级为 G2 枚举守卫（超出 Spec 预期的守卫强度，方向一致）；424 契约与文案未动（V2 未触碰）。
3. **是否超出了 Scope？** 未超。同类扫查命中且同根因的修复属闸门1 批准范围（「同根因随批修」默认项）；PluginManager 命中经甄别为宿主基建（否），以扫描面收窄处理而非改代码。
4. **是否修改了不应该修改的文件？** 否。Forbidden 清单（TerminalSessionService.V2.cs、st_* 工具、AIAgent/ImGateway、HostProvidedServiceContracts）零触碰；未启停任何宿主进程（dev-stack 为自起自停的源码栈）。
5. **测试是否覆盖 Acceptance Criteria？** 覆盖 4/5 + 1 项部分：红✓绿✓回归过滤集✓（123/123）扫查结论✓；「真实链路佐证——得到助手回复」做到「消息往返 + 病文案消失」，**真实模型回复**因环境无可用上游（LM Studio 未运行 + 隔离数据根无提供方）未取得，标 Unknown，闭环动作 = ⑤ 实例复验（发布后）。
6. **是否存在明显回归风险？** 低（L1）。三处命中文件的改动均为构造签名收敛：Scheduler 两服务在 DI 与工具两条装配路径的实参同一（ctx 本体）；WorkflowExecutor 的 _ctx 字段本已存在，本次去掉双参数歧义；全量构建 0 错误 + 123 相关测试绿 + 冒烟 e2e 绿。残余风险：Scheduler/WorkflowEngine 的 DI 子容器路径在修复前会抛/拿 null（未被执行观察过），修复后两路一致，无行为变差面。
7. **是否存在架构不一致？** 否。修后全部插件 DI 服务构造统一为 IContext-only；工具函数持 ctx 本体的既有形态不变（PC-034 lens 已写明两者边界）；宿主基建（ForgeSelf.Api/Plugins 树）不属插件子容器服务面，已在 PC-034 §2 记录为有意收窄。
8. **Evidence 是否足以证明任务完成？** 足以证明「容器接线缺陷已修复且有守卫防复发」（Verified：G1 同一性 + 双宿主 message 判据 + G2 枚举全绿 + 台账检查器绿）；不足以证明「端到端真实模型回复」（Unknown，已披露，闭环动作已定）。

## Requirement Check

PASS（4/5 AC Verified + 1 项部分完成并披露：真实回复 → ⑤ 复验）

## Scope Check

PASS

## Test Check

PASS（红→绿、回归过滤集、G2 守卫、e2e 冒烟、台账检查器、提交卫生）

## Architecture Check

PASS（插件 DI 服务构造统一 IContext-only；与 PC-001/PC-005 的类边界已在 PC-034 写明）

## Risk

L1（构造签名收敛，双装配路径实参同一；发布动作未发生，:51888 实例不受影响）

## Findings

### Critical

无

### Major

无

### Minor

- `pnpm run check` 基线红：DbAdminView.vue TS2339（非本批引入，已记 TODO）
- PC-004/PC-009 planned 守卫 todo 曾被队列清理破坏「逐字可检索」契约（本批补回；机制上 todo 清理时须对照台账检查器——已由 Registry 测试+checker 双兜底）

### Info

- G2 守卫首跑即真实命中 3 文件，守卫有牙；allowlist 空账。
- e2e 冒烟用例两处环境适配（起始目录 `~` 在 publish cwd 失效 → 置 `.`；聊天面板在 rail 内需先开启）。

## Final Decision

**APPROVED**（附披露：真实模型回复验证随 ⑤ 运行实例复验执行；发布/提交待用户批准）
