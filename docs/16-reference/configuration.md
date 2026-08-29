# 配置项参考

> 项目使用 `appsettings.json` + 环境覆盖（`appsettings.Development.json` / `appsettings.Production.json`）管理配置。
> 配置通过 `IConfigurationService` 注入到各服务。
> 最后更新：2026-08-20

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
    "CheckIntervalHours": 24,
    "DownloadPath": "updates",
    "GithubOwner": "openforgeself",
    "GithubRepo": "OpenForgeSelf",
    "GithubToken": ""
  },
  "Service": {
    "Port": 7102,
    "PipeName": "OpenForgeSelf",
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

> 数据库连接路径的**唯一真源是代码**（`OpenForgeSelf.Backend/Data/XCodeConfig.cs`），不是配置节。

| 键 | 默认值 | 说明 |
|---|--------|------|
| `ConnectionStrings:{连接名}` | 无（无需配置） | 可选覆盖。连接名取自 `XCodeConfig.DbFiles`：`OpenForgeSelf` + 各插件库名。**仅当 `Data Source=` 为绝对路径时生效**；相对路径会被忽略并输出告警（否则数据库会落到程序目录，发布目录下常因目录不存在报 SQLite Error 14） |

落盘规则（由 `IDataLocationService` 按运行形态决定数据根）：

| 运行形态 | 数据根 | 宿主库 | 插件库 |
|----------|--------|--------|--------|
| 开发（`ASPNETCORE_ENVIRONMENT=Development`） | `{程序目录}/Data` | `Data/OpenForgeSelf.db` | `Data/Plugins/{插件Id}/{库名}.db` |
| 发布 exe / Windows 服务 | `~/.forgeself` | `~/.forgeself/OpenForgeSelf.db` | `~/.forgeself/Plugins/{插件Id}/{库名}.db` |

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
| `CheckIntervalHours` | int | `24` | 更新检查间隔（小时） |
| `DownloadPath` | string | `updates` | 更新包下载目录 |
| `GithubOwner` | string | `openforgeself` | GitHub 仓库所有者 |
| `GithubRepo` | string | `OpenForgeSelf` | GitHub 仓库名 |
| `GithubToken` | string | `""` | GitHub API 令牌（可选，用于私有仓库） |

### Service — 服务配置

| 键 | 类型 | 默认值 | 说明 |
|---|------|--------|------|
| `Port` | int | `7102` | 后端 API 监听端口 |
| `PipeName` | string | `OpenForgeSelf` | 命名管道名称（用于托盘进程通信） |
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
