# 02 Spec — update-restart-socket-10013

- **task**: 2026-11-21-update-restart-socket-10013
- **date**: 2026-11-21

## 1. 背景与问题

见 01-intent。三件事：10013 出站自愈、原生更新代理双轨、PC-041 溢出类修复。

## 2. 目标与非目标

- 目标：出站 WSAEACCES 有界重试；exe 代理全流程与 ps1 同构；打包链自动携带双代理；版本比较补零定宽。
- 非目标：不实现 WinNAT 保留段探测/自动改端口（用户机操作，运维指引写入诊断信息）；不改 008 冻结链路；不动用户运行实例。

## 3. 方案

### 3.1 出站自愈层

`ForgeSelf.Api/Services/Net/OutboundSocketResilienceHandler`（DelegatingHandler）：

- 判定：异常链上有 `SocketException.SocketErrorCode == AccessDenied`（WSAEACCES=10013）；
- 重试：MaxAttempts=6，退避 150ms×attempt（0.15/0.3/0.45/0.6/0.75s，有界不指数爆炸）；
- 耗尽：抛 `InvalidOperationException`，消息含 `net stop winnat`（临时）与 `netsh int ipv4 set dynamicport tcp start=40000 num=9900`（永久）处置指引；
- 接线：`AppBuilder.ConfigureModelProxyHttpClient` primary handler = 自愈层，`InnerHandler = SocketsHttpHandler { Proxy = ModelProxy }`（代理与回环语义不变）。

### 3.2 原生更新代理（ForgeSelf.UpdateAgent）

- 参数契约：`--host-pid --install-dir --staged-dir --exe-name --arg-list`；
- 步骤 0-9 与 ps1 同构：等宿主退出（上限 5 分钟，PID 不存在视为已退出）→ staged 布局校验（拒绝安装根在 versions 内）→ 业务层 robocopy 落 `versions/<newVer>/` → 公共层合并 `/XD versions` → current 原子切换（tmp+move）→ 扁平残留清理（保留名单制，仅删被版本层接管者）→ 目录名规范化 → 旧版本保留（current+补零序上一版）→ staged tag+Backups 清理 → 重启根启动器（DOTNET_ROOT=安装根，WindowStyle=Hidden）；
- 健壮性：robocopy 退出码 0-7 成功；清理类失败降级日志告警不中断；AgentLog 三重降级（Logs 专目录→Updates 根→纯控制台）；
- 版本比较：`UpdateAgentCore.ToSortKey` 补零定宽 + ordinal（PC-041）；
- 拉起方式（宿主侧 `TryStartExeAgent`）：先拷 `%LOCALAPPDATA%\ForgeSelf\Updates\agent-run\update-agent.exe` 再运行（防运行中自文件锁）+ `DOTNET_ROOT`=staged 解压根（防运行时自锁）；拉起失败回退 ps1，双缺 failed；
- 选择链：staged exe → 安装根 exe → staged ps1 → 安装根 ps1。

### 3.3 打包链

- `publish-updateagent.ps1`：dotnet publish FDD 单文件（win-x64），体积断言 >8MB 拒交付（防误打自包含）；
- `package-release.ps1 -AgentDir`：update-agent.exe + ps1 入公共层，版本目录内防重复；
- `release-local.ps1`：自动接线 publish-updateagent。

### 3.4 Interaction Design（无界面交互）

本批无 UI 变更。相关「无界面」动线：宿主更新仍由设置页/托盘「重启并更新」触发
`POST /api/update/apply` → 后端选择链拉起代理（用户可见反馈沿用既有 `applying` 状态文案
「正在重启并更新，请稍候...」，无新增交互面）；ps1 代理参数契约不变，可继续手动运行。

## 4. 验收标准（AC）

- AC-1：AccessDenied 前 N 次失败，重试后成功（测试断言 Attempts=3）；
- AC-2：AccessDenied 耗尽 6 次抛含 `net stop winnat` 的中文诊断；非 AccessDenied 立即透传（Attempts=1）；异常链深层命中同样识别；
- AC-3：wiring——factory client 管线首层为自愈层、终级 handler 仍挂 ModelProxy；
- AC-4：进程内 E2E 全链：落位/公共层合并覆盖（/XD versions 不动旧版本目录）/current 切换/版本保留/扁平清理/staged+zip 清理/重启全部断言；
- AC-5：同版本重装走 robocopy 覆盖不炸；安装根在 versions 内当场拒绝；staged 缺 versions/ 报错不落位；
- AC-6：PC-041 三处同源守卫（RepositoryScriptTests）+ ToSortKey/CompareVersionNames/SortNewestFirst 排序语义；
- AC-7：ps1 继续随包断言（守卫）+ 文档同步（packaging §1.2/§1.8.2/变更记录、scripts/README、index/PC-041/case）。

## 5. 风险与回滚

- 风险：代理换文件链错误可能弄瘫用户实例 → 进程内 E2E 全链 + 模拟布局先行；exe 拉起失败自动回退 ps1（双保险）；
- 回滚：`git revert` 单提交；exe 轨缺位时宿主自动走 ps1（行为回退不回滚代码）。

## 6. 不确定点

- 无（真机 exe 全流程更新验证待用户下版动线，属发布后走查，不在本批范围）。
