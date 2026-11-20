# mini-task：会话独立页签（Runtime 检测与会话分离）

- **日期**：2026-11-20 ｜ **形态**：轻量（mini-task）
- **闸门1 依据**：用户明文「检测列表列出来了很多 agent，下面还有显示会话，就太长了，单独一个页签展示会话」
- **适用问题类**：无（UX 调整）
- **状态**：🔄 进行中

## ① 意图

原「Runtime / Session」单页签内 Runtime 库存网格很长，下方再叠 Session 操作 → 一屏过长、亲密性差。会话应独立页签。

## ② 规格

1. 顶栏拆为「Runtime 检测」与「会话」两个页签。
2. Runtime 检测页：仅库存 + 一键探测/检查/安装；提示会话在「会话」页签。
3. 会话页：仅会话列表 + Steer/Abort/Close/Records；**不含**一键探测与 Runtime 网格。
4. 回写 `032-agent-hub.md` §6.1 与 `ui-design-brief.md`；更新 agent-hub e2e 页签选择器。

### Interaction Design

| 触发 | 结果 |
| --- | --- |
| 点「Runtime 检测」 | 仅见 Runtime 库存；可一键探测 |
| 点「会话」 | 仅见会话操作；无「一键探测」按钮 |
| 会话空态 | 引导去委派；不展示 Runtime 卡 |

## ③ 计划（文件）

| 文件 | 动作 |
| --- | --- |
| `Plugins/AgentHub/web/src/AgentHubView.vue` | TabKey + 拆分两 section |
| `ForgeSelf.Web/e2e/plugins/agent-hub/agent-hub.spec.ts` | 页签选择器 |
| `docs/02-features/032-agent-hub.md` | §6.1 页签分区 |
| `Plugins/AgentHub/docs/ui-design-brief.md` | 旅程 4/5 |

## ④ 验证

```bash
cd Plugins/AgentHub/web && pnpm run build
# e2e: agent-hub Session / 一键探测 用例选择器对齐
```

## Expected Files

见同目录 `04-task.md`。
