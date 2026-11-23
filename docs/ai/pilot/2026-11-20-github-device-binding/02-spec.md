# 02 Spec — GitHub 账号绑定（PILOT-083）

## 1. 背景

插件市场 GitHub 源安装为匿名请求，私有仓库不可用。用户已有内置 OAuth App（Client 公开，无 Secret）。

## 2. 目标

绑定一次 GitHub 账号：宿主在插件 GitHub 拉取时自动附凭据；绑定过程按设备授权流完成，用户码自动复制、授权页自动打开。

## 3. 范围

- 后端：GitHubOAuthConfig / GitHubBindingStore / GitHubAccountService / GitHubAccountController；ForgeSetting 5 字段；PluginGitHubProvider 与 PluginHostToolCatalog 凭据接线。
- 前端：GithubBindPanel（四态）/ GithubBindDialog / GithubAccountPanel / SettingsView 分类 / PluginStore 前置检查。
- 不改：既有 AI Provider 加密链路、插件安装主流程（只加前置检查钩子）。

## 4. 技术方案要点

- 设备流：POST `login/device/code`（client_id+scope）→ 用户授权 → 轮询 POST `login/oauth/access_token`（`grant_type=urn:ietf:params:oauth:grant-type:device_code`）。
- 协议语义：`authorization_pending`/`slow_down` 为等待态（HTTP 400 也绝不抛错）；`expired_token`/`access_denied` 终态。
- 预填：GitHub 不返回 `verification_uri_complete`，本地拼 `verification_uri + ('?'|'&') + user_code=<urlencoded>`。
- 失败透传：`DescribeStartFailure` 合并 error/error_description（兜底 body/message），` · ` 连接，≤200 字符，永不 null/抛错。
- 存储：`ForgeSetting`（GitHubAccountLogin / GitHubTokenCipher / GitHubScopes / GitHubBoundAt / GitHubAutoAttachToken）；令牌经 `ISecretEncryptionService` 只落 `v2:` 密文。
- 出站凭据门控：`GetBearerToken()` = 已绑定 && 自动附凭据开；否则 null（一处门控，两处消费）。

## 5. Interaction Design（交互设计）

### 5.1 设置页·GitHub 账号面板

- **点什么出现什么**：
  - 进入「GitHub 账号」分类 → 未绑定时显示说明文案 + 「绑定 GitHub 账号」主按钮；
  - 点绑定 → 面板切准备态：旋转图标 + 「正在准备设备码，请稍候…」，入口按钮被整块替换（结构性防连点）；
  - 出码 → 成功横幅「设备码已复制到剪贴板」+ 大号等宽设备码 + 复制按钮 + 倒计时提示「N 秒后自动打开 GitHub 授权页…」+ 手动打开按钮；
  - 倒计时 3s 结束 → `window.open` 打开预填授权页（新标签，noopener）；
  - 轮询期间保持出码态（pending 静默继续；slow_down 自动放慢，用户无感）；
  - bound → 成功 toast「GitHub 账号已绑定：<login>」+ 面板切已绑定态（账号 login / 授权范围 / 解绑按钮 / 自动附凭据开关）；
  - expired/denied/failed/start 失败 → 错误横幅透传上游原因 + 「重试」按钮。
- **空态**：未绑定即空态（说明 + 入口）。
- **加载**：status 拉取时面板 v-loading；准备期整块 loading。
- **出错**：所有错误都有上游原因文案 + 可重试（不出现无出路的死页）。
- **边界**：解绑需 ElMessageBox 二次确认（不可逆动作）；自动附凭据开关失败回滚原值并报错；复制失败显示手动复制引导横幅。
- **验收标准**：见 §8 AC1–AC12。

### 5.2 插件市场·安装前置检查（二轮）

- 点「从 GitHub 安装」时先查绑定态：未绑定 → 不发安装请求，侧栏出现提示「安装 GitHub 源插件需要先绑定 GitHub 账号（私有仓库凭据）」+ 弹出绑定对话框，同时**武装**本次安装；
- 绑定成功 → 自动解除武装并续装（armed-resume）；用户关闭对话框 → 可点「已绑定 GitHub 账号，继续安装」手动续装；
- 已绑定 → 行为与从前一致（直接安装）。

### 5.3 无界面交互（工具/HTTP 面）

- `PUT /api/github-account/auto-attach { enabled }`：开关即存，返回最新绑定态投影；
- `DELETE /api/github-account/bind`：幂等，未绑定调用返回未绑定态；
- `GET /api/github-account/status`：任何已授权客户端可查，响应永不含令牌材料。

## 6. 数据与安全

- 令牌明文只在 `GitHubBindingStore.GetTokenAsync()` 内存中出现；落盘为 `v2:` 密文；不进日志、不进任何 HTTP 响应。
- 绑定态投影 DTO 字段：bound / accountLogin / scopes / boundAt / autoAttachToken（无 token 字段，序列化守卫测试锁定）。

## 7. 依赖与影响面

- 新增 DI：GitHubOAuthConfig（工厂）、GitHubBindingStore、GitHubAccountService（AppBuilder，先于插件注册无顺序要求）。
- `PluginGitHubProvider` 新增可选 `Func<string?>` 令牌源参数（既有调用点不受影响）；`PluginHostToolCatalog` 新增可选服务参数。

## 8. 验收标准（AC1–AC12）

- **AC1** 设备码申请成功解析 device_code/user_code/verification_uri/expires_in/interval；
- **AC2** 预填 URL 在干净 URI 上用 `?`、已有 query 用 `&`、user_code 经 URL 编码；
- **AC3** 未配置 ClientId 时申请失败给出明确原因（HTTP 400，文案含 ClientId）；
- **AC4** 上游错误体（error + error_description）被合并透传，≤200 字符，非 JSON 体兜底原样（trim）；
- **AC5** 轮询 `authorization_pending`/`slow_down` 不抛错（Pending/SlowDown 状态）；`expired_token`/`access_denied` 终态带原因；
- **AC6** 轮询成功绑定账号（/user 取 login）并落密文（密文 ≠ 明文，投影无令牌材料）；
- **AC7** 未绑定/空令牌出站不带 Authorization；已绑定且开关开带 `Bearer <token>`（Release 下载）；
- **AC8** git 克隆/拉取在已绑定且开关开时携带 `http.extraheader` 凭据参数；
- **AC9** 设置页四态 UX：准备 loading 防连点、出码自动复制 + 3s 倒计时自动开页、错误可重试、解绑二次确认；
- **AC10** 自动附凭据开关即改即存、失败回滚；关闭后出站不再附凭据；
- **AC11** 绑定域自定义面 token 组合（设备码块、绑定说明、次要文字）明暗两态对比度 ≥4.5:1（守卫测试）；EP 内建状态横幅暗色欠账另批修（TODO 留痕）；
- **AC12** 插件市场未绑定安装被前置检查拦截并武装，绑定成功自动续装；API 契约（status/unbind 幂等/e2e 未配置分支）绿。

## 9. 不做

见 `docs/07-decisions/not-taken-decisions.md` §068（无 Secret 下发 / 无独立实体表 / 无多账号 / 无全量 API 代理面）。

## 10. Unknown

- 无（设备流协议字段与端点均已在测试锚定；GitHub 侧行为以官方文档 RFC 8628 为准）。
