# 配置项参考

> 项目使用 `appsettings.json` + 环境覆盖（`appsettings.Development.json` / `appsettings.Production.json`）管理配置。
> 配置通过 `IConfigurationService` 注入到各服务。
> 最后更新：2026-09-27

## 完整配置结构

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=...;"
  },
  "AI": {
    "ApiEndpoint": "http://localhost:1234/v1",
    "ApiKey": "",
    "ModelName": "llama-3.2-1b-instruct",
    "MaxTokens": 4096,
    "Temperature": 0.7,
    "TimeoutSeconds": 300
  },
  "Update": {
    "Provider": "github",
    "GitHubApiUrl": "https://api.github.com",
    "GitHubRepo": "OpenForgeSelf/OpenForgeSelf",
    "ServerUrl": "",
    "Channel": "stable",
    "CheckIntervalMinutes": 60,
    "CheckTimeoutSeconds": 15,
    "DownloadTimeoutSeconds": 600
  },
  "Service": {
    "Port": 7102,
    "PipeName": "ForgeSelf",
    "AutoStartTray": true,
    "EnableWebPort": true,
    "WebPort": 7002,
    "WebPortToken": ""
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Http": { "Url": "http://*:7102" }
    }
  }
}
```

## 配置节详解

### ConnectionStrings

> 数据库连接路径的**唯一真源是代码**（`ForgeSelf.Api/Data/XCodeConfig.cs`），不是配置节。

| 键 | 默认值 | 说明 |
|---|--------|------|
| `ConnectionStrings:{连接名}` | 无（无需配置） | 可选覆盖。连接名取自 `XCodeConfig.DbFiles`：`ForgeSelf` + 各插件库名。**仅当 `Data Source=` 为绝对路径时生效**；相对路径会被忽略并输出告警（否则数据库会落到程序目录，发布目录下常因目录不存在报 SQLite Error 14） |

落盘规则（由 `IDataLocationService` 按运行形态决定数据根）：

| 运行形态 | 数据根 | 宿主库 | 插件库 |
|----------|--------|--------|--------|
| 开发（`ASPNETCORE_ENVIRONMENT=Development`） | `{程序目录}/Data` | `Data/ForgeSelf.db` | `Data/Plugins/{插件Id}/{库名}.db` |
| 发布 exe / Windows 服务 | `~/.forgeself` | `~/.forgeself/ForgeSelf.db` | `~/.forgeself/plugins/{插件Id}/{库名}.db` |

### AI — AI 网关配置

| 键 | 类型 | 默认值 | 说明 |
|---|------|--------|------|
| `ApiEndpoint` | string | `http://localhost:1234/v1` | LLM API 端点（兼容 OpenAI 格式） |
| `ApiKey` | string | `""` | API 密钥 |
| `ModelName` | string | `llama-3.2-1b-instruct` | 默认模型名 |
| `MaxTokens` | int | `4096` | 最大生成 tokens |
| `Temperature` | double | `0.7` | 生成温度 |
| `TimeoutSeconds` | int | `300` | HTTP 请求超时（秒） |

> 注意：`AI:ModelName` 默认值 `qwythos-9b-v2` 加载失败，已改为 `llama-3.2-1b-instruct`。新建 provider 不填模型名时测试连接兜底 `gpt-3.5-turbo` 必失败（已知问题）。

### Update — 自动更新配置

| 键 | 类型 | 默认值 | 说明 |
|---|------|--------|------|
| `Provider` | string | `stardust` | 更新源类型：`stardust`（StarServer 版本接口，008 原有协议）/ `github`（GitHub Releases，spec 036）/ `local`（本地目录） |
| `ServerUrl` | string | `""` | StarServer 更新服务器地址；空字符串 = 不使用自动更新（仅手动检查） |
| `GitHubApiUrl` | string | `https://api.github.com` | GitHub API 基址（可指向 GHES） |
| `GitHubRepo` | string | `OpenForgeSelf/OpenForgeSelf` | GitHub 仓库标识 `owner/repo`，`Provider=github` 时必填；更新源为公开仓库，匿名访问即可 |
| `LocalDir` | string | `""` | 本地更新目录（`Provider=local` 时必填）：放置打包脚本 `release-local.ps1 -UpdateDir` 输出的 `OpenForgeSelf-<ver>-win-x64.zip`（连同 `SHA256SUMS.txt` / `RELEASE-NOTES-<ver>.md`）的目录 |
| `Channel` | string | `stable` | 更新通道：`stable`（稳定版）/ `beta`（测试版） |
| `CheckIntervalMinutes` | int | `60` | 自动检查间隔（分钟）；0 = 仅启动时检查一次 |
| `CheckTimeoutSeconds` | int | `5` | 启动时版本检查超时（秒），超时视为无更新、不阻塞启动 |
| `DownloadTimeoutSeconds` | int | `300` | 更新包下载超时（秒） |

> 更新源仓库 `OpenForgeSelf/OpenForgeSelf` 自 2026-09-26 起为**公开**仓库，匿名访问即可，不再提供令牌配置项（原 `GithubToken` / `FORGESELF_UPDATE_TOKEN` 已移除）。
> **运行时可变（2026-09-27 起）**：设置页「更新源配置」可修改并落盘到 `{数据根}/Config/update-settings.json`（`UpdateSettingsService`），重启时优先加载，优先级高于 appsettings.json；`Provider=local` 时填 `LocalDir` 本机目录即可走离线更新。

### Service — 服务配置

| 键 | 类型 | 默认值 | 说明 |
|---|------|--------|------|
| `Port` | int | `7102` | 后端 API 监听端口 |
| `PipeName` | string | `ForgeSelf` | 命名管道名称前缀（用于托盘进程通信，运行时拼接为 `ForgeSelf-Tray-{进程Id}`） |
| `AutoStartTray` | bool | `true` | 是否自动启动托盘图标 |
| `EnableWebPort` | bool | `true` | 是否启用前端 Web 端口 |
| `WebPort` | int | `7002` | 前端开发服务器端口 |
| `WebPortToken` | string | `""` | Web 端口访问令牌（可选） |

### Logging — 日志配置

| 键 | 默认值 | 说明 |
|---|--------|------|
| `LogLevel.Default` | `Information` | 默认日志级别 |
| `LogLevel.Microsoft.AspNetCore` | `Warning` | ASP.NET Core 框架日志级别 |

### Kestrel — Kestrel 服务器配置

| 键 | 默认值 | 说明 |
|---|--------|------|
| `Endpoints.Http.Url` | `http://*:7102` | Kestrel 监听地址和端口 |

## 环境覆盖

| 环境 | 覆盖文件 | 典型覆盖内容 |
|------|----------|-------------|
| Development | `appsettings.Development.json` | 调试日志、本地数据库路径 |
| Production | `appsettings.Production.json` | 生产数据库路径、日志级别 |

## 配置加载顺序

1. `appsettings.json`（基础配置）
2. `appsettings.{Environment}.json`（环境覆盖）
3. 环境变量（最高优先级，前缀 `ASPNETCORE_` 或自定义前缀）
4. 命令行参数

## 运行时配置

部分配置可在运行时通过 API 修改（存储在数据库中）：

| 配置项 | 存储位置 | 修改方式 |
|--------|----------|----------|
| AI 提供方配置 | `AIProvider` 表 | 设置页面 / `api/ai-providers` API |
| API 服务器密钥 | `ApiServerKey` 表 | 设置页面 / `api/api-server` API |
| 端口配置 | `PortConfiguration` 表 | 设置页面 / `api/port-configuration` API |
| 外观设置 | 前端 localStorage | 设置页面 |
| 更新源配置 | `{数据根}/Config/update-settings.json` | 设置页「更新源配置」/ `api/update/config` API（2026-09-27 起，重启后优先加载） |
