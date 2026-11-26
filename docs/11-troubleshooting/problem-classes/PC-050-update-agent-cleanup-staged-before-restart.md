---
id: PC-050
title: 更新代理 exe 轨先删 staged 再重启宿主，自删脚下运行时致 BCL 加载失败、宿主未被拉起
tags: [自更新, 更新代理, staged, DOTNET_ROOT, exe轨, BCL加载]
invariant: exe 轨更新代理的重启宿主步骤必须先于清理 staged 步骤——agent 运行时 DOTNET_ROOT 指向 staged 解压根（防覆盖安装根自锁），若先删 staged，重启时首次访问 psi.EnvironmentVariables（StringDictionary 位于 System.Collections.Specialized.dll）会因该 DLL 已删而抛 FileNotFoundException，宿主未被拉起
status: guarded
first_seen: 2026-11-24
recurrences: []
cases: [2026-11-24-update-agent-exe-cleanup-before-restart]
signature:
  - {id: S1, kind: grep, glob: "ForgeSelf.UpdateAgent/UpdateAgentRunner.cs", regex: "(?s)CleanupStagedAndBackups.{0,200}RestartLauncher", expect: no-match}
  - {id: S2, kind: test, path: "ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs"}
allowlist: []
guards:
  - {level: G2, path: "ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs", status: active}
lens:
  - 更新代理的 staged 目录既是「待装包解压根」又是「agent 自身运行时根」（DOTNET_ROOT 指向它）——清理它等于拆自己脚下的地板；凡 agent 重启后仍需执行的动作，必须在清理前完成。
  - 延迟加载（JIT）会让这类缺陷极难复现：System.Collections.Specialized 直到首次访问 ProcessStartInfo.EnvironmentVariables 才加载，删除 staged 时它还没进内存，到重启那刻才炸；单测若用系统运行时或桩目录则永远碰不到。
  - 排查"宿主更新后没起来"必须看更新代理日志（%LOCALAPPDATA%\ForgeSelf\Logs\update-agent-*.log），而不是托盘日志（~/.forgeself/log/）——后者只有 Tray 自己的启停，更新链路根本不写进去。
  - ps1 轨与 exe 轨的日志字段不同（Exe= vs ExeName=）——判断"以前能不能用"必须先看轨型；不同轨运行时来源不同，同一缺陷只在踩 staged 运行时的 exe 轨暴露。
---
# PC-050 先删 staged 再重启 → 自删运行时 → 宿主未拉起

> 本文件是「这一类问题」的唯一真源：规则、扫描方法、修法。事件经过在 case，不在这里。
> 状态机与守卫等级语义见技能 `.agents/skills/problem-class-closure/references/07-recording-spec.md`。

## 1 不变量与诱因

**不变量**：exe 轨更新代理 `UpdateAgentRunner.RunAsync` 的 `RestartLauncher(o)` 必须排在 `CleanupStagedAndBackups(o.InstallDir, o.StagedDir)` **之前**。原因：宿主 `StagedUpdateService` 拉起 agent 时设 `DOTNET_ROOT = staged 解压根`（设计上防 robocopy 覆盖安装根运行时时自锁），所以 agent 进程整个生命周期都踩在 staged 运行时上。`CleanupStagedAndBackups` 删的正是 staged 的父目录（连带 `shared/` 运行时）：未加载的 DLL（含 `System.Collections.Specialized.dll`）被删掉，已加载被占用的删不掉（日志 `hostfxr.dll ... Access denied` 即此，降级告警"不影响"）。随后 `RestartLauncher` 首次访问 `psi.EnvironmentVariables["DOTNET_ROOT"]`——`EnvironmentVariables` 返回 `StringDictionary`（在 `System.Collections.Specialized.dll`），触发该 DLL 首次加载 → 文件已不存在 → `FileNotFoundException` → agent 进程终止 → 宿主未被拉起。

**为什么会犯**：
- 两条各自合理的设计规则在同一进程内冲突：「agent 运行时指向 staged（防自锁）」与「应用后清理 staged（R3）」，步骤顺序没协调。
- JIT 延迟加载放大盲区：`HostPid/InstallDir/StagedDir` 等属性解析都不碰 `EnvironmentVariables`，直到重启那行才首次加载该 BCL——删除时"看起来没事"。
- ps1 轨（11-19~22 真实更新）用系统 PowerShell 运行时，删 staged 不伤己，一直成功；exe 轨单测用系统运行时或桩目录，也通过；只有 exe 轨真机（2026-11-24 15:09）DOTNET_ROOT 真指 staged，首次暴露。
- 被前端构建 / NETSDK1151（PC-047/049）挡在前面，发布链此前根本走不到重启步骤。

## 2 特征签名与扫描方法

- **S1**：grep `ForgeSelf.UpdateAgent/UpdateAgentRunner.cs`，若 `CleanupStagedAndBackups(o.InstallDir, o.StagedDir);` 之后 200 字符内还出现 `RestartLauncher(o);` 即红（顺序颠倒）。
- **S2**：G2 测试 `ProblemClassEnumerationTests.Pc050_UpdateAgentRunner_MustRestartBeforeCleanupStaged`，用 `IndexOf` 断言 restart 位置早于 cleanup，带阳性对照（存在 `psi.EnvironmentVariables["DOTNET_ROOT"]` 那行）。
- **强特征诊断**：更新代理日志末尾 `FileNotFoundException: System.Collections.Specialized` at `UpdateAgentRunner.RestartLauncher` + 同日志前面有 `删除失败 ... hostfxr.dll ... Access denied`（删 staged 时占用文件删不掉）→ 本类命中。
- **盲区**：S1/S2 只扫源码顺序；若改为"运行时独立到 agent-run/runtime"（另一条修法）则顺序约束可放宽，届时需同步调整守卫（lens 已提示 staged 双重身份）。

## 3 Canonical fix

交换步骤 8/9：

```csharp
// 改前（先删 staged = agent 自己的运行时，再重启）
CleanupStagedAndBackups(o.InstallDir, o.StagedDir);
RestartLauncher(o);

// 改后（重启时 staged 运行时还在，BCL 可加载；宿主起来后再清理）
RestartLauncher(o);
CleanupStagedAndBackups(o.InstallDir, o.StagedDir);
```

并在 `RunAsync` 顶部步骤注释与调用点写明「不可交换」的理由，防后人"优化"回原顺序。

**被否方案**：
- 提前预热 `EnvironmentVariables`（启动时先访问一次把 DLL 载进内存）→ 贴片，依赖 JIT 时机，且只治这一个属性，其它延迟加载 BCL 仍会踩雷。
- 把清理移到最后但保留"重启前清理"的语义（如只删 versions+zip 不删 shared）→ 残留运行时占盘，需另设清理时机，违反 R3。
- agent 改自包含单文件 → 违背 FDD/零 NuGet/<8MB 既有设计（PC-049 同理），且体积膨胀到 ~70MB。
- 换回 ps1 轨 → 倒退，exe 轨是既定方向。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G2 | `ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs`（Pc050…） | `dotnet test` | 只扫顺序，不验证真实 BCL 加载时序（需真机更新闭环补测） |
| G3 | `scripts/check-problem-classes.ps1` S1 签名 | pre-commit / 每次归档校验 | 同上 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| [2026-11-24-update-agent-exe-cleanup-before-restart](../cases/2026-11-24-update-agent-exe-cleanup-before-restart.md) | 2026-11-24 | exe 轨首次真机更新，宿主退出后未再拉起；agent 日志 `System.Collections.Specialized` 加载失败 | 否（首次真机） |
