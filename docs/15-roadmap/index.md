# 15-roadmap — 路线图（执行层）

> 状态：部分（ backlog 以 TODO.md 为准，2026-08-12）
> 最后更新：2026-08-12

> 本目录排"下一步做什么"。当前**活队列在 `TODO.md`**（消费式管理，完成即移除），不再重复搬运。

## 当前已知 backlog（摘自 TODO.md，未完项）

| 项 | 优先级 | 说明 |
|----|--------|------|
| 聊天界面模型选择动态化 | P1 | ChatView/AgentView 模型名写死，未从 `/api/ai-models` 加载 |
| 聊天请求模型关联梳理 | P1 | chatApi 请求体契约与 `/v1` 网关路由关系待确认 |
| 网关上游 4xx 透传 | P2 | OpenAIChatController 把上游 400/401 包装成 500，应透传 |
| 测试连接 15s 超时偏小 | P2 | LM Studio 首载模型响应慢于 15s |
| 内嵌插件无法 enable | T032 阻塞 | EntryAssembly 指向主程序集，PluginManager 找不到 dll |
| 其他插件分页 total=0 | T032 | RetrieveTotalCount 未开启（TodoTracker 已修） |
| pnpm build 31 个预存类型错误 | T032 | check 通过但 build 失败，需独立修复 |

## 下一步决策

新功能提案评审：先对照 `00-vision/03-principles.md`（裁判），再排期到此，落地写 `02-features/`。
