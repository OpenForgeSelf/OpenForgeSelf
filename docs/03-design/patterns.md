# 03-design — 设计规范

> 状态：部分（核心模式已下沉到具体功能文档，2026-08-12）
> 最后更新：2026-10-06

本目录收纳**跨功能共享的设计模式**。分两部分：

## 一、交互模式库（跨页面一致性单一来源）

[`interaction-patterns.md`](interaction-patterns.md) —— 列表页 / 表单 / 空态 / 加载 / 错误 / 确认弹窗 / 无权限，每种固定一份写法。
**新增通用交互模式必须先登记到该文件，再在页面使用**；UI 任务的 Spec 必须引用「设计稿页面 + 交互模式名 + token」。

## 二、已下沉到具体功能文档的核心模式

| 模式 | 落点文档 |
|------|----------|
| 零自定义 token 样式体系 / 背景图 `<img>` 方案 / canvas 取色 | `02-features/006-background-image.md` |
| 前端组件/导航/API 封装规范 | `04-standards/engineering.md` §1 |
| 统一 AI 网关多风格归一（OpenAI / Anthropic / Responses + Agent Framework 实验接口）/ 模型前缀路由 | `02-features/004-provider-models-integration.md` |
| 聊天会话聚合（ChatSession/ChatTurn） | `02-features/010-chat-session-aggregation.md` |
| 多模态图片缓存（分区键/原子写/容错） | `02-features/011-multimodal-image-cache.md` |

新功能若提炼出可复用的通用设计模式，在此补独立小节，并同步 `docs/README.md` 索引。
