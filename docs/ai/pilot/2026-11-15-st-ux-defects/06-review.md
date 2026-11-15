# 06 Review — 智能终端三缺陷实测修复（2026-11-15）

## 八问

1. **需求覆盖？** 用户三项全部落地：A 默认 `~` 可创建会话（StStartDir 展开 + 脱敏）；B `ls` 无乱码（裁决器怀疑门）；C `cd` 后页面目录更新（两行采集 + 响应 cwd + 前端回写 + 轮询 prompt 消费）；「没有对应测试要补」→ 三缺陷各配红灯锚测试（Core 1+1、插件 6），全部保留在套件内。
2. **变更最小？** 9 个业务文件 + 2 个测试文件 + 3 类归档文档；cmd 旧包裹行未动（phase1 兼容），PowerShell/POSIX 分支零改动；前端两处 `if (res.cwd)` 回写 + 轮询一行 `s.prompt = pack.prompt`。live 实测抓到第四层（轮询不消费 prompt）属同根因收口，未扩范围。
3. **测试真实？** 红→绿有日志（05 §1）；cmd 集成测试用真实 cmd.exe 进程（非 mock）；编码测试字节由 WideCharToMultiByte 现场生成（不依赖环境码页）；对照护栏防怀疑门过宽翻案；live spec 真实例 :51888（非 fixture）。
4. **回归风险？** 怀疑门只收窄到「可疑拉丁扩展 + 全文无 CJK + ANSI 往返成功且含 CJK」窄门，UTF-8 主路径（dotnet/pnpm/node 输出）不触；已知原理性局限（纯拉丁重音 UTF-8 在 CJK 码页偏 ANSI）已在 PC-011 与代码注释留档；两行协议只在 cmd kind 激活，其余 shell 走原路径；轮询 prompt 消费对旧服务端（无 prompt 字段）无操作。
5. **契约变更？** `SubmitCommandResponse` 新增可选 `cwd`（加字段，向后兼容）；插件文档 03-架构与接口.md 已同步（2.0.2 节）；前端类型同步。
6. **泄露面闭合？** A 的报错对相对路径只回显输入原文（`Resolve_NonRootedMissingPath_MessageDoesNotLeakHostProcessCwd` 永久红灯锚）；V1 同类同修（同一 partial class 两处入口均走 StStartDir）。
7. **范围外残留？** cmd exit 码恒 0（`!ERRORLEVEL!` 无延迟展开）为独立既有缺陷，已在 PC-036 §6 留档边界，另行挂账（见 TODO）；未做 ANSI 转义序列清洗（无实测案例，YAGNI）；不动 PTY 验收线。
8. **流程合规？** 闸门1 = 用户直接指令（输入22）+ mini-task.md 落盘；六步闭环：①门禁 ②冒烟 e2e ③起源码验收（本批无 UI 结构变化，走查并入 ⑤ 运行实例复验 + e2e 视觉，改动面仅逻辑字段回写）④sidecar 2.0.2（dist 同版本热更一次，C 第四层）⑤:51888 live 复验（05 §6 完成，1 passed）⑥插件文档已更。**交付边界改判**：B 随宿主更新发布（sidecar 不可携带宿主侧库修复，证据 case + packaging §1.8），live 断言按宿主版本门控。

## 问题类归档

- PC-011 逃逸记录更新（第 3 案，首次真逃逸入运行时：启发式「合法即采信」语义盲区 + 测试基建"不可构造"假设双重成因）；守卫强化 = 撞车形态专用测试（G1）+ 对照护栏，机器校验双绿（check-problem-classes OK 36 PC、Registry 5/5）。
- 新建 PC-035（`~` 占位符 + 报错泄露宿主路径；2026-11-13 BUG#1 延期债到期收口，G1 五例）。
- 新建 PC-036（cmd cwd 采集三层缺失；G1 真实 cmd 进程集成测试；lens 三问覆盖未来 shell kind 扩展）。
- 共享 case：`docs/11-troubleshooting/cases/2026-11-15-st-ux-defects.md`（一案三类，cases/README.md 已登记）。

## 重构提案

- `StStartDir.Resolve` 已是单一真源；后续若新增 agent/插件会话入口，lens 第 1 问把关，必要时升级 G2 枚举（当前入口仅两处，枚举收益低）。
- `SessionShell.Parse` 的 cmd/PS/POSIX 三分支已收敛于统一 after 段协议；若再加 shell kind，建议先抽「标记协议适配器」再扩 kind（提案，不在本批做）。

## 悬空教训

1. **测试基建的"不可构造"假设必须过逃逸审查**——修复盲区先修测试宇宙的视野（本批已沉淀为共享 case 教训 §教训 1）。
2. **交互式 cmd 的 %VAR% 解析期展开是会话层协议硬约束**——`call`/`%%` 转义交互态不可用（实测），两行协议是唯一形态；已入 PC-036 §2 供未来 shell 扩展查阅。
3. cmd exit 码恒 0 的独立缺陷已挂 TODO（见 §八问 7），下批同类会话层任务优先认领。
4. **修复落位先问「改动在哪个程序集」**：宿主侧库修复不随插件 sidecar 走（IContext 共享契约类型身份不可劈裂）；跨程序集修复必须声明交付边界，live 断言按宿主版本门控（已回写 packaging 真源 §1.8 + case §交付形态实测裁决）。
5. **live 失败轮次的探针会话必须清理**：本轮多轮 live 调试在用户实例上累积 5 个泄漏会话，已 API close 清零；live spec 需自带幂等清理（已内置）。

## Final Decision

**APPROVED**（05 §6 live 复验已完成：A/C 实测通过、B 门控待宿主更新；提交等用户验收，闸门3）。
