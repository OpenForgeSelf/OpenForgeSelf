# Review — B2 结果硬化

## 八问

**1. 需求是否全部实现？**
B2①②③ 三件事全部落地：① filesChanged 两段断链修复（结构化提取器 + 留痕参数面对齐 + 透传），TDD 先红后绿；② Run 级 Verify 闸门（ExitCriteria 落列 → 三路收束共用 FinalizeRunAsync → 确定性校验 → 未过置 Stuck，开放任务回合级 run/verify 同语义）；③ 结果收据（计划任务 AgentRun.ResultReceipt write-once 冻结 / 开放任务 run/receipt 事件 append-only，RunRecordPanel.vue 收据区块）。逐条 AC 见 05-evidence §4。

**2. 是否有未验证的声明？**
有，三处，均已显式标注：AC-UI 真实宿主走查（待 dev-stack 重启）；cancel 冻结无专测（代码级复用同一 write-once 路径）；AgentHub 委派回写 filesChanged 语义未覆盖。全部进 07 风险与遗留。

**3. 测试覆盖是否充分（快档实跑）？**
新增 17 例（B2① 2 + RunVerifyGate 10 + RunReceipt 5），连同 15 过滤子全量 **121/121 绿**，对照 qa 基线 89 红零新增。覆盖四类形态：真库落库回读（XCode collection）、编排器收束闸门、开放任务会话日志断言、纯单元确定性断言。

**4. 是否偏离 Spec？有否记录？**
一处实质偏离：FR-2.1 ExitCriteria 从「随 PlanJson 持久化」改为 **AgentRun.ExitCriteria 独立列**。理由：PlanJson 是计划面缓存（可被规则回退重建），判据是运行面事实；分列后 resume/审计不依赖 Plan 反序列化成功。已在 02-spec 追加修订记录并在 07 决策登记。其余 FR/BR 逐条对齐（BR-1 向后兼容、BR-2 fail-safe、BR-3 现状不变、BR-4 只投影不执行、BR-5 三步走、BR-6 复用 IProjectWorkspaceService.ResolveSafePath 越界防线）。

**5. 是否触碰禁区？**
未触碰：ToolRegistry.cs（B4）、ForgeSelf.Web/src/（B3）、features.ts（B3）、:51888 实例、dev-stack 起停（全程 lead 调度）。越出自身写面的一处：TasksOverviewController.cs:295 一行编译修复（taskcenter-dev 文件，构建窗口内为达成 lead「build 到绿」指令，已第一时间报备并请 lead 知会对方）。

**6. 代码质量与项目规范符合度？**
XCode 面全部走 Model.xml + xcode（.Biz.cs 零 diff）；SessionEvent 新类型三步走（record + Map + 投影 case）且不入模型历史；UI 全部 `--el-*` 变量 + color-mix，无裸色值；camelCase JSON + 枚举出字符串（前端可读）；留痕归一走 FileChangeList.ToStorage 单点；取证归一走 ToolCallFileExtractor 单点。异常面：验证器 catch-all fail-safe（校验异常=不通过），AppendRunEvent 软降级 XTrace。

**7. 性能/运行经济学？**
Verify 阶段零 LLM、零命令执行（纯内存断言 + 文件存在性检查）；收据聚合 = 单次 FindAllByRunId + 内存归并；提取器失败路径有降级不抛（取证失败不打断执行流）。无新增每请求开销（未配置 ExitCriteria 的 Run 只多一次列写入）。

**8. 回归风险评估？**
两处签名变更的兼容面已核实：AppendSystemAsync 加尾参（可选）→ 既有 19 调用点零改动；AgentRunDto/StepRunDto 增列（camelCase 新字段）→ 前端按可选字段解析，旧数据字段缺席 = 区块不渲染。MafFixture 增 create_file 定义 → 既有 8 例 MAF 测试全绿（脚本化模型不读 schema）。

## Final Decision

**APPROVED（有条件）** —— 代码闭环、后端/插件前端/守卫门禁全绿、问题类闭环齐备；条件项：AC-UI 走查待 dev-stack 重启后补（不阻塞代码收口，阻塞「任务完成」宣告——发布前必须补）。
