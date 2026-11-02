# Plan

> Task ID：PILOT-078｜执行模式：**Agent 团队**（用户指令「组建团队执行」）——Lead 写文档/审/验收，dev 实现，test 验证。

## 团队与写范围（隔离，共享目录）

| 成员 | 职责 | 写范围（workspace 前缀） |
| --- | --- | --- |
| lead（本会话） | 工件 00-04、任务板、Review、验收走查、插件文档、最终汇报 | `docs/ai/pilot/2026-10-30-imgateway-chat-list/**`、`Plugins/ImGateway/README.md`、`Plugins/ImGateway/docs/**`、TODO/日记 |
| im-dev | 后端 + 前端实现（FR-1..FR-6 除文档） | `Plugins/ImGateway/**`（不含 README.md / docs/，归 lead） |
| im-test | 单测 + e2e + 门禁执行与取证 | `ForgeSelf.Api.Tests/Plugins/ImGatewayTests/**`、`ForgeSelf.Web/e2e/plugins/im-gateway/im-gateway.spec.ts`、`.temp/tmp/**` 取证 |

- dev-stack 停/启：由 **lead** 统一执行并在 TODO 协作区登记（test/dev 需要构建窗口时经 lead）。
- 共享任务板：`team_task_create` 建任务并 `blocked_by` 编排；写范围按上表登记（advisory）。

## 步骤拆解（每步可独立验证）

1. **[dev] 后端消息层**
   a. `Data/Model.xml` 增 `ImGatewayMessage` 表 → `cd Plugins/ImGateway/Data && xcode Model.xml`（幂等；`ImGatewayMessage.Biz.cs` 手写便捷方法 `FindByChannelKeyPaged` 如需）。
   b. `Services/ImGatewayMessageStore.cs`（新）：`RecordInbound/RecordReply/RecordError`（try/catch + XTrace）；`DbSessionStore` 不动。
   c. `ImGatewayRouter`：三落库点接入（构造注入 store；ImGatewayPlugin DI 注册）。
   验证：`dotnet build` 过；后续单测覆盖（test 成员）。
2. **[dev] 查询端点 + 鉴权**
   a. `ImGatewayController`：类级 `[Authorize("ApiKeyPolicy")]`；`GET conversations`（会话×最新消息组装）；`GET messages`（游标分页，limit 钳制）。
   b. `plugin.json` Version 2.1.0 + Description。
   验证：build 过；带/不带 token 手测（curl）。
3. **[dev] 前端聊天式布局**
   a. `web/src/http.ts`：+`fetchConversations`/`fetchMessages`（类型 DTO）。
   b. `ImGatewayView.vue` 重构：双视图切换（会话/配置，配置内容原样迁移）；左栏 `ChatList`（轮询 12s 比对赋值）+ 右栏 `ChatThread`（气泡/错误标记/加载更多/增量轮询）；子组件落 `web/src/components/`（原生 HTML + token，禁 `<ElXxx>`，禁宿主 import；滚动容器子区块 flex-shrink:0）。
   验证：`cd Plugins/ImGateway/web && pnpm run build` 零错误；产物无宿主 import。
4. **[test] 单测**（`ForgeSelf.Api.Tests/Plugins/ImGatewayTests/`，守 PC-014 换库规范）：
   - `ImGatewayMessageStoreTests`：in/out/error 落库字段正确；异常吞掉不抛。
   - `ImGatewayRouterRecordTests`：scripted IChatCompletion 成功/失败两链路 → 落库条数与内容断言（含网关主链不受影响）。
   - `ImGatewayControllerQueryTests`：conversations 组装/排序/displayName；messages 游标/钳制/hasMore/400；鉴权 401/200。
   验证：`dotnet test --filter FullyQualifiedName~ImGatewayTests` 绿。
5. **[test] e2e 冒烟**：扩展 `im-gateway.spec.ts`——页面加载（左栏空态/配置切换/版本徽标/无致命报错/无未知 5xx）；保留既有用例语义。
   验证：`pnpm run test:e2e -- e2e/plugins/im-gateway/im-gateway.spec.ts` 绿。
6. **[lead] 验收走查**（dev-stack 起源码，含种子数据）：按 02 §走查符合性逐项 + 截图读图；插件 README + ui-design-brief。
7. **[lead] Review（06）+ 汇报（07 骨架）**：八问 + Final Decision；闸门2 交用户。

## 里程碑 / 依赖

```
T1(dev 后端) ─┬─→ T2(dev 端点) ─→ T3(dev 前端) ─→ [集成] ─→ T5(e2e) ─→ T6(走查) ─→ T7(review)
T4(test 单测) ┘（依据 02 契约与 dev 并行，集成时对齐）
```

## 风险与对策

| 风险 | 对策 |
| --- | --- |
| dev-stack DLL 锁（后端重建） | lead 统一停/启 + 协作区登记；前端 HMR 不受影响 |
| xcode 生成破坏既有实体 | 生成幂等 + 生成后 `git diff` 仅应出现新增文件/表 |
| `[Authorize]` 补加破坏 e2e | e2e helper 已带 token（先例 mcp-center）；test 成员跑全 spec 确认 |
| 前端重构破坏配置功能 | 配置视图整块迁移不改逻辑；e2e+走查双验 |
