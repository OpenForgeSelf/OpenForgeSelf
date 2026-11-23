# PILOT-086 · 07-Final-Report

## 一、任务

用户输入17（2026-11-23）：「xxred对应本项目有pr关于存储机密的方案，你开工作树或者新分支去实现它」→ 以 xxred 分支 `docs/agent-secret-management-20261122`（tip `839dfc2`）的设计提案为真源，在新分支 `feat/credential-vault` 实现宿主级 Credential Vault（P1 核心 + P2 管理面 API + P3 授权执行与审计）。

## 二、完成内容

- **数据层**：`StoredSecret` / `CredentialGrant` / `SecretAuditEvent` 三实体（XCode，ConnName=ForgeSelf，惰性建表，+.Biz 半边声明 `Entity<T>` 基类）。
- **加密核心（P1）**：`SecretRef`（引用 JSON 生成/校验）、`VaultKeyProvider`（随机 256-bit KEK；Windows=DPAPI **LocalMachine**+熵，非 Windows=0600 文件）、`EnvelopeSecretCrypto`（AES-256-GCM 信封：随机 DEK + wrap/data 双 AAD 绑定）。
- **服务层（P3）**：`SecretStore`（CRUD/轮换/吊销/删除连带吊销授权/审计落行/`GetValueForHost` 唯一明文出口）、`CredentialBroker`（默认拒绝授权矩阵，撤销立即生效）。
- **管理面（P2）**：`SecretsController` 10 端点（`/api/secrets`，类级 ApiKeyPolicy，成功/业务失败/404/500 四种外壳，响应无明文无信封材料）+ AppBuilder 三行 DI 注册。
- **文档**：`docs/08-security/credential-vault-design.md`（设计真源）、`docs/06-research/008-agent-secret-management.md`（调研，renumber 003→008）、`docs/README.md` 索引行、`docs/02-features/100-secret-encryption.md` §6 回写。

## 三、Validation（验证结果 · 中档 · 均为实际执行）

| 项 | 结果 |
|---|---|
| 过滤集（Wave-1+2 五类测试） | ✅ **66/66 通过**（首跑 64/66，两红修复后复绿） |
| 全量 `dotnet test ForgeSelf.slnx`（中档） | ✅ 4284 总（46+40+19+48+174+3957）：**Api.Tests 3957/3924 通过/33 失败，33 红逐条对表归因完毕，归本批新增红 = 0**（26 基线既红=主仓同 filter 同红；5 worktree 环境缺件=OAR dist/插件产物/TODO.md 不入库；2 顺序干扰=隔离重跑 10/10 绿且主仓亦绿） |
| `verify-pilot-artifacts.ps1` | ✅ 本批目录全绿（首跑 07 缺 Validation 节名已补；余 2 FAIL 属 sems-workbench-1.3.0-merge 存量基线，非本批） |
| `check-commit-hygiene.ps1 -Staged` | ✅ 违规 0 |
| 明文红线机械断言 | ✅ 五类响应体无明文/无信封字段（AC-9） |

## 四、设计决策（决策→背景→理由→准则）

1. **DPAPI 用 LocalMachine 而非 CurrentUser** → 宿主以桌面 + Windows 服务（LocalSystem）双形态运行，CurrentUser 会导致服务态解不开桌面态加密的 KEK → 与 `IMachineKeyProvider` 排除用户 SID 同一教训 → 准则：跨进程边界的长效机密不得绑用户域。
2. **每凭据随机 DEK + KEK 信封（而非直接 KEK 加密）** → 换 KEK/轮换 KEK 时只需重包裹 DEK，无需重加密全部数据；AAD 绑定行字段防复制错位 → 准则：信封模式优先，密钥管理与数据加密解耦。
3. **审计行跨删除存活** → 删除凭据后审计链完整性优先，`secretId` 悬挂引用可接受 → 准则：审计是不可抵赖面，不随被审对象生命周期级联。
4. **不向插件 seed 保险库服务** → 插件用凭据属 P4 Adapter 面，必须经 Grant 授权通道 → 准则：默认拒绝，能力显式开口。

## 五、NOT-to-do（已同步 `docs/07-decisions/not-taken-decisions.md`）

- 不动既有 `ISecretEncryptionService`（CBC v1/v2）与 AI Provider 配置加密——并存不互替，零迁移；
- 不做 KEK 轮换、测试连接端点、Lease、UI 管理页、插件 Adapter（设计稿 §10 后续批次）；
- 不新增配置项（密钥文件路径固定 `data/keys/`，与数据目录策略一致）。

## 六、风险

- **Unknown（部署面）**：Windows 服务态 LocalSystem 与桌面态首次建 KEK 的文件 ACL 归属（ProgramData 写权限差异）——单机部署实证为主，未做服务态实测；风险低（DPAPI LocalMachine 跨会话可解，文件只受 ACL 影响）；
- 审计写失败降级为日志告警（不阻断主流程）——极端情况下审计可能缺行，为可用性取舍，设计稿 §6 已声明。

## 七、状态与待用户事项

- **状态：代码与文档完成，中档全量验证通过（归本批新增红=0）**；
- **未 commit**（闸门3 需明示批准）；分支 `feat/credential-vault`（基于 86dfbf4），工作区：新增 ~30 文件（源码 15 + 测试 5 + 文档 6 + 工件 8）+ AppBuilder/csproj/docs 修改；
- **待你批准**：① 提交本批（一次 commit，pre-commit 工件门禁已预验）② 是否推送 github-origin / 开 PR（PR 目标主仓，另需 xxred PR 联动说明）。

## 八、代价与收益

- 代价：新增 ~2800 行（源码 + 测试）；机器时 ≈ 过滤集 2s×3 轮 + 全量 ~15 分钟；
- 收益：宿主获得「默认拒绝 + 授权显式开口 + 审计不可抵赖 + 明文不出宿主」的凭据底座，为 Agent/自动化真实使用第三方凭据（后续 Adapter 批次）铺路；xxred 方案的核心主张（引用而非明文分发、Grant 治理、审计）全部落地。
