# Review — B4 权限可配置化

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 执行者自审（闸门2 交 lead 复核）；结论只报事实。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？** 满足。四项交付全落：①PermissionProfile 实体 + 默认策略种子=零配置行为不变（registry 层沉默 Allow + MAF 包装不变，AC3 实证）②ToolRegistry pre-execute 闸门消费策略（AppBuilder 挂监听器，查配置→决策→回落默认）③BrowserUse 域名白名单上收（宿主层判定 + 插件 BrowserGuard 保留纵深防御，零平行 PermissionBroker）④审计查询端点（决策日志 + ToolApproval 联合）。
2. **实现是否符合 Spec？** 符合。§4 决策语义 11 个分支全实现（含 fail-safe/兜底*/禁用跳过/Agent 优先）；§3 契约除一处签名微调（HasRuleForToolAsync agentId→sessionId，语义等价，04-task 偏差记录 5）。
3. **是否超出了 Scope？** 未超出。写面收敛：ToolRegistry.cs 零改动（闸门已内建，监听器挂 AppBuilder）；BrowserUse 仅注释；新增触碰 3 处（ReactLoopAgent 注入面 / BrowserUseConfig 注释 / MafApprovalLoopTests 用例）均为最小实现必需，已在 04-task 偏差记录。
4. **是否修改了不应该修改的文件？** 否。Forbidden 全守：宿主 Web/插件 web 零触碰；AgentHub PermissionBroker 零触碰；:51888/:7301/:7399 进程零触碰（孤儿 testhost 119960 为本任务自身后台遗留，清理不涉 dev-stack）。
5. **测试是否覆盖 Acceptance Criteria？** 覆盖。AC1..AC9 逐字映射（05-evidence §1）；新用例 23 条（服务 21 + MAF 层 2）+ 真管线（ToolRegistry.ExecuteAsync+EventBus）+ 真库（XCode SQLite）；AC8 正反两面（让位 + 零配置不变）。
6. **是否存在明显回归风险？** 低。零配置路径仅增两条静默观察（决策日志 + HasRule 查询），行为分支无变化；监听器异常 fail-open 不阻断管线；既有 MAF/审批/registry 测试全量绿。风险点：决策日志量级（见 Findings Minor-1）。
7. **是否存在架构不一致？** 无平行面。策略消费走唯一 pre-execute 闸门；插件本地校验保留作纵深防御（铁律 2 合规）；会话→Agent 记忆复用服务内单例字典（与 PersistentApprovalService 同型）；宿主契约经 Cordis 懒解析（同 B2 RunVerify 模式）。
8. **Evidence 是否足以证明任务完成？** 足以。Verified 级证据 7 项（05-evidence §0），全量对表 qa-baseline 零新增红 + 44 条伪红隔离复跑实证环境漂移。AC7 路由层（HTTP 真请求）为控制器级替代，已在 05 标注覆盖口径。

## Requirement Check

PASS（AC1..AC9 全 ✅，见 05-evidence §1/§3）

## Scope Check

PASS（偏差 5 条记录于 04-task，无 Forbidden 触碰）

## Test Check

PASS（新 23 用例全绿；全量 3435 对表零新增红；隔离复跑定性闭合）

## Architecture Check

PASS（唯一闸门消费 + 纵深防御保留 + Cordis 注入面同型）

## Risk

L2（宿主源码触碰，由全量门禁 + 零配置红线测试覆盖兜住）

## Findings

### Critical

（无）

### Major

（无）

### Minor

1. **决策日志量级**：default-policy Allow 逐调用落行，高频会话下 PermissionDecisionLog 增长快——V1 接受，保留策略列入 07 悬置（TODO 无需另立，07 §Risk 已承载）。
2. **QueryAudit 全量拉取合并**：策略决策侧 pageSize 上限 10000 + 内存归并排序——审计量上万后需下推 SQL（V1 不做，07 悬置）。
3. **HasRuleForToolAsync 每工具回合查询 2 次 XCode**（MAF 包装判断）——Meta.Cache 命中时开销小；量大后可加 profile 级内存缓存（07 悬置）。

## Final Decision

APPROVED

<!-- 自审通过，闸门2 交 lead 复核入库。AC7 路由层为控制器级口径（偏差记录 2），lead 若要求真 HTTP 层可后续补 Integration 测试。 -->
