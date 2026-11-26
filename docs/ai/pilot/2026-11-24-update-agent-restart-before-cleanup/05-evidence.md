# 05 Evidence

## AC1：restart 早于 cleanup
- `UpdateAgentRunner.cs` RunAsync：步骤 8 = `RestartLauncher(o)`（line ~88），步骤 9 = `CleanupStagedAndBackups(o.InstallDir, o.StagedDir)`（line ~92）。
- 顶部步骤注释（line 18-21）含「PC-050 顺序不可交换」+ staged 双重身份 + JIT 延迟加载理由。
- Status：Verified（源码直读）。

## AC2：agent 单文件编译
- `dotnet publish ForgeSelf.UpdateAgent/... -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -m:1` → EXIT=0，`update-agent.exe` 195193 B（与修复前一致，步骤顺序不改变体积，符合预期）。
- Status：Verified。

## AC3：守卫与门禁
- G2 `Pc050_UpdateAgentRunner_MustRestartBeforeCleanupStaged` = Passed（1/1）。
- `check-problem-classes.ps1` = OK，40 个 PC 签名/allowlist/guards 全通过。
- Registry + Pc050 守卫 = 6/6 全绿（含 Cases_Resolve / Ledgers_ListEvery / FrontMatter / Allowlist / Guards_Active）。
- Status：Verified。

## AC4：归档自洽
- PC-050 + case 已建；PC README / cases README / index 三处索引已登记；Registry 测试全绿证明互相引用解析正确（首轮 case front-matter 字段名错已修正为 id/pc/evidence）。
- Status：Verified。

## Known Limitations
- 真机完整更新闭环未实测：本沙箱完整发布链连续 MSB4166（MSBuild 并行节点崩溃），属环境约束；用 `-m:1` 单组件 publish 已验证 agent 可编译，但整包重打包未在本沙箱完成。需正常终端跑一次更新确认宿主被拉起。
- 回滚/启动健康检查缺失（用户诉求，另批）。
