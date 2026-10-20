# Review

> 阶段：Stage 8（Reviewer 视角回裁 Intent → Spec → Plan → Task → Code → Test → Evidence 全链）。
> 结论只裁事实。CHANGES_REQUIRED 时回指倒退到哪个阶段。
> 独立评审：CodeReview 子代理（current_session diff，剔除上批 v2.5.2 混存改动说明）+ 本会话自审，双重过。

## 八问（逐条回答）

1. **实现是否满足 Intent？** 是。「万能工具 list_tools 把 mcp. 开头的也带上，内外都要能用」→ ①list_tools 枚举出口合并已连接外部工具（全名 + source=mcp-external + schema）；②网关与内置 AIAgent 两侧均可直调 `mcp.<id>.<工具名>`（AIAgent 不再落 unknown tool）。
2. **实现是否满足 Spec？** 是。AC-1~AC-7 逐条 Verified（05-evidence「AC 逐项判定」）；BR/BC/FM 有对应单测（契约缺失降级、BC-4 合并段异常不崩、FM-3 禁缓存）。
3. **是否超出 Scope？** 未超。改动文件集 = 03-plan A~F 清单 + 测试；e2e 存量 `.port-input` strict 修复属本批首跑全文件暴露的测试侧一行修（非业务代码）；全量测试存量红仅登记 TODO 未顺手修。
4. **是否修改了不应修改的文件？** 否。宿主内核（Context/Fiber/PluginManager）零改动；外部工具未注册进 IToolRegistry（D10 注册表隔离保持，合并只发生在枚举出口）。
5. **测试是否覆盖 Acceptance Criteria？** 是。四组新测试（契约 46/46、域内 200/200）+ e2e 1 passed + dev-stack 真连 deepwiki 实测（枚举 3 条 + 直调真实回答）。
6. **是否存在隐性回归风险？** 低。契约纯增量；`ExternalPrefix`/`FullName` 改引常量真源（值不变 `mcp.`）；ListTools 无外部连接时输出与 v2.5.x 逐字段等价（Execute_GatewayMissing 用例锚定）；既有 200 域内用例全绿。
7. **是否与架构一致？** 是。architecture-design 铁律 2（契约入 Abstractions）/4（运行期 ctx.Get 禁缓存）+ 调研 §5.6 裁决 A/F（eager 提供、提供即 effect、Fiber 摘除）；判例同 IWorkflowAIAdvisor。CodeReview 独立确认「跨插件类型一致性（ForgeSelf.*.dll 回落默认 ALC）成立，无悬空崩溃路径」。
8. **Evidence 是否足以证明任务完成？** 是（Verified 级：命令输出 + e2e 证据日志 + 现场走查实测记录 + e2e 截图读图 v2.6.0 徽标）。

## Requirement Check

PASS

## Scope Check

PASS

## Test Check

PASS

## Architecture Check

PASS

## Risk

L2（新增跨插件契约 + 两插件消费面改动；无内核/数据契约变更；回滚 = git 还原本批文件回 2.5.2/1.8.0，契约纯增量回滚不影响超时批）

## Findings

### Critical

无。

### Major（CodeReview 提出 1 条，已当场修复并复验）

1. **`ListConnectedTools()` 违背契约「零 IO」声明**：原实现经 `GetStates()` → 每次 `store.Load()` 读盘 + 全量配置反序列化 + 密钥脱敏（只为拿 Connected/Id）。修复 = `McpClientManager` 新增 `ConnectedSessions()`（`_sessions` 字典天然只含已连会话），适配器改遍历会话缓存。复验：Adapter 真连用例 + 域内 200/200 复跑绿。

### Minor（CodeReview 提出 2 条，均已修）

1. **卸载窗口「会话已停、契约尚在」**：`ctx.Register` 原在停止器 Effect 之前，Fiber 逆序回滚先停会话后摘网关。修复 = 注册移至停止器之后（网关先摘、消费方 Get 得 null 即干净降级），顺序理由已写入代码注释。
2. **契约文档 exception 列「目标工具不在清单」与实际不符**（实际透传远端、以错误负载原样返回不抛）：文档订正并注明语义。

### 走查期发现并已修（存量，非本批引入）

- e2e 渲染用例 `.port-input` strict 双元素冲突（v2.5.2「执行超时」行复用样式类，本批首跑全文件才暴露）→ 测试定位器 `.first()` 修复，复跑 1 passed。

## 实施偏差复核

03-plan「实施偏差记录」4 条均成立且优于原案（运行期 Get 更贴铁律 4；命名真源上移是 ALC 隔离下的必然选择；测试落点/ e2e mock 传输为仓内既有设施适配）——闸门1 批准范围内，无越权设计变更。

## Final Decision

**APPROVED**（门禁①②③ + CodeReview Major/Minor 闭环均有证据；④发布按硬闸门待用户授权，与 v2.5.2 批合并为 2.6.0 一次发布；⑤运行实例复验在用户启用新版本后执行）。
