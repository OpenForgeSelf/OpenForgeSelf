---
title: "铸己匣 OpenForgeSelf 功能设计"
version: "v0.2"
last_updated: "2026-08-07"
status: "active"
type: "design"
req_ids: ["REQ-001", "REQ-002", "REQ-003", "REQ-004", "REQ-005", "REQ-006", "REQ-007", "REQ-008", "REQ-009", "REQ-010", "REQ-011", "REQ-012", "REQ-013", "REQ-014", "REQ-015", "REQ-016", "REQ-017", "REQ-018", "REQ-019"]
# 功能清单（单一真源 = 前端 src/data/features.ts；下表为其镜像，新增功能须先登记 features.ts）
modules: ["ai-agent", "quick-links", "text-tools", "file-tools", "system-monitor", "workflow", "scheduler", "script-runner", "dev-tools", "chat", "chat-records", "prompts", "skills", "mcp", "memory", "agents", "code-snippets", "todo", "profile", "plugins", "settings"]
affects: ["src/", "OpenForgeSelf.Backend/", "OpenForgeSelf.Frontend/"]
author: "human"
reviewed_by: ""
---

# 铸己匣 | OpenForgeSelf

> **Slogan**: 以器铸己，日积寸进
>
> **寓意**: 用工具锻造自己，能力随使用不断沉淀、进化、扩展。

---

## 一、项目定位

个人全能工具箱，一个可无限添加工具、可自定义、不断沉淀能力、伴随使用者成长、不断自我迭代的瑞士军刀级应用。

---

## 二、核心创新

### 2.1 插件化架构

- **统一插件接口**: 所有功能（AI代理、工具、脚本、定时任务）通过同一接口扩展
- **热插拔**: 新增功能无需修改核心代码，只需将插件放入 Plugins 文件夹
- **类型隔离**: AI 代理与普通工具统一管理，互不干扰

### 2.2 能力沉淀机制

- 工具使用数据本地记录
- 个人工作流自动沉淀
- 随使用时间增长，工具越来越懂你

### 2.3 AI 代理融合

- AI 作为特殊插件，可调用其他所有插件
- 支持自然语言操控工具
- 自动规划、执行复杂任务链

---

## 三、功能理念

### 3.1 无限扩展

想加新功能 = 直接加一个插件文件，不用改主程序代码。

### 3.2 自我迭代

越用越强，工具不断进化，能力持续沉淀。

### 3.3 以器铸己

工具不是目的，锻造自己的能力才是目的。每一次使用都在积累属于自己的能力资产。

---

## 四、功能模块

> 本表为 `OpenForgeSelf.Frontend/src/data/features.ts` 的镜像。新增功能必须先在该文件登记（含 `signals` 代码产物键），再由 `scripts/check-features.mjs` 在 CI 中校验代码与清单一致性。**单一真源以 `features.ts` 为准**，本表仅作文档展示。

| 模块 | 分类 | 说明 |
|------|------|------|
| AI 代理 | AI | 自然语言操控工具，自动规划执行复杂任务链（50+ 工具函数） |
| 核心聊天 | AI | 多轮对话聊天页面，对接 AI 提供方实时流式回复 |
| 聊天记录 | AI | 查看与管理历史对话记录，支持回顾与检索 |
| 提示词 | AI | 管理可复用的提示词模板 |
| 技能 | AI | 管理与编排 Agent 技能 |
| MCP 工具 | AI | 接入 MCP 工具服务器，扩展 Agent 工具集 |
| 记忆 | AI | 长期记忆系统，跨会话沉淀知识与上下文 |
| 多 Agent 管理 | AI | 管理多个专业 Agent 与其协作 |
| 快捷链接 | 工具 | 分类管理常用网址，一键打开 |
| 文本工具 | 工具 | 格式化、编码转换、哈希计算等文本处理 |
| 文件工具 | 工具 | 批量重命名、清理、压缩解压等文件管理 |
| 待办事项 | 工具 | 本地待办清单，跟踪个人任务 |
| 系统监控 | 系统 | 进程、网络、CPU、内存实时监控 |
| 个人档案 | 系统 | 个人能力画像与成长曲线，沉淀使用数据 |
| 插件商店 | 系统 | 浏览、安装、更新与管理插件生态 |
| 设置 | 系统 | AI 提供方、API 服务、背景图与端口安全配置 |
| 工作流引擎 | 编排 | 多工具编排，任务链自动执行，支持条件分支 |
| 定时任务 | 编排 | Cron 表达式支持，自动化执行 |
| 脚本运行器 | 开发 | PowerShell/Python/Node 脚本运行，支持 AI 生成 |
| 开发者工具箱 | 开发 | JSON/YAML/Base64/哈希/正则/时间戳等 25+ 开发工具 |
| 代码片段库 | 开发 | 个人代码片段管理与复用 |

---

## 五、技术选型

| 层级 | 技术 |
|------|------|
| 前端 | Vue 3 + TypeScript |
| 后端 | .NET 10 + Mircosoft Agent Framework |
| 通信 | REST API + WebSocket |

---

*文档生成时间: 2026年5月8日*