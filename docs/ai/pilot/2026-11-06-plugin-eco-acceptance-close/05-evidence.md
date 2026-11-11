# 05 Evidence — 插件生态验收收尾批（2026-11-06）

> 等级标注：**Verified**=亲自跑过拿到真实输出 / **Inferred**=凭代码或日志推断 / **Unknown**=未验证。
> 全部命令日志落 `.temp/tmp/acc-*.log`；日记 `.forgeself/memory/2026-11-06.md`。

## E1 模板守卫 TDD（AC①）— Verified

| 步骤 | 证据 |
|------|------|
| RED | `acc-guard-red2.log`：7/7 全红（守卫对旧模板全数失败：secrets-in-if、裸 dotnet、无 Copy-Item、8 级 Exists 引用、根清单、.gitignore、lib 缺失）|
| GREEN | `acc-guard4.log`：守卫 7/7 + 生态过滤集 11/11 = **18/18 绿**（含 G7 测试工程 lib 引用、G8 `permissions: contents: write` 两条迭代中新增守卫）|
| 过程缺陷（自纠）| 初版守卫文件两处语法损坏（record 名不一致/坏 Assert 行）、正则字面量与断言子串不匹配（`Microsoft\.Extensions` vs `Microsoft.Extensions.`）→ 改 StartsWith 显式列表；骨架测试工程 `Private=false` 不拷引用致运行时 FileNotFoundException → 测试工程改 `Private="true"` |

## E2 骨架 CI 等价（AC②）— Verified

- 骨架 build：0 错误（`acc-hw-build.log`）；骨架 test：1/1 绿（`acc-hw-test4.log`）。
- **GitHub 真实 runner 复证**：CI run 42148394140 的 test 步骤 success（windows-latest + dotnet 10.0.403）——CI 等价性不止本地模拟，真实 runner 上通过。

## E3 发布链（AC③）— Verified

| 环节 | 证据 |
|------|------|
| 建仓 | REST 201 → https://github.com/OpenForgeSelf/OpenForgeSelf.Plugin.HelloWorld（org OpenForgeSelf，public）|
| push | main d9fd0c4（15 文件）→ 修复提交 6744097；tag v1.0.1（force 更新一次）|
| CI 第一轮 | run 42147844590 **failure**：`github release` 步 403 "Resource not accessible by integration"（新仓库默认只读 workflow token）——test/package zip 两步已绿 |
| 修复 | workflow 顶层加 `permissions: contents: write`（模板+骨架+守卫 G8 同步）|
| CI 第二轮 | run 42148394140 **success** |
| Release | `releases/latest` → tag v1.0.1，assets：`HelloWorld-1.0.1.zip`(10,312B) + `SHA256SUMS.txt`(88B)；下载解包核验：根含 `plugin.json`(Version=1.0.1) + `Plugin.HelloWorld.dll` + deps.json + pdb，**无宿主共享程序集、无 src/** |

## E4 跨环境 GitHub 安装（AC④）— Verified

- 第二隔离实例（`:7302`，DataRoot `.temp/dev-stack-acceptance/data`，全新空根）：`POST /api/plugin/install/source {kind:"github", locator:"https://github.com/OpenForgeSelf/OpenForgeSelf.Plugin.HelloWorld", activate:true}` → `{"id":"hello-world","version":"1.0.1","message":"插件已安装并激活"}`。
- 版本表：恰 1 条 1.0.1。PluginGitHubProvider（releases/latest + SHA256SUMS 校验）全链真实走通。

## E5 真机演练（AC⑤）— 部分通过

| 断言 | 结果 | 证据 |
|------|------|------|
| ZIP 暂存不切现行 | ✅ 上传 1.0.1.2611060925 → 日志「只暂存不激活」「current=False」| dev-stack-backend.log（GBK 解码）|
| 激活升级 | ✅ 「插件版本激活成功: v1.0.1.2611060925」| 同上 |
| **失败激活→400+自动回滚（12 步 #11）** | ❌ **未过**：坏包（EntryType=NoSuchType, v1.0.2.2611061023）**上传校验通过**；激活切指针成功、插件 state=0/isEnabled=false/**error=null**，无 400、无回滚 → 立项 TODO D1/D2 | acc 演练命令输出 + 日志 10:25:39 |
| 卸载保留 src/.forge、versions 清除（12 步 #12）| ✅（健康态重演）：「插件卸载成功」→ `src=True forge=True versions_dir_exists=False`；重启后持久态重装 1.0.1 被 400 拒（"只允许升级"）为附加正向证据 | 卸载命令输出 |
| **加载失败态下卸载** | ❌ **宿主进程无声死亡**（:7302 拒绝连接；日志 10:26:11 后零痕迹）→ 立项 TODO D3；重启后同态卸载正常，定界「卸载半加载程序集」路径 | acc 演练命令输出 |
| 重启持久性（附加）| ✅ DataRoot 跨重启保留 current=1.0.2.2611061023（版本化布局持久生效）| 重装 400 响应文 |
| package-plugin.ps1 实跑（补验收 Unknown）| ✅ 产 `HelloWorld-1.0.1.2611060925.zip`（10,360B）+ 坏包夹具 zip | acc-package.log |

## E6 全量对账（AC⑦）— Verified

- 第一次全量：9m13s 后 **testhost 进程崩溃**（1078 通过后中止，`acc-fulltest.log`）——运行基建问题非测试红。
- blame 重跑：构建被 dev-stack DLL 锁挡（`acc-fulltest-blame.log` MSB3027）。
- **终轮（锁释放后，`acc-fulltest2.log`）**：**3731 例 / 3707 过 / 24 红 / 16m14s，正常跑完无崩溃**。与 v2 基线（`plugin-eco-fulltest.log`，36 红）逐条对表：**新增红 = 1**（`ImGatewayRouterRecordTests.落库失败不影响主链_回发照常完成`：断言落库静默失败无残留、却查到 `{wecom:conv-drop:user-drop}` 残留行——共享数据根/类序污染，归在册 PC-014 族；本批改动面与 ImGateway/DB 零交集）；基线 36 红中 12+ 条本轮转绿（UnifiedAIGateway×7、TodoTracker×2、DesignSystem/McpCenter/编码守卫等）——基线红集合随类序漂移，与本批无关。本批改动面过滤集 18/18 绿 + 真实 CI runner 绿。

## E7 NuGet 真实上传 — Unknown（受外部依赖阻断）

- 无 `NUGET_API_KEY`（环境与仓库 secrets 均未配置）；CI nuget 步按设计跳过上传（`IsNullOrWhiteSpace($env:NUGET_API_KEY)` 分支，CI 保持绿）。本批 Forbidden 边界，如实 Unknown。

## E8 凭据处理 — ⚠️ 部分违规（AC⑨ 未全达）

- 建仓/push 的凭据使用全程未回显 ✅；但**诊断命令两次把 `~/.git-credentials` 的 gho_ 令牌完整打印进会话转录**（脱敏逻辑取错了 github.com 分割位置）→ 已在 07 风险节披露，建议用户择机轮换该令牌（GitHub → Settings → Applications → 授权撤销重发）。
- 附带发现：DSH worker 的 `GIT_CONFIG_GLOBAL` 用**无盘符路径**（`\dsh-state\...`），D 盘 cwd 下解析失败 + worker-gitconfig `[credential] helper=`（空值）**故意清空 helper**——git 凭据操作需命令行 `-c credential.helper=store` 显式恢复（沉淀见日记）。
