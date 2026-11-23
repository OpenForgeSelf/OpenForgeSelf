# 00 Repository Understanding — update-restart-socket-10013

- **task**: 2026-11-21-update-restart-socket-10013
- **date**: 2026-11-21

## 1. 技术栈与架构事实（读码实测）

- 宿主：ASP.NET Core（.NET 10，ForgeSelf.Api），QQNT 式版本化布局——安装根公共层（根启动器 + 运行时 + `update-agent.ps1`）+ 业务层 `versions/<ver>/` + `versions/current` 指针（真源 `docs/04-standards/packaging-upgrade-backup.md` §1.5-§1.8）。
- 更新链路：`StagedUpdateService`（spec 036 唯一链路）检查 → 下载校验 → 解压 `%LOCALAPPDATA%\ForgeSelf\Updates\<tag>\` → `ApplyStaged()` 拉起 `update-agent.ps1` → 宿主自停 → 代理换文件 → 重启根启动器。
- 模型请求出口：`AppBuilder.ConfigureModelProxyHttpClient` 给全部 IHttpClientFactory client 装 primary handler（`SocketsHttpHandler { Proxy = ModelProxy }`，2026-10-16-settings-network-proxy）。
- 版本号规则：`<major>.<minor>.<patch>.<yyMMddHHmm>`（10 位时间码第 4 段，packaging §4-R10）。
- 脚本执行纪律：一律 `pwsh`（禁 5.1）；`.ps1` 随包分发必须 UTF-8 BOM。

## 2. 与本任务相关的既有缺陷/风险

- 用户实例「重启自动更新后本地 LLM 连不上」：`HttpRequestException → SocketException (10013) WSAEACCES`（gpustack.luxvisions-inno.com:80，index.md :53 已有散例记载 2026-10-18/11-05 两次实测）。
- ps1 代理版本保留步 `[version]` 对 10 位时间码溢出（实机日志实锤，后归档 PC-041）。
- ps1 代理是 PowerShell 脚本进程：依赖 PS5.1 兼容、编码、执行策略多重环境脆弱性（历史多次事故，见 index/PC 台账）。

## 3. 仓库级守卫基建

- `RepositoryScriptTests`（仓库级静态守卫，全量测试才扫）；
- pre-commit 门禁链：design:check → check-problem-classes → commit-hygiene → verify-pilot-artifacts → verify-pilot-feature-writeback → commit-msg（Pilot trailer）。
