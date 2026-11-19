# AgentHub RuntimeWorker

AgentHub 内部 Node Worker：经 **JSON-RPC / JSONL over stdio** 调用锁定的 `@botiverse/oar@0.109.0`。

## 要求

- **Node.js ≥ 24**（OAR engines）
- 不在启动时自动 `install` 第三方 Agent；只读探测可用 `runtime.installation` / `runtime.installPlan`

## 命令

```bash
# 使用 Node ≥24
pnpm install
pnpm run smoke   # P0 探针 12 步（scripted，零第三方二进制）
pnpm run build
pnpm start       # 读 stdin JSONL，写 stdout JSONL；日志走 stderr
```

## 协议（节选）

| method | 说明 |
| --- | --- |
| `worker.hello` / `ping` | 探活 + 协议版本 |
| `runtime.list` | Registry 列表（含 `scripted` + OAR defaultRuntimes） |
| `runtime.installation` | 只读安装探测 |
| `runtime.auth` / `runtime.models` / `runtime.usage` | 需可用安装；scripted 返回 `not_supported` |
| `session.open` / `session.resume` | 打开 / 恢复；返回宿主 `sessionKey` |
| `session.prompt` / `session.deliver` / `session.control` | 输入与 abort/cancel |
| `session.records` / `session.dispose` | 审计记录 / 释放 |
| 通知 `session.event` | 会话事件推送（无 `id`） |

真源：`docs/01-architecture/agenthub-oar-runtime.md` §8；ADR：`docs/07-decisions/007-oar-agenthub-runtime.md`。
