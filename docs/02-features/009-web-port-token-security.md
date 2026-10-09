---
feature_key: F009
feature_no: 009
status: implemented
last_updated: 2026-10-06
aliases: ["009-web-port-token-security"]
---

# 009 Web 端口与令牌安全 — 功能需求与设计

> 功能编号：009
> 状态：已实现
> 关联：003 API 服务器；008 托盘；08-security（历史 specs/009 已弃用）
> 最后更新：2026-08-12

## 1. 功能需求

### 1.1 背景
后端监听端口需可动态配置（避免冲突），且管理类接口需令牌鉴权（`ApiKeyPolicy`），防止未授权修改 AI Provider、重启服务等。

### 1.2 目标
1. 端口动态配置落库（`ForgeSetting.PortNumber`），重启生效；
2. 管理接口统一 `ApiKeyPolicy` 鉴权；
3. 端口校验（1024–65535）。

## 2. 设计

### 2.1 实现位置
| 文件 | 职责 |
|------|------|
| `Models/ForgeSetting.cs` | `PortNumber = 7102`（默认值） |
| `Controllers/PortConfigurationController.cs` | `api/portconfiguration`（`[Route("api/[controller]")]` → 小写 controller 名）：读取/设置端口（校验范围） |
| `AppBuilder.cs` | `ApiKeyPolicy` 注册（238 行附近）+ 端口绑定（226/401 行） |
| `Controllers/ApiServerController.cs` | 令牌 init-token/regenerate/restart |

### 2.2 鉴权策略
- 策略名 `ApiKeyPolicy`，凭据即 `ApiServerKey` 明文（解密后比对）；
- `AIProviderController`、`ApiServerController`（status/regenerate/restart 三处）、`PortConfigurationController` 均已加 `[Authorize("ApiKeyPolicy")]`；仅 `init-token` 端点因首次初始化需要故意免鉴权。

## 3. 使用指南
设置 → 端口配置（重启生效）；首次启动 `GET api/api-server/init-token` 取令牌，客户端请求管理接口带 `Authorization: Bearer <token>` 或 `X-Api-Key`。

## 4. 注意事项
- 端口改后需**重启服务**（`ApiServerController.restart` 或托盘）才监听新端口；
- 运行实例无权限停止时，改配置需手动重启 exe（TODO.md 已知：PID 锁导致 `dotnet build` 复制阶段 CS2012，需构建到独立输出目录或停止进程）。

## 5. 测试覆盖
| 测试 | 覆盖点 |
|------|--------|
| `PortConfigurationController` 单测/集成 | 端口范围校验、读取/设置 |
| `ApiServerController` 集成 | 令牌发放/轮换 |
