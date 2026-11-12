## 根因终版（三段合并 · 2026-11-05 04:3x 定稿，取代先前分段叙述）

> 诊断三段演进：**WAL 假设（qa，已撤回：磁盘取证漏扫实例目录）→ 宿主产物陈旧（0417 干净重建证伪）→ 双 XCode 双 DAL（终版，物证贯通）**。

**终版机制（全实锤）**：
1. `Plugins/DbAdmin/DbAdmin.csproj` 曾带 `CopyLocalLockFileAssemblies=true`（为外置驱动预留）→ NewLife.XCode 产物 **XCode.dll 被拷进插件 bin**；
2. 宿主 `ForgeSelf.Api.csproj` StageAllPlugins 的 Exclude 通配仅 `ForgeSelf.*;NewLife.*`——**XCode.* 两不匹配** → 私带 DLL 漏进发布载荷（旁路 package-plugin.ps1/publish-plugin.ps1 早补了 XCode.dll 排除，主路漏改——教科书同类缺口）；
3. 部署面仅 DbAdmin 一家私带（22 插件扫查）；宿主隔离 ALC 加载插件 → **插件侧 EntityFactory/DAL 为独立静态态** → 宿主 `DAL.AddConnStr("DbAdmin")` 登记对插件实体不可见 → ExpireStale（Apply 阶段实体访问）触发插件侧 XCode 自动兜底 `Data Source=Data\DbAdmin.db`（相对 CWD=版本目录，Migration=On 特征串即插件侧默认格式）→ **双 XCode 双 DAL 双库**。
4. 贯通解释：双库（L104 版本内 vs L445-446 宿主位）、跨版本丢失（升级换版本目录）、时序矛盾（干净重建后登记先于插件首用兜底照样触发——因为登记写的是宿主 DAL，兜底读的是插件 DAL，两个静态态）。

**0.1.2 修复（插件侧 + 宿主 Exclude 类级封口，lead APPROVED）**：
- `DbAdmin.csproj` 删 `CopyLocalLockFileAssemblies`（真凶注记：铁律12c 仅指宿主没有的库）
- 宿主 StageAllPlugins Exclude 追加 `XCode.*;MX.*`（类级封口，主路对齐旁路）
- `DbAdminPlugin.Apply` 删 ExpireStale 预热（Apply 零实体访问，对齐 AgentHub 先例；惰性语义由 DbQueryService.ResolveCore/ListApprovals 首行保证）
- 版本 0.1.2

# Evidence — F1（P1）用户连接跨宿主重启丢失：修复批

> 任务：`2026-11-05-db-admin-f1-connloss`（mini-task 裁剪形态）｜状态：**终稿**。

## 0 证据表（全部 Verified，日志在 wt-db/.temp/）

| # | 项 | 证据 | 结果 |
|---|----|------|------|
| E1 | 根因定位 | 宿主日志 03:30:32.895 `自动为[DbAdmin]设置SQLite连接字符串：Data Source=Data\DbAdmin.db`（版本层 Log\2026_11_05.log）+ `DataSource=D:\...\versions\2.4.22.2611050327\Data\DbAdmin.db` | **Verified**：XCode 未登记兜底 → 相对 CWD=版本目录 |
| E2 | 三库验尸（只读拷贝，System.Data.SQLite Read Only） | v0300 库 `DbConnection` 1 行=walkthrough-fixture（qa 03:17:45 保存，mtime 03:17:44 秒级吻合）；v0327 建表 0 行；`~/.forgeselves` 侧 4096B 零表空壳；三库 -wal 全 0B | **Verified**：数据困在旧版本目录，可抢救 |
| E3 | 同型对照 | 22 插件库 21 个在 `~/.forgeselves/plugins/<id>/`，仅 DbAdmin 在版本层；AgentHub 插件侧**零** EnsureCreated 调用（AgentHubTables.cs:14 注释阐明时序） | **Verified**：DbAdmin Apply 抢跑 = 唯一反模式 |
| E4 | 修复 Red→Green | 删 `DbAdminPlugin.cs` Apply 内 EnsureCreated 调用（保留方法本体供测试夹具）+ 根因注记；版本 0.1.1 | **Verified**：build 0 error |
| E5 | 后端回归 | dotnet build 0 error + DbAdmin* 过滤集 **166/166**（log-f1-tests.txt）+ 新增守卫 **DbAdminFallbackGuardTests 2/2**（guard8：①未登记→兜底相对路径字面量断言 ②已登记→绝对路径登记串生效） | **Verified** |
| E6 | 插件 web | build 68.60kB + vitest **61/61**（log-f1-webbuild/webtest.txt） | **Verified** |
| E7 | **重启链实测**（lead 指定验收链） | dev-stack 起→POST 保存 f1-restart-check（id=2）→ -Stop →再起（**新进程新 token** sk-c328e8…）→ GET 列表含 f1-restart-check（**restore PASS**）+ **宿主日志无「自动为[DbAdmin]设置」**（lead 验收断言 b ✓）+ CWD 侧无 DbAdmin.db 文件 | **Verified**（log-devstack-f1b/c/d + dev-stack-backend.log） |
| E8 | e2e 冒烟复跑 | db-admin 冒烟主链路 **1 passed (59.9s)**（log-e2e-f1.txt）——删 Apply 调用未破 S-R 链 | **Verified** |

## 1 证据边界（如实交代）

- **e2e 内无法直接模拟「宿主升级换版本目录」**（publish 宿主由 globalSetup 单生命周期管理，spec 无宿主控制权）——「保存→模拟升级重启→恢复」链以 **E7 dev-stack 双起实测**覆盖同语义（跨进程重启 + 数据根持久化 + 日志断言），版本目录漂移本身由守卫测试①（兜底=相对路径实锤）+「Apply 不抢跑」封死。
- qa 第二次保存（03:33:28）的行在 v0327 主库 0 行 + wal 0B——**去向未定**（POST 响应待 qa 佐证）；不阻塞主因与修复。
- 基底记录按 lead 补强 a：qa 包记录（0300=dd00969 / 0327=966b943）与我的字符串探测不兼容（单文件发布压缩，字面量探测不可靠）；**以复现实验为锚**：dd00969 内容 dev 宿主正常（库落数据根、无兜底日志、CWD 无文件），51888 异常实例宿主必不含登记——晨报按此口径。

## 2 数据抢救（挂第四轮）

qa 的 walkthrough-fixture 行仍在 v0300 库（E2）。抢救方式 = **第四轮宿主（0.1.1）上线后经 API 重建连接**（走修好的写路径落数据根；51888 运行实例的库文件不可直接改——铁律）。重建动作留证据后报 qa/lead。