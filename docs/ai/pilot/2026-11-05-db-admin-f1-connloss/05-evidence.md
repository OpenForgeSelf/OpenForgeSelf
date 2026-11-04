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