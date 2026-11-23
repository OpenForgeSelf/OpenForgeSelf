# 101 - GitHub 账号绑定

> 功能真源：本文件。实现 = PILOT-083（RFC 8628 设备授权流）+ O2 内置 OAuth App + 绑定 UX 两轮（2026-11-20/22）。

## 功能说明

用户可将个人 GitHub 账号绑定到宿主，绑定后：

1. **插件市场 GitHub 源安装**自动携带凭据（Release 下载 Bearer + git 克隆 extraheader），支持私有仓库；
2. 授权范围固定 `repo workflow`（插件源安装与发布所需的最小集）。

## 入口

- 设置页 → 左侧分类「GitHub 账号」（`/settings?cat=github-account`）
- 插件市场侧栏「安装」→ 从 GitHub 安装（未绑定时自动前置检查并引导绑定，绑定成功自动续装）

## 绑定流程（RFC 8628 Device Authorization Grant）

1. 点击「绑定 GitHub 账号」→ 后端向 `https://github.com/login/device/code` 申请设备码（等待期按钮整块 loading「正在准备设备码，请稍候…」）；
2. 出码后自动复制用户码（横幅「设备码已复制到剪贴板」），3 秒倒计时自动打开预填授权页（`verification_uri?user_code=...`）；
3. 后端按 GitHub 返回的 interval 轮询令牌端点：`authorization_pending` 继续等、`slow_down` 自退避 +5s；
4. 终态：`bound`（展示账号）/ `expired` / `denied` / 其他失败——一律透传上游原因并给出重试。

## 关键事实（精度口径）

| 事实 | 值 | 出处 |
|------|-----|------|
| 设备码端点 | `https://github.com/login/device/code` | `ForgeSelf.Api/Services/GitHubOAuthConfig.cs` |
| 令牌端点 | `https://github.com/login/oauth/access_token` | 同上 |
| 内置 OAuth App ClientId | `Ov23likdtDN2AlQvAO33`（appsettings `GitHubOAuth:ClientId`，可被用户配置覆盖） | `ForgeSelf.Api/appsettings.json`；守卫 `GitHubOAuthDefaultsGuardTests` |
| 令牌落盘 | 仅密文（`ForgeSetting.GitHubTokenCipher`，经 `ISecretEncryptionService` AES 加密）；明文永不出后端、不进日志/响应 | `ForgeSelf.Api/Services/GitHubBindingStore.cs` |
| 绑定态存储 | `ForgeSetting`（ForgeSetting.config），非独立实体表 | 同上 |
| 自动附凭据开关 | `ForgeSetting.GitHubAutoAttachToken`（默认开）；关闭后插件 GitHub 拉取不带凭据 | `GitHubAccountService.GetBearerToken()` 门控 |
| 私有仓库克隆 | `git -c http.extraheader="Authorization: Bearer <token>" clone/pull` | `ForgeSelf.Api/Plugins/Services/PluginHostTools.cs` |
| Release 下载 | `PluginGitHubProvider` 请求前附加 `Authorization: Bearer`（仅已绑定且开关开） | `PluginPackageProviders.cs` |

## HTTP 端点（控制器 `ForgeSelf.Api/Controllers/GitHubAccountController.cs`，均 `[Authorize("ApiKeyPolicy")]`）

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/github-account/status` | 绑定状态（`{ success, data: { bound, accountLogin, scopes, boundAt, autoAttachToken } }`） |
| POST | `/api/github-account/bind/start` | 申请设备码；未配置 ClientId → HTTP 400 + 明确错误（e2e 即测该分支） |
| POST | `/api/github-account/bind/poll` | 轮询一次（body `{ deviceCode }`）；返回 `pending/slow_down/bound/expired/denied/failed` |
| DELETE | `/api/github-account/bind` | 解绑（幂等） |
| PUT | `/api/github-account/auto-attach` | 自动附凭据开关（body `{ enabled }`） |

## 失败语义（错误体透明化）

- `bind/start` 的上游错误（如 `incorrect_client_credentials` + `error_description`）经 `GitHubAccountService.DescribeStartFailure` 合并为「描述 · 错误码」（≤200 字符），HTTP 400 透传给前端，不吞成通用「网络异常」。
- 轮询的 `authorization_pending`/`slow_down` 是**协议等待态**，绝不因状态码 400 抛错。

## 验收标准

见 `docs/ai/pilot/2026-11-20-github-device-binding/02-spec.md`（AC1–AC12）与两轮 UX 批次 02/05。
