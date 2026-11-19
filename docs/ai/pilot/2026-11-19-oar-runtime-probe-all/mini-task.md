# mini-task：Runtime 库存「一键探测」全部 Runtime

- **日期**：2026-11-19 ｜ **形态**：轻量（mini-task，≤3 业务文件）
- **闸门1 依据**：用户明文「加一下这个功能，做完之后提交全部，帮我合并到主分支」（本会话输入 N；功能 = 一键探测全部 Runtime）
- **适用问题类**：无（新功能，非缺陷/回归）
- **状态**：✅ 完成

## ① 意图（为什么）

Runtime 库存每卡仅有「检查」→ `POST /api/agent-hub/runtimes/{id}/probe`。本机多 Runtime 时须逐卡点击；用户要**一次发起对当前列表全部 Runtime 的真实探测**，与面板「刷新」（只 list）区分。不引入假成功路径。

## ② 规格要求（做什么 / 到什么程度）

1. Runtime 库存区提供主操作「一键探测」（文案可并列「全部检查」语义）：对当前 `productRuntimes` 列表（已剔除 scripted）逐条调用既有 `probeRuntime(id)`。
2. **不新增 batch API**（除非前端循环明显不足）；本批复用单 id probe。
3. 运行中：主按钮禁用；每卡展示进度态（探测中 / 成功 / 失败）；聚合提示（成功数/失败数）；失败保留真实错误，禁止 mock。
4. 并发上限（建议 3）避免打爆本机 CLI；单卡「检查」仍可用（非一键进行中时）。
5. 回写 `docs/02-features/032-agent-hub.md` §6.1 与 `Plugins/AgentHub/docs/ui-design-brief.md`。

### Interaction Design（交互设计）

| 触发 | 结果 / 状态 | 反馈 |
| --- | --- | --- |
| 点「一键探测」且列表非空 | 进入 `probingAll`；按并发池调用各 id probe；每卡 `running→ok|err` | 顶栏/提示条聚合「n 成功 · m 失败」；失败卡保留错误可展开 |
| 点「一键探测」且列表空 | 不发请求 | 提示「暂无 Runtime 可探测」 |
| 一键进行中再点 | 按钮 disabled | 文案「探测中…」 |
| 一键进行中点单卡「检查」 | disabled | 避免交错写同一 map |
| 面板「刷新」 | 仅 `listRuntimes`/`listSessions` | **不**触发 probe（与一键探测区分） |
| 单卡「检查」 | 与既有相同 | 展开该卡 installation/auth |

**五态**：加载=探测中徽标；空=无 Runtime 空态+刷新指引；出错=该卡 err + 聚合失败数；无权限=沿用现有 API 401 提示；正常=ok 展开 JSON。

### Failure Modes

| # | 失效模式 | 对策 |
|---|----------|------|
| F1 | 把「刷新」做成隐式全探测 | 按钮文案与位置独立；文档写明刷新≠探测 |
| F2 | 并发过高拖垮本机 CLI | 固定并发上限 3 |
| F3 | 部分失败却报全绿 | 聚合必须含失败计数；err 卡可见 |
| F4 | 引入 batch 端点假装成功 | 本批不新增 API；前端循环真实 probe |

## ③ 计划（涉及文件）

| 文件 | 动作 |
| --- | --- |
| `Plugins/AgentHub/web/src/AgentHubView.vue` | 状态 + `runProbeAllRuntimes` + 按钮/卡进度 UI |
| `docs/02-features/032-agent-hub.md` | §6.1 探测粒度改为「单卡 + 一键全探测（前端循环）」 |
| `Plugins/AgentHub/docs/ui-design-brief.md` | 交互表增加一键探测行 |

**不改**：`AgentHubRuntimesController`、Worker、Agent 卡「探测」、发布宿主进程。

## ④ 任务拆分

- [x] 日记 + TODO + 读规范/技能
- [x] 实现 UI 一键探测（并发 3）
- [x] 文档 §6.1 + ui-design-brief
- [x] `pnpm` 插件 web build/check；截图
- [x] Evidence/Review；提交推送 PR；合并栈→main

## ⑤ 验证（命令 + 判据）

```bash
cd Plugins/AgentHub/web && pnpm install && pnpm run build
# 若有 check/test 脚本则跑
```

判据：构建 0 error；UI 可见「一键探测」；点后各卡有进度；与「刷新」独立。e2e 若因 Linux 无 `ForgeSelf.exe` 阻塞则记 Evidence Unknown/Blocked，不伪造成功。

## Expected Files

- `Plugins/AgentHub/web/src/AgentHubView.vue`
- `docs/02-features/032-agent-hub.md`
- `Plugins/AgentHub/docs/ui-design-brief.md`
- `docs/ai/pilot/2026-11-19-oar-runtime-probe-all/mini-task.md`
- `docs/ai/pilot/2026-11-19-oar-runtime-probe-all/04-task.md`
- `docs/ai/pilot/2026-11-19-oar-runtime-probe-all/05-evidence.md`
- `docs/ai/pilot/2026-11-19-oar-runtime-probe-all/06-review.md`
- `docs/ai/pilot/2026-11-19-oar-runtime-probe-all/07-final-report.md`
