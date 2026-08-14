# 14-onboarding — 上手指南

> 状态：已实现（2026-08-12 反向更新，端口以代码为准）
> 最后更新：2026-08-12

## 1. 环境要求

| 依赖 | 版本 |
|------|------|
| .NET | 10（后端） |
| Node + pnpm | Node >= 20，pnpm（前端，非 npm） |
| 操作系统 | Windows（托盘桌面应用） |

## 2. 一键启动

```bash
.\start.ps1
```

- 后端：`http://localhost:7102`（默认端口，见 `ForgeSetting.PortNumber`）
- 前端：`http://localhost:7002`（dev server）

> ⚠️ 端口以代码为准。AGENTS.md 旧注 `7300/7380` 已过时，请勿沿用。

## 3. 先读文档（按认知顺序）

1. `docs/00-vision/` — 项目为什么存在、去哪（裁判依据）
2. `docs/01-architecture/overview.md` — 系统怎么搭
3. `docs/02-features/` — 各功能做成什么样
4. `AGENTS.md` — 怎么干活（Loop Engineering + 预飞铁律）
5. `docs/04-standards/engineering.md` — 代码必须长什么样

## 4. 常用命令

| 动作 | 命令 |
|------|------|
| 前端类型+lint | `cd OpenForgeSelf.Frontend && pnpm run check` |
| 前端单测 | `pnpm run test` |
| 前端 e2e | `pnpm run test:e2e` |
| 前端构建 | `pnpm run build`（注意 31 个预存类型错误，T032） |
| 后端构建 | `cd OpenForgeSelf.Backend && dotnet build` |
| 后端测试 | `cd OpenForgeSelf.Backend.Tests && dotnet test` |
| 一键构建发布 | `.\build.ps1` |

## 5. 第一次改代码

1. 收到任务 → 先写当天日记（`.forgeself/memory/YYYY-MM-DD.md`）+ 建 TODO（AGENTS.md §0 预飞铁律）；
2. 读相关 spec（`specs/NNN-*/`）+ 设计稿（`forgeself-design/`）+ 代码；
3. 改完跑对应 Verify 门禁；
4. 完成 → 更新 TODO + 日记「下一步」，可复用规律写 MEMORY.md。
