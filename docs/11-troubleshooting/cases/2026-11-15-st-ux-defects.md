---
id: 2026-11-15-st-ux-defects
title: 智能终端三缺陷实测（~ 起始目录 400 / GBK 偶合合法 UTF-8 乱码 / cmd 后 cwd 不更新）
pc: PC-011
severity: P1
evidence: Verified
---
# Case 2026-11-15 智能终端三缺陷实测（~ 起始目录 400 / GBK 拉丁扩展乱码 / cmd 后 cwd 不更新）

> 事件档案（一次输入三个实测缺陷，分属三个问题类）。流程与裁决见 pilot `docs/ai/pilot/2026-11-15-st-ux-defects/`。
> 归属：A → PC-035（起始目录占位符）；B → PC-011（**逃逸**：合法 UTF-8 偶合 ANSI）；C → PC-036（cmd cwd 采集缺失）。

## 现象（用户实测，:51888 实例 v2.0.1）

| # | 现象 | 复现证据 |
|---|------|----------|
| A | 新增会话选类型后默认起始目录 `~`，确定即报「工作目录不存在: <宿主进程目录>\~」 | 探针 POST sessions（`cwd:"~"`）→ 400，报错串含宿主进程 cwd 全路径（泄露） |
| B | 终端里 `ls` 输出表头显示「Ŀ¼: <盘>:\…」 | PowerShell 5.1 会话 `ls`，GBK「目录」(C4 BF C2 BC) 恰为合法 UTF-8 拉丁扩展（U+013F U+00BC） |
| C | `cd <盘>:\` 后页面当前目录徽标不更新 | cmd 会话 block JSON 无 cwd；会话态 Cwd 仅 PowerShell 分支由 `::cwd` 更新 |

## 根因

- **A（PC-035）**：`TerminalSessionService.V2.cs ResolveLocalCwd` 与 V1 创建路径对 `~` 直接 `Path.GetFullPath` → 相对宿主进程 cwd 解析成不存在路径；报错串拼接解析后的完整路径 = 泄露宿主路径。该缺陷 2026-11-13 pilot BUG#1 已分诊为延期 TODO，本次实测命中 = 延期债到期（同类第二次出现）。
- **B（PC-011 逃逸）**：PC-011 裁决器「严格 UTF-8 合法即采信」的启发式只验证**形式**（字节是合法 UTF-8），不验证**语义合理性**；GBK 双字节偶合出合法 UTF-8 拉丁扩展序列时整行被误判 UTF-8。守卫没拦的原因（逃逸分析）：G1 测试基建 `ToAnsiBytesOrThrow` 主动拒收「对 UTF-8 也合法」的撞车形态（注释称"理论上不发生"）——**测试宇宙自己把逃逸形态排除在外**，G2 token 扫描与 G3 签名对启发式语义盲区原理性不可达。
- **C（PC-036）**：四层叠加：① `SessionShell.BuildWrappedCommand` cmd 分支只发标记不采集 cwd（`%VAR%` 在整行解析期展开，同行拿不到执行后值；实测 `call echo`/`%%` 转义均不可用）；② `SubmitCommandResponse` 无 cwd 字段，服务端已更新的 `s.Cwd` 不随响应下行；③ 前端 tab 徽标只在建会话时设置，命令执行后不回写；④ 前端 ps1() 优先用会话创建时固化的 `s.prompt`，blocks 轮询不消费 `pack.prompt`——②③ 修复后 live 实测仍不上屏，第四层才补（见 PC-036 §1）。

## 交付形态实测裁决（结构性发现：sidecar 不能携带宿主侧库修复）

修复落位横跨两个程序集：A/C 在插件 `SmartTerminal.dll`（sidecar 2.0.2 可送达运行实例），**B 的裁决器在宿主侧 `ForgeSelf.Core`**。2026-11-15 live 实测（:51888，宿主 2.4.22）证实：sidecar 换 2.0.2 后 A/C 生效、**B 仍乱码（`Ŀ¼:` 仍在页面上）**——运行实例的 Core 是宿主版本层（11-13 构建），插件 ALC 经 deps.json/beside 回退到宿主 Core。**不能把修复版 Core.dll 私有打进插件版本目录**：`IContext`/`IChatCompletion` 等插件-宿主共享契约定义在 Core（`StHostGateway` 经 IContext 取宿主服务），私有 Core 会把契约类型劈成两份，插件直接崩。裁决（`PluginLoadContext.Load`：resolver → beside-dll → Default ALC 回退）：**宿主侧库修复只能随宿主更新发布**（update-agent 动线），sidecar 流只覆盖插件自身程序集内的变更。live spec 的 B 段按宿主版本门控（≥ 2.4.23 才断言），宿主更新后自动生效。已回写 packaging 真源 §1.8。

## 运行实例 live 复验（2.0.2 sidecar + :51888，2026-11-15）

- 授权：沿用输入20 对本插件/实例流的侧载+实测授权；live spec 建真实会话并**全部清理**（含前几轮失败运行泄漏的 5 个探针会话，经会话 API close，余 0）。
- A（PC-035）✅：新增会话对话框起始目录保持默认 `~` 直接创建成功，无 400/报错横幅（截图 live-st-202-dialog-tilde / live-st-202-ps-created）。
- B（PC-011）⏸️ 门控：宿主未携带裁决器修复，live 断言待宿主 ≥ 2.4.23 后执行；修复本体 + 测试已绿（Core 单测 + 插件集成测试）。
- C（PC-036）✅：PowerShell 7+ 与 cmd 两会话 `cd <盘>:\` 后提示符均实时更新为 `<盘>:\ $`（截图 live-st-202-ps-cd / live-st-202-cmd-cd）；cmd 块内可见两行协议回显。
- 用户 shell 实测补充：用户会话为 PowerShell 7+（非 5.1），重定向输出仍走控制台码页 GBK——B 的耦合字节机制与 shell 版本无关。

## 修法（canonical fix）

- **A**：抽 `StStartDir.Resolve` 单一真源（V1+V2 共用）：`~`/`~/x` 展开 UserProfile，环境变量展开，报错只回显用户输入原文（根路径与 `~` 形态展示解析后路径，相对路径不拼接宿主 cwd）。
- **B**：裁决器 2.5 步「怀疑门」：严格 UTF-8 成功后，若文本含 U+00A0–U+024F 可疑拉丁扩展且全文无 CJK，试 ANSI 往返，成功且结果含 CJK 才翻案。已知原理性局限：真·纯拉丁重音 UTF-8 文本（café）在 CJK 码页环境偏向 ANSI（不可分，按使用环境概率裁）。
- **C**：cmd 两行采集协议（标记行结束后再发 `echo <token2>::%CD%`，第二行在命令完成后才被解析展开）；`SubmitCommandResponse.Cwd` 下行；前端 run 与批准两条路径都回写 `s.cwd`；blocks 轮询消费 `pack.prompt`（服务端 `BuildPrompt` 为提示符格式单一真源）。

## 守卫

| 类 | 守卫 | 位置 |
|----|------|------|
| PC-035 | G1 | `ForgeSelf.Api.Tests/Plugins/SmartTerminalTests/StStartDirAndSessionShellTests.cs`（StStartDirTests：`~` 展开 / 脱敏回归 / 根路径回显） |
| PC-011 | G1 强化 | `ForgeSelf.Core.Tests/ProcessOutputTextTests.cs` GbkChineseBytes_CoupledAsValidUtf8_PreferAnsiWhenAnsiYieldsCjk + 对照护栏 Utf8ChineseWithLatinAccent_KeepsUtf8Verdict；测试基建新增 `ToAnsiBytesAllowingUtf8Collision`（旧 helper 的"不可构造"假设即逃逸成因，永久留档） |
| PC-036 | G1 | 同文件 SessionShellCmdCwdTests：真实 cmd 进程 `cd` 后 Run 返回 Cwd 必须反映新目录 |

## 教训

1. **测试基建的"不可构造"假设要过逃逸审查**——修不了问题先修测试宇宙的视野（本例：helper 拒收 = 盲区永测不到）。
2. **启发式裁决只验形式不验语义时，必须配"反例变绿 + 正例不翻案"成对测试**（B 的两个测试即此形态）。
3. cmd 交互式展开时机（整行解析期）是会话层协议设计的硬约束；`call`/`%%` 转义在交互态不可用（实测），两行协议是唯一可靠形态。
4. **修复落位先问「改动在哪个程序集」**：宿主侧库修复不随插件 sidecar 走（共享契约类型身份不可劈裂）；跨程序集修复必须声明交付边界，否则"测试绿"与"用户可见"脱节（本例 B 直至宿主更新前仍乱码）。
5. **前端展示的快照字段优先级**：会话快照字段（prompt）优先于实时回写字段（cwd）时，轮询必须同步消费服务端刷新值，否则"接口有 cwd、页面纹丝不动"。
