# 05 Evidence — 验证证据

## AC1：无条件显式 SelfContained=false
- Verified：`ForgeSelf.UpdateAgent.csproj:20` = `<SelfContained>false</SelfContained>`，全仓 grep 无条件式 SelfContained 遗留（Bootstrapper 无条件 true 属独立发布正例）。
- 出处：`git diff ForgeSelf.UpdateAgent/ForgeSelf.UpdateAgent.csproj`。

## AC2：publish 成功且 FDD 形态
- 复现红灯：`.temp/netsdk1151-diag.log` — `dotnet publish … -p:PublishSingleFile=true` EXIT=1，NETSDK1151。
- 对照：`.temp/netsdk1151-diag2.log` — `dotnet build` 同参数 EXIT=0（证明 publish-only 推断）。
- 修复后：`.temp/netsdk1151-m1.log` — publish EXIT=0；产物 `.temp/publish-m1/ForgeSelf.exe` 17,069,550 字节，无 `Microsoft.NETCore.App/` 自包含目录，`plugins/` 与 `wwwroot/` 齐备。
- 注：`-m:1` 仅用于规避本沙箱 MSBuild 并行节点崩溃（MSB4166），非产品代码要求。

## AC3：守卫与台账
- Verified：`.temp/pc049.trx` — `Pc049_UpdateAgentCsproj_MustDeclareFddSelfContainedUnconditionally = Passed`。
- Verified：`check-problem-classes.ps1` — `39 个 PC 签名/allowlist/guards 全部通过` EXIT=0。
- Verified：`.temp/pc049b.trx` — Registry + PC-049 守卫 6/6 Passed。

## AC4：本地 zip 产出与校验
- Verified：`artifacts/release/OpenForgeSelf-2.4.22.2611241442-win-x64.zip`，149,948,841 字节（143 MB）。
- Verified：`sha256sum -c`（CRLF 规整后）→ `OK`；哈希 `ff984a49532b92f24cd67ef3f47c119d08a50b53124a5189be29258641ac4ede`。
- Verified：zip 内容含 `ForgeSelf.exe`（launcher）、`update-agent.exe`、`versions/current`、`versions/2.4.22.2611241442/ForgeSelf.exe`（17 MB）、`host/fxr/`、`shared/Microsoft.AspNetCore.App/10.0.13/`（FDD 运行时）、SHA256SUMS.txt、RELEASE-NOTES。
- Verified：发布链 `-SkipFrontend` EXIT=0，`release-local: ALL DONE in 85s`。
