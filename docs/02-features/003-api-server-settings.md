# 003 API 服务器设置（初始化令牌）— 功能需求与设计

> 功能编号：003
> 状态：已实现
> 关联：specs/003-api-server-settings/；009 Web 端口与令牌安全；100 密钥加密
> 最后更新：2026-08-12

## 1. 功能需求

### 1.1 背景
后端首次启动需要初始化访问令牌（`ApiServerKey`），并在运行期支持查看、重新生成、重启服务。令牌用于 `ApiKeyPolicy` 鉴权（见 009）。

### 1.2 目标
1. 首次启动生成初始化令牌（`ApiServerKey.KeyCipher` 加密存储）；
2. 支持查看当前令牌（`init-token`）、重新生成（`regenerate`）、重启服务（`restart`）；
3. 令牌即 `ApiKeyPolicy` 的合法凭据。

## 2. 设计

### 2.1 实体（`Entities/ApiServerKey.cs`）
| 字段 | 含义 |
|------|------|
| `KeyCipher` | API 密钥密文（AES-256-CBC，见 100） |
| `IsActive` | 是否当前生效密钥 |
| `CreateTime` / `UpdateTime` | 时间戳 |

### 2.2 实现位置
| 文件 | 职责 |
|------|------|
| `Entities/ApiServerKey*.cs` | 实体 |
| `Controllers/ApiServerController.cs` | `api/api-server`：`status` / `init-token` / `regenerate` / `restart` |
| `AppBuilder.cs` | `ApiKeyPolicy` 注册（238 行附近）+ 端口绑定 |

## 3. 使用指南
`GET api/api-server/init-token` 取当前令牌（前端设置页展示）；`POST regenerate` 轮换并失效旧令牌；`POST restart` 5 秒后按 `ForgeSetting.PortNumber` 重启。

## 4. 注意事项
- 重新生成令牌后，旧的 LLM 客户端配置需同步更新，否则鉴权失败。

## 5. 测试覆盖
| 测试 | 覆盖点 |
|------|--------|
| `ApiServerController` 集成测试 | init-token / regenerate / restart 行为 |
