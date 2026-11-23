# 05-Evidence — Credential Vault 核心（PILOT-086）

> 验证时间：2026-11-23 ｜ 执行：lead（DSH worktree `feat/credential-vault`）
> 门禁档位：**中档**（触 `ForgeSelf.Api/**` 宿主源码 → 后端全量 `dotnet test`，AGENTS §5.6）

## 1. 验证矩阵（Verified = 亲自跑过拿到真实输出）

| # | 验证项 | 命令 | 判据 | 结果 | 等级 |
|---|--------|------|------|------|------|
| V1 | Wave-1 单测（SecretRef/KEK/信封） | `dotnet test --filter …SecretRefTests\|VaultKeyProviderTests\|EnvelopeSecretCryptoTests` | 全绿 | **33/33 通过** | Verified |
| V2 | Wave-2 单测（Store/Broker/Audit/Controller） | `dotnet test --filter …CredentialVaultStoreTests\|SecretsControllerTests`（与 V1 合跑） | 全绿 | **66/66 通过**（合计 33+33，合并运行 66 个用例全绿） | Verified |
| V3 | 全量后端 `dotnet test ForgeSelf.slnx` | 见 §2 | 归本批新增红 = 0 | 见 §2 | Verified |
| V4 | 工件链门禁 | `pwsh scripts/verify-pilot-artifacts.ps1` | PASS | 见 §2 | Verified |
| V5 | 提交卫生守卫 | `pwsh scripts/check-commit-hygiene.ps1 -Staged` | 违规 0 | 见 §2 | Verified |
| V6 | 明文红线（AC-9）机械断言 | SecretsControllerTests 创建/列表/详情/轮换/审计五类响应体序列化后逐串断言无明文、无 wrappedDek/ciphertext/nonce/tag/keyId 字段 | 全部不含 | 通过（V2 内含） | Verified |

## 2. 结果记录

- V2（2026-11-23 12:33 首跑）：64/66 → 两红已修：
  - `轮换_新DEK新Nonce…`：XCode 实体缓存返回同一实例，`SetSecretValue` 原地改写导致测试前置引用被连带改值 → 测试改为轮换前捕获标量快照（实现无缺陷，缓存语义本身正确）；
  - `轮换_目标不存在_404`：`SetSecretValue` 对不存在目标抛 `secret_not_found`，控制器按业务失败回 200 → 控制器为 Patch/SetValue/CreateGrant/RevokeGrant 四处补 `secret_not_found → 404` 映射（与设计 AC-9 一致）。
  - 复跑：**失败: 0，通过: 66，总计: 66（2 秒）**。
- V3（2026-11-23 12:29–12:46，中档全量 `dotnet test ForgeSelf.slnx`）：
  - 各项目：Abstractions 46/46 ✅ · Core 40/40 ✅ · Tray 19/19 ✅ · SmartTerminal 48/48 ✅ · DbAdmin 174/174 ✅（2m51s）· **Api.Tests 3957 总 / 3924 通过 / 33 失败**（15m45s）。
  - **33 红归因矩阵（逐条对表，判责归零）**：
    | 类别 | 明细 | 证据 |
    |------|------|------|
    | 主仓同红（基线既红，代码同源，26 条） | ProjectWorkspaceDefaultRootTests×15、WorkflowPlanningIntegrationTests×6、Pc034_DiConstructedPluginServices×1、RootDevCommand_MustUseFullStackLauncher×1、ProcessOutputEncodingGuard×1、JsonNonAsciiEscapingGuard×1、RunOrchestratorTests.Resume×1 | 主仓（无本批改动）同 filter 复跑同红：`.temp/main-baseline-check.log`（3/26）、`.temp/main-baseline-check2.log`（22 FAIL 名单与 worktree 完全一致） |
    | worktree 环境缺件（5 条） | OarTransport/OarWorkerClient×3（`RuntimeWorker/dist` 未构建，gitignored 产物）、ScriptRunnerDi×1（插件路由 404，worktree 插件产物缺）、ProblemClassRegistryTests×1（读根目录 TODO.md——不入库文件，worktree 无） | 失败消息原文：`$XunitDynamicSkip$Worker 未构建` / 404 / `FileNotFoundException …\TODO.md`；主仓同 filter 该三项绿 |
    | 全量态顺序干扰（2 条） | MafApprovalLoopTests×1、DesignAgentToolContractTests×1 | 隔离重跑 **10/10 绿**（`.temp/isolated-two.log`）；主仓同 filter 亦绿；与 86dfbf4 后主仓两笔提交（242c5fd/e5358ab）均未触碰两区域（`git log 86dfbf4..HEAD -- <区域>` 为空） |
  - **结论：归本批新增红 = 0**（我的 66 条新增用例全绿；33 条既有红全部对表归因于基线/环境/干扰，判责不落本批）。
- V4（verify-pilot-artifacts.ps1，12:41）：全仓扫描首跑 3 FAIL，其中本批 1 项（07 缺 Validation/Review 节名）已补齐复绿；**余 2 FAIL 属 2026-11-15-sems-workbench-1.3.0-merge 存量基线**（05 缺 Verified/06 缺 Final Decision，非本批范围，已记 TODO 备忘不混车）。
- V5（check-commit-hygiene.ps1 -Staged）：提交获批后随 staging 一并跑（见 §2 尾注）。

## 2.1 V5 补录

- 暂存后执行 `pwsh scripts/check-commit-hygiene.ps1 -Staged`：**违规 0（PASS）**（2026-11-23 12:57 实跑，输出见 `.temp/hygiene-staged.log`）。


## 3. AC 覆盖对表（ACn → 证据落点）

| AC | 证据（Verified） |
|----|------|
| AC1 | 信封往返：`EnvelopeSecretCryptoTests` 往返用例（V1/V2 集合内全绿） |
| AC2 | 篡改拒绝（Ciphertext/Nonce/Tag/WrappedDek/wrap AAD 各维）：`EnvelopeSecretCryptoTests` 对应用例全绿 |
| AC3 | 同明文两次加密 Nonce/Ciphertext 均不同：`EnvelopeSecretCryptoTests` 随机性用例 |
| AC4 | 错误 KEK 解密失败：`EnvelopeSecretCryptoTests` 错钥用例 |
| AC5 | KEK 稳定/隔离/落点：`VaultKeyProviderTests`（同目录两实例同 KeyId、异目录异钥、`vault-kek.v1.bin` 落点） |
| AC6 | Windows 文件字节 ≠ KEK 明文（DPAPI）：`VaultKeyProviderTests` 保护形态用例 |
| AC7 | Grant 矩阵七拒绝一允许：`CredentialVaultStoreTests` 授权矩阵段（V2 集合内） |
| AC8 | revoked/expired → GetValueForHost 拒绝 + 审计 denied：`CredentialVaultStoreTests` 状态拒绝段（deny reasonCodes secret_revoked/secret_expired） |
| AC9 | 明文红线机械断言：`SecretsControllerTests` 五类响应体序列化串不含 value/密文字段（V6，通过） |
| AC10 | 审计落行与字段白名单：`CredentialVaultStoreTests` 审计段（create/update_value/use_allowed/use_denied/grant_* 全有事件、无明文） |
| AC11 | 删除连带：Grant 全清、行消失、审计仍可查（reasonCode=secret_deleted）：`CredentialVaultStoreTests` 删除级联段 |
| AC12 | 类级 ApiKeyPolicy 反射断言：`SecretsControllerTests` 鉴权特性用例 |
| AC13 | `dotnet build` 绿 + 全量无归本批新增红：V3（4284 总/33 红逐条对表归零，§2） |
| AC14 | 改动面 = 03-plan 清单：暂存 35 文件均为 03-plan 所列（AppBuilder/csproj/docs 为计划内修改，无越界 diff；`git status --porcelain` 对表） |

## 4. 证据归档

- 过滤集日志：`.temp/vault-test.log`、`.temp/vault-test2.log`（worktree 内临时，不入库）
- 全量日志：`.temp/full-test.log`
- 关键断言摘录（审计后补入本文件的代码块）
