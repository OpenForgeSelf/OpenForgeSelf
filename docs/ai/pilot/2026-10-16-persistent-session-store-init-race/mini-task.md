# mini-task：避免 SessionEvent 首次建表竞态

- 日期：2026-10-16
- 类型：轻量缺陷修复（首次持久化写入与数据库初始化并发）
- 来源：输入2，AIAgent 冒烟 e2e 发现 SSE 返回 SQL 错误

## Intent / Spec

真实 e2e 的流式端点返回 HTTP 200/SSE，但首个事件是 `type=error`，后端日志为 `SessionEvent 数据库初始化失败: database is locked`，随后写入报 `no such table`。`PersistentSessionStore.EnsureTable` 忽略建表失败并把实例标成已初始化。

## Scope

- 允许：`ForgeSelf.Api/Services/PersistentSessionStore.cs`、AIAgent e2e 结构化响应断言、本 pilot 工件。
- 保持持久化表结构和事件语义不变；不碰正在运行的宿主。

## Acceptance Criteria

- 首次建表调用按实例串行化；短暂 SQLite 锁可重试，持续失败不得继续写入或谎报初始化成功。
- e2e 断言 SSE 有结构化事件且不含 `type=error`。
- 后端 AIModel 定向测试、前端全量测试、AIAgent 单条冒烟 e2e 通过。

## Plan / Task

1. 给惰性建表加初始化锁与有限重试，失败后显式抛错。
2. 加强现有流式 e2e 的错误事件断言。
3. 重跑相关验证并记录 Evidence/Review/Final Report。

## Gate 1

用户指令「剩下的看看还有什么问题，解决掉并验收提交」授权修复当前工作区验收中发现的相关缺陷；本修复仅处理首次初始化竞态，不改变事件表结构。
