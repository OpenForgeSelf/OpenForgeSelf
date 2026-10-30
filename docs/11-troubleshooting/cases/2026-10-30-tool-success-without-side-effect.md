---
id: 2026-10-30-tool-success-without-side-effect
title: 执行体报 success 但真实副作用未落定（write_file 零落盘）
pc: PC-018
severity: P1
evidence: Verified
---
# Case：2026-10-30 write_file 报成功但零落盘（PC-017 修复后复测现形）

> Case 只讲「这一次发生了什么」；规则/修法/扫描方法一律引用 PC-018，不复述。
> 事件链上游：PC-017（write_file schema 重复键，网关层必挂）修复并升级到 2.4.20 后，按用户 goal 做 fs 复测时发现本缺陷。

## 1 现象

升级到 2.4.20.2610301232 后经 fs 万能工具复测：`write_file` 返回
`{"success":true,"path":"…\\.temp\\fs-writefile-probe.md","writtenLength":29,"totalLines":1,…}`
——但紧接的 `read_file` 回读报 `文件不存在：…fs-writefile-probe.md`（同一绝对路径）。pwsh `Get-Item` 独立复核同样不存在；全仓递归搜 `fs-writefile-probe.md` 零命中。**报成功、磁盘无文件。**

## 2 复现红灯

- 现场层：:51888（2.4.20）fs write_file → success:true → read_file 报文件不存在（12:47 实测，本会话记录）。
- 测试层：新增守卫 `ForgeSelf.Api.Tests/Unit/FileToolsWritePersistTests.cs`；将单行修复（补 `File.WriteAllText`）stash 后运行 → **3 failed / 1 passed**（新建/替换/追加三个落盘断言全红，负例正常通过）——证明守卫拦得住该缺陷。

## 3 根因（5 Whys + 第 6 问）

1. 为什么 success 但没文件？→ `WriteFileToolFunction.ExecuteAsync` 拼好 `final`、建好父目录后直接构造 success 响应，**没有 `File.WriteAllText` 调用**。
2. 为什么会没有？→ 2026-10-29 FileTools 坏合并的最小语法修复批重排了该方法代码段，落盘调用在重排中丢失；该文件在 git 的首个提交（68df669）就已没有此调用——坏档已入库。
3. 为什么之前没人发现？→ 重复键（PC-017）让 write_file 的所有调用在网关层必挂，深层缺陷被完全遮蔽。
4. 为什么构建/测试没拦？→ 该方法无任何持久化断言测试；既有 FileTools 测试只覆盖 cwd 校验与目录鉴权。
5. 为什么验收没拦？→ 10-29 修复批的验收止步「构建绿」，没有成功路径的端到端验证。
- **第 6 问：为什么现有流程/守卫没让我一开始就写对？** ①缺「副作用类工具必须配副作用对象回读断言」的类级守卫（本批补 G2）；②「修好报错」不等于「功能恢复」——缺陷修复验收应固定包含成功路径端到端复验（已入 PC-018 lens）。

## 4 同类扫查命中表

| 位置（文件:行） | 同根因？ | 处置（已修 / 已守卫 / TODO#） |
|-----------------|----------|-------------------------------|
| Plugins/FileTools/Services/FileToolFunctions.cs（修复前 315-325 行段） | 是（本体） | 已修（补 `File.WriteAllText(absPath, final)`） |
| FileTools 其余副作用工具（压缩/清理/重命名等） | 形态同类、未验证 | TODO：扩面持久化回读断言 |
| PC-004（Agent 委派成功回报 ≠ 进程真实退出，候选） | 类属（回报完整性侧），根因独立 | 互留注记，待其定性后评估合并 |

## 5 修复与被否方案

修法 = PC-018 canonical fix：成功分支补真实落盘调用。被否方案见 PC-018 §3（以响应体字段当凭证）。

## 6 重构提案与结论

R0 不重构（单语句缺失）；R1 提案记 TODO：FileTools 其余副作用工具扩面「回读断言」。

## 7 守卫落地

G2 `ForgeSelf.Api.Tests/Unit/FileToolsWritePersistTests.cs`（4 用例；红→绿实证：stash 探针 3 failed / 1 passed → 恢复修复后 36/36 全绿，含 FileTools 域回归）。

## 8 逃逸分析（二次复发必做，首见可写「N/A 首次」）

N/A 首次（被 PC-017 遮蔽属「上游遮蔽」形态，已写入 PC-018 诱因③）。

## 9 基建改进

①write_file 持久化守卫入库（FileTools 域过滤集必跑）；②缺陷修复验收动作新增「成功路径端到端复验」；③TODO 登记：FileTools 其余副作用工具扩面回读断言。

## 10 复用赋能

一切「有副作用的工具/端点」的新增与重构（写文件、起进程、写库、发请求）——验收必须含「以副作用对象回读为准」的断言；修复「报错类」缺陷必须跑成功路径端到端复验（lens 已入 PC-018）。

## 11 证据

| 结论 | 等级 | 出处（文件:行 / 命令输出 / 截图） |
|------|------|-----------------------------------|
| fs write_file success 但 read_file 文件不存在 | Verified | :51888（2.4.20）fs 调用对 12:47 实测 |
| 全仓无该文件（零落盘） | Verified | pwsh 递归搜索零命中 + Get-Item 无结果 |
| 修复前源码无 WriteAllText（工作区与 HEAD 一致） | Verified | grep 零命中 + `git show HEAD:…` 零命中 |
| 守卫红灯 3 failed / 1 passed（stash 探针） | Verified | .temp/results-schema-guard/persist-red 日志 |
| 修复后 36/36 绿 | Verified | .temp/results-schema-guard/green-run3c.log，EXIT=0 |
| 2.4.20 包内即坏档（68df669 首提交已缺调用） | Verified | git log --follow 单提交；2.4.20 实例行为与源码一致 |
