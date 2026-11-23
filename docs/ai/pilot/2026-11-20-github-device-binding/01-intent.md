# 01 Intent — GitHub 账号绑定（PILOT-083）

## 为什么做

- 插件市场「从 GitHub 安装」当前是匿名请求：私有仓库 Release 下载 404、私有仓库 git 克隆被拒——宿主无法消费用户自己的私有插件源。
- 用户已有 GitHub OAuth App（内置 ClientId `Ov23likdtDN2AlQvAO33`，公开应用无需 ClientSecret），期望「绑定一次，安装自动带凭据」。

## 做什么

- 后端：RFC 8628 设备授权流（device code → 用户授权 → 轮询换 token），绑定态落 `ForgeSetting`（令牌仅密文），5 个 HTTP 端点（status / start / poll / unbind / auto-attach）。
- 插件消费面：`PluginGitHubProvider` 下载与 `PluginHostToolCatalog` git 克隆/拉取在「已绑定 + 自动附凭据开」时自动带 Bearer。
- 前端：设置页「GitHub 账号」面板（绑定/解绑/开关）+ 四态绑定 UX + 插件市场安装前置检查与绑定后自动续装。

## 到什么程度（完成定义）

- AC1–AC12 全绿（见 02-spec §验收标准）；令牌明文不出后端进程；e2e 零外联（未配置 ClientId 分支驱动）。
- 不做：多账号/组织、GitHub API 全量代理、绑定态独立实体表（见 not-taken-decisions 068）。
