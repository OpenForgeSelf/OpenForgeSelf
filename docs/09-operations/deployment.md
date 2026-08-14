# 09-operations — 运维手册

> 状态：已实现（2026-08-12 反向更新）
> 最后更新：2026-08-12

## 1. 本地开发启动

```bash
# 一键启动（后端 7102 + 前端 7002）
.\start.ps1
```

- 后端默认端口：`ForgeSetting.PortNumber = 7102`（运行期可改，重启生效）；
- 前端 dev server 默认 `7002`。

## 2. 构建与发布

```bash
# 一键构建：前端 → 输出后端 wwwroot → 发布后端
.\build.ps1
```

构建要点（代码事实）：
- 前端 `vite.config.ts`：`outDir: ../OpenForgeSelf.Backend/wwwroot`，**`emptyOutDir: false`**——本环境 safe-delete shim 拦截 Vite 删目录（`wrappedRmSync` 抛错致构建中断），故不清旧产物；
- 构建前可手动清 `OpenForgeSelf.Backend/wwwroot/assets` 避免旧 hash 累积；
- 前端验证门禁：`pnpm run check`（vue-tsc + eslint）+ `pnpm run test`（vitest）；
- 后端验证门禁：`dotnet build` + `dotnet test`。

> ⚠️ 代码侧已知残留：`vite.config.ts` 的 dev proxy 目标仍指向 `51888`（后端端口从 51888 迁移到 7102 时未同步），属死端口。本地 dev 代理需修正该 proxy 目标为 `7102`；本文档所述运行端口 7102/7002 为正确事实，与此残留无关。

## 3. 服务管理（托盘 / Windows）

- 托盘常驻：`Services/TrayIconManager.cs`，右键菜单检查更新/退出；
- 自更新：`Services/UpdateChecker.cs`（检查）+ `Services/UpdateService.cs`（实际下载/应用编排）+ `TrayProcessStarter.cs`，操作记录落 `UpdateTrace`；
- 进程锁注意：运行实例占用 `OpenForgeSelf.exe/dll` 时，`dotnet build` 复制阶段会 CS2012 失败——需停止进程或构建到独立输出目录。

## 4. 数据库

- SQLite 文件：`OpenForgeSelf.Backend/bin/Debug/net10.0-windows/Data/OpenForgeSelf.db`；
- XCode 自动建表（`DAL.Migration` 默认 On）；`DAL.CheckDatabase()` 主动建库；
- 图片识别缓存：`Data/ImageRecognitionCache/<会话键>/<sha256>.json`。

## 5. 故障恢复 / 备份

- 单密钥方案无自动轮换：更换加密密钥后旧密文失效，需重新加密存量（见 `02-features/100`）；
- 数据库文件即数据源，备份 = 复制 `.db` 文件（停机或事务间隙）。
