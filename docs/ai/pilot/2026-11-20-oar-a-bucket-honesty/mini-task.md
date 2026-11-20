# mini-task：OAR A-bucket honesty（Watch poll 契约 + interrupt fixture + 一键探测 e2e）

- **日期**：2026-11-20 ｜ **形态**：轻量（mini-task；文档诚实 + 测试/e2e，不实现 #63 SSE）
- **闸门1 依据**：父代理任务明文 A-bucket scope（#22/#28/#25/#78）；Gap plan `/opt/cursor/artifacts/oar-78-gap-action-plan.md`；Gate1 = already ordered
- **适用问题类**：无新增 PC（契约诚实/守卫测试；非生产 early-Exit——sibling `oar-prod-honesty` 另修 #20/#39/#50/#70）
- **状态**：✅ 完成

## ① 意图（为什么）

Checklist #22/#28 把 Watch 标成可 DONE 的前提是：**文档与 AC 承认 shipped 面 = 轮询最小面**，不是 unified SSE（#63 → C deferred）。#25 需 crash/interrupt fixture：Interrupted ≠ Succeeded；Session resume ≠ process revive。#78 需正式 e2e 点「一键探测」并证明 refresh ≠ probe。

## ② 规格要求（做什么 / 到什么程度）

1. `docs/02-features/032-agent-hub.md`（及本 pilot）写明：`IAgentRuntime.WatchTaskAsync` + `sessions/.../stream` = **poll 最小面**；tasks 控制器仍有任务 SSE；**禁止**宣称 unified Watch SSE；#63 缓做。
2. 测试断言 poll 契约：Submit / Get / Watch（可取消）/ Cancel / 续接（SendInput）可用；Watch 经轮询增量，Cancel token 可中断。
3. Interrupt fixture：遗留 Running → RecoverOrphans → `Interrupted` 且 `IsTerminal` 且 **≠ Succeeded**；Open Session → InterruptOpenSessions → `Interrupted`；再 Open 新 SessionKey / Generation+1 ≠ 自动复活旧进程。
4. Playwright：`e2e/plugins/agent-hub` 增加「一键探测」用例；Linux 无可用宿主则写 FAIL 证据 + vitest 并发池/消息格式最小测 + 记 D。
5. **不改** OarTransport 50ms / handlers bundled（sibling 范围）；不实现 #63。

### Interaction Design（交互设计）

| 触发 | 结果 / 状态 | 反馈 |
| --- | --- | --- |
| 读功能文档 § Watch | 明确 poll 契约 + 非 SSE | 无 UI 变 |
| 点「一键探测」 | 并发≤3 调 per-id probe | 聚合成功/失败；与「刷新」分离（既有 UI） |
| 宿主重启收尾 | 遗留 Task/Session → Interrupted | 不可显示为 Succeeded / 假活进程 |

**五态**：本批以契约/测试为主；一键探测五态沿用既有 probe-all pilot。无新增无界面工具声明需求（有页面，交互表见上）。

### Failure Modes

| # | 失效模式 | 对策 |
|---|----------|------|
| F1 | 文档写 SSE 已完成 | 032 诚实节 + AC 标明 poll；#63 not-taken |
| F2 | Interrupt 后仍可 Succeeded | fixture 断言 Interrupted 终态且 ≠ Succeeded |
| F3 | e2e 只点刷新当探测 | 断言 `/probe` 请求仅在一键探测后出现 |
| F4 | 误改 sibling transport | Forbidden；stash 隔离 |

## ③ 计划（涉及文件）

| 文件 | 动作 |
| --- | --- |
| `docs/02-features/032-agent-hub.md` | 增 §6.2 Watch/stream 诚实契约 |
| `docs/ai/pilot/2026-11-19-oar-agenthub-runtime/02-spec.md` | AC-R3 诚实标注强化（poll） |
| `ForgeSelf.Api.Tests/Plugins/AgentHub/AgentHubRuntimeProviderTests.cs` | Watch poll + Submit/Get/Cancel/续接 |
| `ForgeSelf.Api.Tests/Plugins/AgentHub/AgentHubInterruptFixtureTests.cs` | #25 crash/interrupt |
| `Plugins/AgentHub/web/src/probeAllRuntimes.ts` (+ spec) | 并发池纯函数 + vitest |
| `Plugins/AgentHub/web/src/AgentHubView.vue` | 复用纯函数 |
| `ForgeSelf.Web/e2e/plugins/agent-hub/agent-hub.spec.ts` | #78 一键探测 |

**不改**：`OarTransport.cs` early-Exit、`handlers.ts` bundled、统一 SSE。

## ④ 任务拆分

- [x] 日记 + TODO + 分支
- [x] 文档诚实 #22/#28
- [x] Watch/poll + interrupt 测试
- [x] probeAll 提取 + e2e/#78
- [x] Verify + Evidence/Review + commit/push/PR

## ⑤ 验证（命令 + 判据）

```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~AgentHubRuntimeProviderTests|FullyQualifiedName~AgentHubInterruptFixtureTests|FullyQualifiedName~AgentHubDelegationRuntimeTests.G5"
cd Plugins/AgentHub/web && pnpm exec vitest run
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/agent-hub/agent-hub.spec.ts -g "一键探测"
```

判据：过滤测绿；vitest 绿；e2e 若宿主可起则绿，否则 FAIL 证据落 `/opt/cursor/artifacts/oar-a-bucket-honesty/` 并标 D。

## Expected Files

见上表 + `docs/ai/pilot/2026-11-20-oar-a-bucket-honesty/{mini-task,05-evidence,06-review,07-final-report}.md`
