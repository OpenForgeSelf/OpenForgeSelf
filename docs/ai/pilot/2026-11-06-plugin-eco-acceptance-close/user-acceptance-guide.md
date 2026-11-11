# HelloWorld 独立插件生态 · 用户验收手册（文本版）

> 对应批次：`docs/ai/pilot/2026-11-06-plugin-eco-acceptance-close/`（mini-task + 05-evidence + 06-review + 07-final-report）
> 编写时间：2026-11-06 19:4x；编写时已复核：Release v1.0.1 与资产在线、CI run 42148394140=success、本地包/坏包夹具/骨架仓均在。
> 用法：按 A→F 顺序逐项执行，每项末尾有「预期结果」与「判定」；G 是总判定表，逐格打勾即完成验收。
> 已知缺陷（D1-D3）在 E 节：复现步骤与「缺陷行为」如实写明，供你确认后拍板修复批。

---

## 0. 前置准备（约 2 分钟）

1. 打开 PowerShell 7（**必须是 `pwsh`，不要用 powershell 5.1**），进入仓库根：
   ```powershell
   cd D:\src\my-proj\OpenForgeSelf\OpenForgeSelf
   ```
2. 本机环境三件套（跳过会出假红/白跑，见 AGENTS.md §5.0）：
   ```powershell
   $env:NO_PROXY='localhost,127.0.0.1,::1'
   $env:TEMP="$PWD\.temp\tmp"; $env:TMP=$env:TEMP
   ```
3. 验收全程只用你自建的隔离实例；**不要碰 `:51888` 与任何用户运行中的宿主**。
4. 验收结束统一清理：`pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\dev-stack.ps1 -Stop`。

---

## A. 发布链验收（GitHub 侧，无需本地实例，约 5 分钟）

### A1 仓库存在且公开
```powershell
Invoke-RestMethod 'https://api.github.com/repos/OpenForgeSelf/OpenForgeSelf.Plugin.HelloWorld' |
  Select-Object full_name, private, default_branch
```
- **预期**：`full_name=OpenForgeSelf/OpenForgeSelf.Plugin.HelloWorld`，`private=False`，`default_branch=main`
- **判定**：☐ 通过

### A2 CI 最新一次全绿
```powershell
(Invoke-RestMethod 'https://api.github.com/repos/OpenForgeSelf/OpenForgeSelf.Plugin.HelloWorld/actions/runs?per_page=2').workflow_runs |
  Select-Object id, status, conclusion, head_sha
```
- **预期**：`id=42148394140`，`status=completed`，`conclusion=success`（head `6744097`）；更早一条 `42147844590=failure` 是修复前的 403 历史，属正常迭代记录
- **判定**：☐ 通过

### A3 Release 与资产
```powershell
$rel = Invoke-RestMethod 'https://api.github.com/repos/OpenForgeSelf/OpenForgeSelf.Plugin.HelloWorld/releases/latest'
$rel | Select-Object tag_name, name
$rel.assets | Select-Object name, size
```
- **预期**：`tag_name=v1.0.1`；资产两件：`HelloWorld-1.0.1.zip`（**10312 B**）、`SHA256SUMS.txt`（**88 B**）
- **判定**：☐ 通过

### A4 包内容核验（人工，约 3 分钟）
1. 浏览器打开 https://github.com/OpenForgeSelf/OpenForgeSelf.Plugin.HelloWorld/releases/tag/v1.0.1 ，下载 `HelloWorld-1.0.1.zip`。
2. 解压后检查 zip 根：
   - ☐ 有 `plugin.json`，`Version=1.0.1`
   - ☐ 有 `Plugin.HelloWorld.dll` + `Plugin.HelloWorld.deps.json` + `Plugin.HelloWorld.pdb`
   - ☐ **没有** `ForgeSelf.*.dll`、`NewLife.*.dll`、`Microsoft.Extensions.*.dll`、`XCode.dll`、`MX.dll`（宿主共享程序集必须被剔除）
   - ☐ **没有** `src/`、`tests/`、`lib/`
3. SHA256 对照（可选）：
   ```powershell
   Get-FileHash <下载的zip路径> -Algorithm SHA256
   # 与 SHA256SUMS.txt 内数值比对一致
   ```
- **判定**：☐ 通过

---

## B. 守卫与模板门禁验收（本地，约 3-5 分钟，含构建）

```powershell
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~PluginScaffoldReleaseTemplate|FullyQualifiedName~PluginEcosystem" --nologo
```
- **预期**（判定看输出正文，不看退出码）：`已通过! - 失败: 0，通过: 18`（7 条脚手架守卫 G1-G8 + 11 条插件生态既有测试）
- **说明**：这 7 条守卫把模板八类缺陷永久封堵（secrets-in-if、裸 dotnet 命令、不拷根清单、不剔共享程序集、8 级相对引用、manifest 落根、缺 .gitignore、缺 `permissions: contents: write`）；以后任何改动让模板回退，这里必红。
- **判定**：☐ 通过

---

## C. 跨环境 GitHub 安装验收（隔离实例真装，约 8 分钟）

### C1 起隔离实例（全新数据根）
```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\dev-stack.ps1 -BackendPort 7302 -FrontendPort 7398 -DataRoot "$PWD\.temp\dev-stack-user-accept\data" -SkipBuild
```
- 脚本可能阻塞不退——**属正常**（宿主已拉起）；另开一个 pwsh 窗口继续 C2。
- 端口被占会自动顺延：以 `.temp\dev-stack.json` 为准：
  ```powershell
  $s = Get-Content .temp\dev-stack.json | ConvertFrom-Json
  $tok = $s.token; $base = "http://localhost:$($s.backendPort)"
  $tok; $base   # token 只在本机用，别外传
  ```

### C2 真源安装（拉取真实 GitHub Release）
```powershell
$body = @{ kind='github'; locator='https://github.com/OpenForgeSelf/OpenForgeSelf.Plugin.HelloWorld'; activate=$true } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "$base/api/plugin/install/source" -Headers @{Authorization="Bearer $tok"} -ContentType 'application/json' -Body $body
```
- **预期**：`{"data":{"id":"hello-world","version":"1.0.1"}...,"message":"插件已安装并激活","success":true}`
- **判定**：☐ 通过（这一步证明「源码仓 → CI → Release → 宿主安装」全链在你机器上可复现）

### C3 版本表与磁盘布局
```powershell
(Invoke-RestMethod "$base/api/plugin/hello-world/versions" -Headers @{Authorization="Bearer $tok"}).data
$pd = "$($s.dataRoot)\plugins\hello-world"
Get-ChildItem $pd | Select-Object Name
Get-ChildItem "$pd\versions" | Select-Object Name
```
- **预期**：版本表恰 1 条 `1.0.1`；磁盘有 `src/`、`.forge/`、`versions/`（内含 `1.0.1/`）、`current`、`plugin.json`
- **判定**：☐ 通过

---

## D. 升级 / 暂存 / 卸载语义验收（约 6 分钟）

### D1 本地包上传 = 只暂存、不切现行
```powershell
Invoke-RestMethod -Method Post -Uri "$base/api/plugin/install" -Headers @{Authorization="Bearer $tok"} -Form @{ file = Get-Item "artifacts\plugin-packages\HelloWorld-1.0.1.2611060925.zip" }
```
- **预期**：返回 hello-world，`version=1.0.1.2611060925`，`isEnabled=False`（未激活）；磁盘 `versions/` 新增 `1.0.1.2611060925/` 目录
- **判定**：☐ 通过

### D2 激活指定版本（升级）
```powershell
Invoke-RestMethod -Method Post -Uri "$base/api/plugin/activate/hello-world" -Headers @{Authorization="Bearer $tok"} -ContentType 'application/json' -Body '{"version":"1.0.1.2611060925"}'
```
- **预期**：`"message":"插件已激活","success":true`
- **判定**：☐ 通过

### D3 卸载保留源码、清除版本
```powershell
Invoke-RestMethod -Method Post -Uri "$base/api/plugin/uninstall/hello-world" -Headers @{Authorization="Bearer $tok"}
"src=$(Test-Path "$pd\src") forge=$(Test-Path "$pd\.forge") versions=$(Test-Path "$pd\versions")"
```
- **预期**：`"插件卸载成功"`；随后输出 `src=True forge=True versions=False`
- **已知观察**（非缺陷）：卸载后立刻 `GET /api/plugin` 可能仍列出 hello-world（扫描缓存/根清单残留，已在批内记录）——以磁盘核验为准
- **判定**：☐ 通过

### D4 防降级保护（可选加分项）
> 需先重新 C2 装回 1.0.1（保持 current=1.0.1），再上传旧包后激活旧版本；或直接用我批内实测的反向证据：current 已是更高版本时重装 1.0.1 → `400 包版本 1.0.1 未高于当前生效版本…（版本化布局只允许升级）`。
- **判定**：☐ 通过 / ☐ 跳过

---

## E. 已知缺陷复现（选做；**会让实例崩，先做完 D 再做**，约 5 分钟）

> 三个缺陷同根因链：安装-激活-卸载生命周期对「失败态」无防御。已在 TODO 立项 P1×3。修复批完成后，本节三步应分别变为：400 拒收 / 400+自动回滚 / 卸载正常——可直接当修复批的验收步骤用。

### E1【D1】坏包能通过校验入库
```powershell
Invoke-RestMethod -Method Post -Uri "$base/api/plugin/install" -Headers @{Authorization="Bearer $tok"} -Form @{ file = Get-Item "artifacts\plugin-packages\HelloWorld-1.0.2.2611061023.zip" }
```
- **缺陷行为（当前）**：返回成功，坏包（EntryType 指向不存在的 `Plugin.HelloWorld.NoSuchType`）入库暂存
- **应为**：校验器解析 EntryType 失败 → 400 拒收

### E2【D2】激活失败静默切指针
```powershell
Invoke-RestMethod -Method Post -Uri "$base/api/plugin/activate/hello-world" -Headers @{Authorization="Bearer $tok"} -ContentType 'application/json' -Body '{"version":"1.0.2.2611061023"}'
(Invoke-RestMethod "$base/api/plugin/hello-world" -Headers @{Authorization="Bearer $tok"}).data | Select-Object version, state, isEnabled, error
```
- **缺陷行为（当前）**：返回「插件已激活」；详情 `version=1.0.2.2611061023, state=0, isEnabled=False, error=null`——无 400、无回滚、错误信息丢失
- **应为**：400 + current 回滚 + error 落库

### E3【D3】失败态下卸载 → 宿主进程死亡（⚠️ 复现即崩，做完请 -Stop）
```powershell
Invoke-RestMethod -Method Post -Uri "$base/api/plugin/uninstall/hello-world" -Headers @{Authorization="Bearer $tok"}
```
- **缺陷行为（当前）**：宿主进程无声死亡（后续请求「无法连接」，后端日志无任何异常痕迹）
- **应为**：卸载成功或明确报错，宿主存活
- **规避**（在修复前）：先 `-Stop` 重启实例再卸载（批内实测重启后同态卸载正常）

---

## F. 立项与归档验收（约 3 分钟，读文件为主）

1. TODO 立项存在（4 条）：
   ```powershell
   Select-String -Path TODO.md -Pattern '插件包校验器不验证 EntryType|激活加载失败静默切指针|卸载致宿主进程无声死亡|进程隔离门槛立项'
   ```
   ☐ 四条均在
2. 工件齐（本目录）：`mini-task.md`、`05-evidence.md`、`06-review.md`、`07-final-report.md` ☐
3. 全量对账数字（读 `.temp/tmp/acc-fulltest2.log` 末行）：`3731 例 / 3707 过 / 24 红`；对 v2 基线 36 红 → **新增红=1**（`ImGatewayRouterRecordTests.落库失败…`，归在册 PC-014 数据根污染族，本批改动面零交集）☐
4. 未越权项：主仓无本批 git 提交（`git log --oneline -3` 最新提交不含本批改动）；`:51888` 未被触碰 ☐

---

## G. 总判定表

| 项 | 内容 | 判定 |
|----|------|------|
| A1-A4 | 发布链（仓库/CI/Release/包内容） | ☐ |
| B | 模板守卫 18/18 | ☐ |
| C | 跨环境 GitHub 真装+激活 | ☐ |
| D | 暂存/升级/卸载语义 | ☐ |
| E（选做） | D1/D2/D3 缺陷确认 | ☐ |
| F | 立项+归档+对账+未越权 | ☐ |

**不算本批验收项的两处（避免误判）**：① NuGet 真实上传＝Unknown（无 NUGET_API_KEY，CI 按设计跳过上传且保持绿）；② PluginStore 浏览器走查截图＝未做（P3 在册移交）。

**验收结论记录**（由你填写）：＿＿＿＿＿＿（全过 / 哪项不过+现象）
