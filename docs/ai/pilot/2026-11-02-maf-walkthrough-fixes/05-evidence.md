# 05 Evidence — 浏览器走查缺陷批（2026-11-02-maf-walkthrough-fixes）

> 来源等级：全部 ✅ = **Verified**（亲自跑过/截图读过）；无 Inferred 充当结论。

## 1. 缺陷 D2：记忆时间戳（TDD）

| 步骤 | 命令/动作 | 结果 |
|---|---|---|
| 红灯 | `dotnet test --filter MemoryTimestampGuardTests`（修复前，宿主 DLL 解锁后） | **失败: 3，通过: 0**（断言 CreatedAt/UpdatedAt > MinValue 全红）|
| 修复 | `MemoryServiceXCode.cs` 4 处（Create :94-95 / Import :424-425 / Update :133 / Delete :178） | 编辑落盘，Select-String 复核 7 处时间戳赋值点 |
| 绿灯 | `dotnet test --filter FullyQualifiedName~MemoryTimestampGuard\|FullyQualifiedName~MemorySystem` | **失败: 0，通过: 26**（3 守卫 + 23 记忆回归）|
| UI 复验 | dev-stack 重启后 `/memory` 新建记忆（表单填写→保存→详情面板） | 创建时间 **2026/11/02 20:18**（修复前 1/01/01 00:00）；截图 11 |
| 清理 | API 删 id=1,2 + UI 删 id=3 | search remaining=0 |

守卫文件：`ForgeSelf.Api.Tests/Integration/MemoryTimestampGuardTests.cs`（新增 3 用例）。

## 2. 缺陷 D1：Home 插件旧名（PC-021 同类）

| 步骤 | 动作 | 结果 |
|---|---|---|
| 修复 | `features.ts:54`（名称）、`features.ts:382`（IM 网关描述）、`HomeView.vue:147`（看板标题）、`HomeView.vue:78`（openPage 标签）→ 「智能体」 | 4 处编辑 |
| 构建 | `Plugins/Home/web && pnpm run build` | ✅ 450ms（vite 6.4.3，12 modules）|
| UI 复验 | `/home` guard-evaluate 全文检查 | `hasOldName:false, hasNewName:true`（3 处「智能体」）；截图 10 |
| 残留 | TodoTracker「本工具 AI Agent 执行」 | 登记小批（含 TodoBuiltInDispatchTests 断言耦合）|

## 3. 走查覆盖（主体来自 2026-11-02-maf-first-class 批，佐证截图 01–09）

命名/审批链/技能/记忆维度/本地模型（Ollama、LM Studio 类型 + 本地模式开关落库 + 恢复）/Scheduler AgentRun=3 建删实测/RouteMissingView 兜底/记忆纠正端点留痕——明细见当天日记「走查结果」节。截图目录 `ForgeSelf.Web/screenshots/dev/ai-agent-maf-20261102/`（01–11 共 11 张，已读图核对）。

## 4. 环境事实（影响可信度标注）

- 测试构建曾撞 dev 宿主 DLL 锁与一次 MSB4018 OOM（机器内存瞬时）；处置：dev-stack -Stop（自建实例，非用户 :51888）+ `dotnet build-server shutdown` + `-m:1`。绿灯结果在锁释放后的干净构建取得。
- 共享浏览器与并行 Stardust 走查会话互抢 tab：全部 UI 断言带 URL guard，API 断言独立于浏览器。
- 未跑：宿主前端 `pnpm run check/test`（本批未改 ForgeSelf.Web 源码，仅插件 web）、插件冒烟 e2e（仓库现无 `e2e/plugins/` 目录，以起源码走查第③步替代，已如实登记）。
