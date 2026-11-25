---
id: 2026-11-24-update-agent-exe-cleanup-before-restart
title: exe 轨首次真机更新宿主未拉起——先删 staged 再重启自删运行时
pc: PC-050
severity: P1
evidence: Verified
---
# 2026-11-24 exe 轨首次真机更新宿主未拉起

## 1 现象

页面点击更新，宿主（15:09）退出后没有再起来。`~/.forgeself/log/`（托盘日志）今日无更新相关信息——因为更新链路根本不写这里。

## 2 现场证据

| 来源 | 关键内容 |
|------|----------|
| 用户目录 `Logs/update-agent-20261124150932.log` | 末尾 `致命异常 System.IO.FileNotFoundException: System.Collections.Specialized, Version=10.0.0.0 at UpdateAgentRunner.RestartLauncher`；前面 `警告: 删除失败（跳过）: ...hostfxr.dll — Access denied` |
| 成功对照 `.../Updates/agent-20261122144443.log`（11-22 14:44） | 有「宿主已重启 ✔ / 更新完成 ✔」；首行字段 `Exe=`（ps1 轨） |
| 本次日志首行 | 字段 `ExeName=`（exe 轨） |
| staged 现状 | `Updates/v2.4.22.2611241505/extracted/shared/Microsoft.NETCore.App/10.0.13/` 只剩 17 DLL；安装根完整 185 DLL |

## 3 第一性原理定位（推翻的假设保留，供复盘）

1. **异常倒推**：`RestartLauncher` 里 `psi.EnvironmentVariables["DOTNET_ROOT"] = o.InstallDir;`（`UpdateAgentRunner.cs:276`）首次访问 `EnvironmentVariables` → 需加载 `StringDictionary`（在 `System.Collections.Specialized.dll`）。
2. **谁提供这个 DLL**：宿主拉起 agent 时设 `DOTNET_ROOT = staged 解压根`（防覆盖自锁）→ agent 运行时踩在 staged 上。
3. **DLL 还在吗**：步骤 8 `CleanupStagedAndBackups` 删掉 staged 父目录（连带 shared）——未加载的 DLL（含 Specialized）被删；已加载被占用的（`hostfxr.dll`）删不掉，降级告警。步骤 9 重启才首次加载 Specialized → 文件已不存在 → 崩。
4. **排除项（实测）**：
   - SDK 10 和 SDK 11 打 agent 均 = 195193 B → 非 SDK 版本导致（用户"装了 net11 改变打包"假设不成立）。
   - 失败包内 agent = 203264 B = 195193 + 8071 B Authenticode 签名（尾部 DER PKCS#7）；实测签名/未签名两 exe 都能启动到参数解析 → 签名非根因。
   - 11-22 成功那次 staged 也只剩 20 DLL（残缺）→ 不是残缺本身，而是"agent 踩 staged 时 staged 被删"的时序。
   - 我的 csproj 改动（PC-049 SelfContained=false）只影响编译期声明，与运行时 BCL 加载无因果——但它修通 NETSDK1151 才让发布链第一次走到重启步骤，使缺陷暴露。

## 4 为什么"之前能用、现在不行"

| 轨 | 运行时来源 | 删 staged 影响它吗 | 历史 |
|----|-----------|-------------------|------|
| ps1（`Exe=`） | 系统 PowerShell | 否 | 11-19~22 五次真实更新全成功 |
| exe 单测（`ExeName=`） | 系统运行时/桩目录 | 否 | 11-23 单测通过 |
| **exe 真机**（`ExeName=`） | **staged（DOTNET_ROOT 指它）** | **是** | **11-24 15:09 首次真机，崩** |

## 5 修复

交换 `UpdateAgentRunner.RunAsync` 步骤 8/9：先 `RestartLauncher(o)` 后 `CleanupStagedAndBackups(...)`。改动 2 行 + 注释写明不可交换；新增 G2 守卫 `Pc050_UpdateAgentRunner_MustRestartBeforeCleanupStaged` 断言顺序。

## 6 验证

- G2 守卫测试 Passed（1/1）；修复后 agent 单文件 publish 成功（195193 B，体积不变符合预期）。
- 真机完整更新闭环尚未实测（发布链在本沙箱卡 MSB4166 并行节点崩溃，属环境约束，见 TODO）。需在正常终端重跑一次更新验证宿主被拉起。

## 7 遗留

- 回滚/启动兜底仍缺（current 指针无回滚、无启动健康检查）；用户已提出诉求，另批（PC-050 之外）处理。
- 发布链 MSB4166（本沙箱并行节点崩溃）用 `-m:1` 单节点或正常终端规避；本轮重打包未能在沙箱完成。
