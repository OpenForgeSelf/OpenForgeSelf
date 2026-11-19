# 14-onboarding — 上手指南

> 状态：已实现（2026-08-12 反向更新，端口以代码为准）
> 最后更新：2026-11-19

## 1. 环境要求

| 依赖 | 版本 |
|------|------|
| .NET | 10（后端） |
| Node + pnpm | Node >= 20，pnpm（前端，非 npm） |
| 操作系统 | Windows（托盘桌面应用） |

### 1.1 Linux 上编不过宿主（2026-11-19 实测）

宿主 `ForgeSelf.Api/ForgeSelf.Api.csproj` 把 `TargetFramework` 钉成 `net10.0-windows`，并打开 `UseWindowsForms`（托盘用 H.NotifyIcon 和 `System.Windows.Forms`，见 `ForgeSelf.Api/Program.cs` 的 `StartTrayIcon`）。Linux 上直接 `dotnet build` 报 **NETSDK1100**。根启动器 `ForgeSelf.Bootstrapper` 另有 `RuntimeIdentifier=win-x64` 和 `Microsoft.WindowsDesktop.App`，同样不能在 Linux 上编。

两条路，解决的不是同一件事：

| 目的 | 做法 | 结果 |
|------|------|------|
| 只求在 Linux 上**编译出** Windows 宿主 | 构建时加 `-p:EnableWindowsTargeting=true`（或写进上述两个 csproj） | 实测 SDK 10.0.115：**0 error**。产物仍在 `bin/Debug/net10.0-windows/`，运行时框架是 `Microsoft.WindowsDesktop.App` 10.0.0 |
| 要在 Linux 上**把 Kestrel 跑起来** | 非 Windows 构建改用 `net10.0`，关掉 `UseWindowsForms`，源码里不要引用 `System.Windows.Forms` / H.NotifyIcon | 交叉编译帮不上。`dotnet exec` 该产物退出码 150：本机只有 `Microsoft.NETCore.App` 和 `Microsoft.AspNetCore.App`，没有 WindowsDesktop，且这个框架没有 Linux 运行时 |

Windows 上的发行形态不变：业务层仍是 `net10.0-windows`，托盘仍在业务层进程里（`docs/04-standards/packaging-upgrade-backup.md` §1.6「不拆独立程序」）。非 Windows 跳过托盘，走已有的 `FORGESELF_NO_TRAY=1`（`Program.cs` 的 `StartTrayIcon`）。根启动器继续只在 Windows 上编，它是安装包入口，不是开发时的 API。

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
| 前端类型+lint | `cd ForgeSelf.Web && pnpm run check` |
| 前端单测 | `pnpm run test` |
| 前端 e2e | `pnpm run test:e2e` |
| 前端构建 | `pnpm run build`（注意 31 个预存类型错误，T032） |
| 后端构建 | `cd ForgeSelf.Api && dotnet build` |
| 后端测试 | `cd ForgeSelf.Api.Tests && dotnet test` |
| 一键构建发布 | `.\build.ps1` |

## 5. 第一次改代码

1. 收到任务 → 先写当天日记（`.forgeself/memory/YYYY-MM-DD.md`）+ 建 TODO（AGENTS.md §0 预飞铁律）；
2. 读相关工件（`docs/ai/pilot/YYYY-MM-DD-<task-id>/`，同域历史任务看 02-spec/03-plan）+ 设计稿（`forgeself-design/`）+ 代码；
3. 改完跑对应 Verify 门禁；
4. 完成 → 更新 TODO + 日记「下一步」，可复用规律写 MEMORY.md。
