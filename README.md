<div align="center">

# 铸己匣 OpenForgeSelf

**以器铸己，日积寸进**

一个可无限扩展的个人全能工具箱：插件化架构 × AI Agent 融合 × 能力沉淀

![Platform](https://img.shields.io/badge/platform-Windows-blue) ![.NET](https://img.shields.io/badge/.NET-10.0-512BD4) ![Vue](https://img.shields.io/badge/Vue-3.5-42B883) ![License](https://img.shields.io/badge/license-MIT-green)

</div>

---

## 简介

**铸己匣**（OpenForgeSelf）是一个运行在 Windows 桌面的个人全能工具箱（托盘常驻 + 自动更新）。寓意「用工具锻造自己」：所有功能以**插件**形式接入，AI Agent 作为"特殊插件"可以调用其他所有插件——用自然语言操控工具、自动规划执行复杂任务链，随使用不断沉淀属于自己的能力资产。

## 核心特性

- **插件化架构** — 统一插件接口 + 热插拔：新增功能不用改核心代码，把插件放进 `Plugins/` 目录即可。
- **AI Agent 融合** — AI 代理核心可调用 50+ 工具函数，自动规划、执行复杂任务链，支持多模型接入。
- **能力沉淀** — 长期记忆、个人能力画像与成长曲线，使用数据全部本地记录。
- **对外扩展** — MCP 中心对外暴露宿主全部工具、可接入外部 MCP 服务器（支持 MCP 2.0 协议协商）；IM 网关统一接入企业微信 / 公众号 / 飞书 / 钉钉。
- **本地优先** — SQLite 本地存储，敏感凭据（AI 提供方 ApiKey 等）AES-256 加密落库，接口仅返回掩码。
- **工程化纪律** — AI-Native 开发闭环（`AGENTS.md` + `docs/`），打 tag 自动发布（CI 打包 GitHub Release）。

## 功能模块

> 清单派生自单一真源 [`ForgeSelf.Web/src/data/features.ts`](ForgeSelf.Web/src/data/features.ts)（新增功能须先在该文件登记，再用 `pnpm run check:features` 校验代码与清单一致性——幻影登记与孤儿产物两类脱节都是硬失败；该脚本目前**未接入 CI**，靠本地与提交前手工跑）。

| 模块 | 分类 | 说明 |
|---|---|---|
| AI Agent | AI | AI代理核心，自然语言操控工具，自动规划执行复杂任务链 |
| 快捷链接 | 工具 | 一键打开常用网址，支持分组管理 |
| 文本工具 | 工具 | 格式化、编码转换、哈希计算等文本处理工具集 |
| 文件工具 | 工具 | 批量重命名、清理、压缩解压等文件管理工具 |
| 系统监控 | 系统 | CPU、内存、磁盘实时监控，进程管理 |
| 工作流引擎 | 编排 | 多工具编排，任务链自动执行，支持条件分支 |
| 定时任务 | 编排 | Cron表达式管理，定时执行脚本和工作流 |
| 脚本运行器 | 开发 | PowerShell/Python/Node脚本运行，支持AI生成 |
| 开发者工具箱 | 开发 | JSON/YAML/Base64/哈希/正则/时间戳等25+开发工具 |
| 核心聊天 | AI | 多轮对话聊天页面，对接 AI 提供方实时流式回复 |
| 聊天记录 | AI | 查看与管理历史对话记录，支持回顾与检索 |
| 提示词 | AI | 管理可复用的提示词模板 |
| 技能 | AI | 管理与编排 Agent 技能 |
| MCP 中心 | AI | 对外暴露 MCP 端口（单一万能工具转发宿主全部工具）+ 外部 MCP 服务器管理 |
| 记忆 | AI | 长期记忆系统，跨会话沉淀知识与上下文 |
| 多 Agent 管理 | AI | 管理多个专业 Agent 与其协作 |
| 代码片段库 | 开发 | 个人代码片段管理与复用 |
| 待办事项 | 工具 | 本地待办清单，跟踪个人任务 |
| 个人档案 | 系统 | 个人能力画像与成长曲线，沉淀使用数据 |
| 插件管理 | 系统 | 浏览、安装、更新与管理插件生态 |
| 设置 | 系统 | AI 提供方、API 服务、背景图与端口安全配置 |
| IM 网关 | AI | 多渠道 IM 网关：企业微信/公众号/飞书/钉钉消息统一接入 AI Agent |

## 快速开始

**环境要求**：Windows 10+ · .NET 10 SDK · Node ≥ 20 + pnpm

```powershell
git clone https://github.com/OpenForgeSelf/OpenForgeSelf.git
cd OpenForgeSelf

# 一键构建发布：前端构建 → 输出到后端 wwwroot → 发布后端
.\build.ps1

# 或开发模式：
# 前端（http://localhost:7002）
cd ForgeSelf.Web && pnpm install && pnpm dev
# 后端（http://localhost:7102）
dotnet run --project ForgeSelf.Api
```

- 端口可覆盖：环境变量 `FORGESELF_PORT`（优先）或 `--server-port` 参数，覆盖即落盘、重启一致。
- 稳定版从 [Releases](https://github.com/OpenForgeSelf/OpenForgeSelf/releases) 下载（打 tag 自动发布）。
- 运行测试：后端 `dotnet test`；前端 `cd ForgeSelf.Web && pnpm run check && pnpm run test`。

## 插件开发

一个插件 = 一个 `plugin.json` 清单 + 后端程序集 +（可选）前端视图，参考模板 [`Plugins/SamplePlugin/`](Plugins/SamplePlugin/)。宿主通过清单注册插件、动态注册路由；对外工具经 MCP 中心暴露。

上手路径：[`docs/14-onboarding/getting-started.md`](docs/14-onboarding/getting-started.md) → [`AGENTS.md`](AGENTS.md)（AI 协作工作手册）→ [`docs/01-architecture/overview.md`](docs/01-architecture/overview.md)。

## 文档

文档中心 [`docs/README.md`](docs/README.md) 按"30 秒定位"组织，共 20 个编号分类：愿景 [`00-vision/`](docs/00-vision/)、架构 [`01-architecture/`](docs/01-architecture/)、规范 [`04-standards/`](docs/04-standards/)、安全基线 [`08-security/`](docs/08-security/baseline.md)、运维 [`09-operations/`](docs/09-operations/) 等。

## 路线图

见 [`铸己匣-OpenForgeSelf-演进路线图.md`](铸己匣-OpenForgeSelf-演进路线图.md) 与 [`docs/15-roadmap/`](docs/15-roadmap/)。前四个阶段（核心基建 → 插件化架构 → 工具集深化 → 创造工具）已完成；**阶段五「AI Agent 觉醒」**（多 Agent 协作、长程记忆、主动规划、个性化学习）规划中。

## 许可证

[MIT](LICENSE) © 2026 笑笑
