# 01 Intent — update-restart-socket-10013

- **task**: 2026-11-21-update-restart-socket-10013
- **date**: 2026-11-21

## 为什么做

用户实机故障：宿主「重启自动更新」后本地 LLM（gpustack:80）全部连接被拒
`SocketException (10013) WSAEACCES`。定位＝Windows 全机动态端口段（10000-64999）与
WinNAT/Hyper-V 保留段重叠，connect 阶段被本地拒绝；重启时刻是全机连接最密集窗口，
撞段概率最高（index.md :53 前两次实测已定性，均未根治）。

同一链路上 ps1 更新代理还有两个结构性问题：①版本保留步 `[version]` 对 10 位时间码溢出
（实机日志实锤，PC-041）；②PowerShell 脚本代理环境脆弱（编码/执行策略/PS 版本）。

## 做什么

1. 出站 10013 自愈：`OutboundSocketResilienceHandler` 有界重试接进模型出口 primary handler；
2. 原生更新代理：新工程 `ForgeSelf.UpdateAgent`（`update-agent.exe`，FDD 单文件）替代 ps1 主轨，宿主双轨选择（exe 优先/ps1 回退）；
3. PC-041 类级修复三处同源（`UpdateAgentCore.ToSortKey` / `update-agent.ps1` / `publish-plugin.ps1`）；
4. 打包链接线（publish-updateagent → package-release → release-local）与真源/台账回写。

## 到什么程度（验收）

- 10013 场景：AccessDenied 有界重试吸收、耗尽抛中文处置指引；非 AccessDenied 不重试；
- 代理全流程：等退出→落位→切 current→保留→清理→重启，进程内 E2E 全链绿；
- 双轨兼容：ps1 继续随包（老宿主只认 ps1，存量收编前禁删）；
- 后端全量测试对基线无新增红；仓库级守卫在位。
