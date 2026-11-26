# 00 Repository Understanding

## 相关模块
- `ForgeSelf.UpdateAgent/UpdateAgentRunner.cs`：exe 轨更新应用主流程（步骤 1-9），本次修复点。
- `ForgeSelf.Api/Services/StagedUpdateService.cs`：宿主侧暂存更新服务，拉起 agent 时设 `DOTNET_ROOT = staged 解压根`。
- `scripts/release/*`：发布链（前端构建 / 宿主 publish / agent publish / 打包）。
- `docs/04-standards/packaging-upgrade-backup.md`：QQNT 式分层设计真源（公共层内置运行时 + 版本层快照 + agent FDD 零 NuGet）。

## 设计意图（先理解再决策）
- 公共层（安装根一份）放启动器 + 完整运行时 + agent；版本层 `versions/<ver>/` 每版 FDD 单文件不自带运行时，靠 `DOTNET_ROOT` 共用公共层。
- agent 要 robocopy 覆盖安装根，为防覆盖自身运行时导致文件锁，启动时 `DOTNET_ROOT` 指向 staged 解压根的运行时副本；重启宿主那一刻才改指回安装根。
- staged tag 目录同时是「待装包解压根」与「agent 运行时根」——这是理解本缺陷的关键双重身份。

## 现状事实（第一性原理核查）
- 11-19~22 五次真实更新全走 ps1 轨（日志 `Exe=`），运行时来自系统，删 staged 不伤己，全成功。
- 11-23 exe 轨只在单测（桩/系统运行时）验证过。
- 11-24 15:09 exe 轨首次真机（`ExeName=`），`DOTNET_ROOT` 真指 staged，步骤 8 删 staged 后步骤 9 重启崩。
- SDK 10/11 打 agent 均 195193B；失败包内 203264B = 195193 + 8071B 签名，签名不影响启动。均排除。
