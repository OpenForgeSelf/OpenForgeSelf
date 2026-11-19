# AgentHub RuntimeWorker

AgentHub 内部 Node Worker：经 **JSON-RPC / JSONL over stdio** 调用锁定的 `@botiverse/oar@0.109.0`。

## 要求

- **Node.js ≥ 24**（`engines.node`；OAR 0.109 要求）
- 包管理：**pnpm**（在本目录执行，勿用 npm 另起 lock）
- 不在启动时自动 `install` 第三方 Agent；只读探测可用 `runtime.installation` / `runtime.installPlan`

## 命令

```bash
# 使用 Node ≥24（PATH 优先指向 ≥24 的 node）
pnpm install
pnpm run smoke   # P0 探针 12 步（scripted，零第三方二进制）
pnpm run build
pnpm start       # 读 stdin JSONL，写 stdout JSONL；日志走 stderr
```

## 运维 / CI 说明（P4）

| 场景 | 做法 |
| --- | --- |
| 本机开发 | 确认 `node -v` ≥ 24；本目录 `pnpm install && pnpm run smoke` |
| Cloud / CI 默认 Node 22 | **另备** Node ≥24 工具链后再跑本目录 smoke/build；不要为此把仓根前端 Node 引擎整体抬到 24 |
| 宿主拉起 | `OarWorkerClient` + `OarWorkerClientOptions.WorkerEntryPath` 指向本 Worker 入口；空路径 = Worker 不可用，**Cli Transport 须仍可降级 Submit** |
| 发布 | 随 AgentHub 插件版本走（`dist/` 现编或随包）；宿主 zip 结构真源见 `docs/04-standards/packaging-upgrade-backup.md`；契约摘要见架构真源 §13 |
| 禁止 | 绝对路径写入文档/代码；启动时自动 install 第三方 Agent；停/杀用户运行中的宿主 |

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

真源：`docs/01-architecture/agenthub-oar-runtime.md` §8 / §13；ADR：`docs/07-decisions/007-oar-agenthub-runtime.md`。
