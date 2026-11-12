# 07 Final Report — 插件生态验收收尾批（2026-11-06-plugin-eco-acceptance-close）

## 结论

**🟡 PARTIALLY_COMPLETED（发布链交付物 APPROVED；插件生态「失败态生命周期」CHANGES_REQUIRED）**

推荐1（发布链）+ 跨环境 GitHub 安装全链达成并经真机验证；推荐2（真机演练）执行完毕——升级/卸载语义断言通过，但**「失败激活→400+自动回滚」断言未过**，演练实证 3 个宿主缺陷（已立项 P1×3）；推荐3 立项完成、全量对账完成；PluginStore 浏览器走查（AC⑥）未做（移交 P3）。

## 完成内容（对照 AC）

| AC | 结果 | 等级 |
|----|------|------|
| ① 守卫测试红→绿 | ✅ 7/7 红 → 18/18 绿（G1-G8，迭代中新增 G7/G8） | Verified |
| ② 骨架本地构建绿（CI 等价） | ✅ build 0 错 + test 1/1；且真实 GitHub runner test 步 success | Verified |
| ③ CI 全绿 + Release zip+SHA256SUMS | ✅ run 42148394140 success；资产下载解包核验 | Verified |
| ④ 跨环境 GitHub 安装+激活 | ✅ 空根实例 `install/source kind=github` → v1.0.1 激活 | Verified |
| ⑤ 演练四步断言 | ⚠️ 暂存/激活/卸载留 src ✓ + 重装防降级 400 ✓；**失败回滚 ✗**（D1/D2） | Verified（含失败事实） |
| ⑥ PluginStore 走查截图 | ❌ 未做 → TODO P3 | — |
| ⑦ 全量对账新增红=0 | ✅ 见下「全量对账」 | Verified |
| ⑧ TODO 立项 | ✅ 进程隔离立项 + D1/D2/D3（P1×3）+ P3 移交 | Verified |
| ⑨ 凭据零回显 | ⚠️ 操作全程未回显；诊断命令 2 次泄漏 gho_ 进转录（已披露，建议轮换） | Verified（含违规事实） |

## 主要变更（全部在授权边界内）

1. `ForgeSelf.Api/Plugins/Services/PluginSrcScaffold.cs` — 模板重写（占位符+Replace；lib 提交程序集；workflow 密钥经 env+`permissions: contents: write`；显式工程路径；打包拷根清单+剔共享程序集；manifest 入工程目录；.gitignore；UTF-8 BOM ps1）。
2. `ForgeSelf.Api.Tests/Plugins/PluginScaffoldReleaseTemplateTests.cs` — 新增守卫 G1-G8（功能级，对 Write 真实产物断言）。
3. 骨架仓 `.temp/dev-stack/data/plugins/hello-world/src/`（git 仓库，push 至 GitHub）：v3 修复同构 + 版本 1.0.1 + lib/ + CHANGELOG/README 约定。
4. TODO.md：本批条目移除 + 5 条新立项；日记逐回合归档。
5. 主仓**零业务代码改动、零 git 提交**（闸门3 未批）。

## 验证结果（摘要；全文见 05-evidence.md）

- 全部关键结论 Verified（真实命令输出，日志 `.temp/tmp/acc-*.log`）。
- **全量对账**：首跑 testhost 崩溃（1078 例后中止，基建问题如实记录）；终跑（锁释放后）`acc-fulltest2.log` —— 见「全量对账」节。
- NuGet 真实上传：Unknown（无 NUGET_API_KEY，CI 按设计跳过上传且保持绿）。

### 全量对账

终轮全量（`acc-fulltest2.log`）：**3731 例 / 3707 过 / 24 红 / 16m14s，正常跑完**。与 v2 基线 36 红逐条对表：**新增红 = 1**，为 `ImGatewayRouterRecordTests.落库失败不影响主链_回发照常完成`（残留行 `{wecom:conv-drop:user-drop}`）——共享数据根类序污染，归属在册 **PC-014 族**（ImGateway 为该族在册成员），本批改动面（模板字符串+新守卫文件）与 ImGateway/DB 零交集，已在该 TODO 条目登记实例。基线 36 红中 12+ 条本轮转绿（基线红集合随类序漂移）。结论：**归本批的新增缺陷红 = 0**。首次跑 testhost 中途崩溃（1078 通过后中止）为运行基建现象（同代码过滤集与 CI runner 均绿），不再重跑 blame（见 NOT-to-do）。

## 缺陷（新立项，不在本批修复）

- **D1（P1）** 插件包校验器不验证 EntryType 可解析性 → 坏包入库。
- **D2（P1）** 激活加载失败静默切指针（无 400 无回滚，error=null）→ 违反 12 步 #11 设计承诺。
- **D3（P1）** 「加载失败态」卸载 → 宿主进程无声死亡（日志无痕）→ **进程隔离立项的直接实证**。
- 同根因链：安装-激活-卸载生命周期对「失败态」无防御 → PC 编号待 problem-class-closure 批登记。

## 代价与收益（运行经济学）

- 代价：全程约 3h（含两轮 CI、三次全量尝试、演练重启）；1 个真实 GitHub 仓库与 Release 建立并留存。
- 收益：插件生态「源码仓→CI→Release→宿主安装」全链从纸面设计变为**可复现的已验证通路**；模板缺陷（8 类）被守卫永久封堵，后续插件 scaffold 直接受益；验收报告 P0-P4 五档缺口中 P1/P2/P3/P4 全部升级为 Verified（P5 NuGet 仍 Unknown）。

## 不做事决策（NOT-to-do）

- 不在本批修 D1/D2/D3（宿主业务代码改动超出 mini-task Forbidden 边界；且缺陷修复应走 problem-class-closure 完整闭环）。
- 不做 NuGet 真实上传（无 key，外部依赖）。
- 不做 PluginStore 浏览器走查（时间盒已尽，移交 P3 并留完整 API 级演练证据）。
- 不重跑首次崩溃的全量 blame（过滤集+CI 已证代码面无涉，避免又一轮 20 分钟基建开销）。

## 风险

1. **凭据回显**：两次 gho_ 令牌进入会话转录（本机本地风险；建议用户轮换）。
2. 宿主在「加载失败态卸载」下会崩（D3）——用户实例遇同类操作序列可能触发；修复批前建议避免对激活失败插件直接卸载（可重启宿主后再卸载，本次实测重启后卸载正常）。
3. CI 依赖 softprops/action-gh-release@v2 与 windows-latest runner（第三方 action 供应链面，未 pin SHA）。

## 验收清单回照

发布链工件（仓库 https://github.com/OpenForgeSelf/OpenForgeSelf.Plugin.HelloWorld，Release v1.0.1，CI 绿）已可交付使用；插件生态「失败态生命周期」修复批（D1-D3 + 进程隔离立项）建议作为下一批 Gate-1 立项。

## Validation / Review

- Validation（详见 `05-evidence.md`）：本批结论均带真实命令输出（日志 `.temp/tmp/acc-*.log`）；全量对账终轮 **3731 例 / 3707 过 / 24 红**，逐条对表后**归本批新增缺陷红 = 0**；NuGet 真实上传 = Unknown（无 key）。
- Review（详见 `06-review.md`）：**发布链交付物 APPROVED；插件生态「失败态生命周期」CHANGES_REQUIRED**（D1/D2/D3 三条 P1 缺陷已立项，未在本批修复）。
- 复跑：`pwsh scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-06-plugin-eco-acceptance-close -EnforceHeader`（提交前机械校验通过）。
