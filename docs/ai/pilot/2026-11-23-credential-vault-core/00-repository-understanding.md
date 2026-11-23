# 00 Repository Understanding — Credential Vault P1–P3 核心（PILOT-086）

> 阶段：Stage 0 · Repository Understanding｜任务日：2026-11-23｜Task ID：PILOT-086
> 任务来源：用户输入17（2026-11-23）「xxred对应本项目有pr关于存储机密的方案，你开工作树或者新分支去实现它」。
> 方案真源 = xxred fork 分支 `docs/agent-secret-management-20261122`（839dfc2，fork 内 PR pull/1/head）：
> `docs/08-security/credential-vault-design.md`（设计提案）+ `docs/06-research/003-agent-secret-management.md`（调研）。
> 两份文档已由本批带入工作树（staged，随本批入库）；工作树 = `.agents/worktrees/dsh-4556e8e8-aa45`，分支 `feat/credential-vault`（自 86dfbf4 切出）。

## 项目结构

- 解决方案 `ForgeSelf.slnx`：`ForgeSelf.Abstractions`（宿主/插件共享契约）→ `ForgeSelf.Host`（安全实现等低层）→ `ForgeSelf.Api`（ASP.NET Core 宿主：Controllers/Services/Entities）→ `ForgeSelf.Core`（IContext/EventBus 内核）；插件 `Plugins/<X>` 独立程序集；测试 `ForgeSelf.Api.Tests`（xUnit + FluentAssertions + Moq）。
- 本批只涉宿主侧（Abstractions / Host / Api / Api.Tests），不涉前端与插件。

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 契约 | net10.0 类库 | `ForgeSelf.Abstractions/ForgeSelf.Abstractions.csproj` |
| 宿主低层 | net10.0，仅引用 Abstractions | `ForgeSelf.Host/ForgeSelf.Host.csproj`（安全实现 `Security/AesSecretEncryptionService.cs`、`Security/MachineKeyProvider.cs`） |
| 宿主 API | .NET 10 + ASP.NET Core + NewLife.XCode 12.0 + SQLite（XCode.SQLite 11.24） | `ForgeSelf.Api/ForgeSelf.Api.csproj` |
| 数据 | XCode 实体（手维护生成态代码，`[BindTable]` + ConnName=`ForgeSelf`，表自动建） | `ForgeSelf.Api/Entities/ApiKeyCredential.cs` |
| 测试 | xUnit + FluentAssertions；XCode 用 `XCodeTestFixture`（进程级单次绑定临时 SQLite） | `ForgeSelf.Api.Tests/XCodeTestFixture.cs`、`Unit/ApiKeysControllerTests.cs` |

## 架构特点（与本批直接相关的事实，均实测核对）

1. **既有密钥加密**：`ISecretEncryptionService`（`ForgeSelf.Abstractions/ISecretEncryptionService.cs:12`）= AES-256-CBC 密文版本化（v2 写 / v1 读兼容），实现 `ForgeSelf.Host/Security/AesSecretEncryptionService.cs:26`；主密钥来源 配置 > 环境变量 > 机器派生（`MachineKeyProvider`，PBKDF2 + MachineGuid，`ForgeSelf.Host/Security/MachineKeyProvider.cs:18`）。**本批不动它**（设计提案 §5「现有加密兼容」）。
2. **DI 注册点**：`ForgeSelf.Api/AppBuilder.cs:239-251`（IMachineKeyProvider/ISecretEncryptionService/ApiKeyService 等）；数据根 = `dataLocation.GetHostDataDirectory()`（`AppBuilder.cs:89`）。
3. **鉴权**：管理面控制器**类级** `[Authorize("ApiKeyPolicy")]`（`AppBuilder.cs:320-330` 策略注册；`Controllers/ApiKeysController.cs:20-22` 先例），响应外壳 `{ success:true, data }` / `{ success:false, error }`。
4. **实体模式**：生成态 partial + `_`/`__` 字段快捷类 + 静态 Find（`ApiKeyCredential.cs` 全文）；表随 `EntityFactory.InitConnection` 自动建，无手写迁移。
5. **明文红线先例**：ApiKeyCredential「实体绝不直接作为 HTTP 响应返回，必须 DTO 投影」（`ApiKeyCredential.cs:20`）；明文只在创建/轮换当次出现。

## 测试方式

- 后端单测：`ForgeSelf.Api.Tests`，实体相关测试挂 `[Collection("XCode")]` + `IClassFixture<XCodeTestFixture>`（临时 SQLite，`ForgeSelf` 连接名在夹具 `_connNames` 内 → 新表自动建）。
- 控制器测试：直接 `new Controller(service)` 调用（禁 WebApplicationFactory 覆盖连接串，见 `ApiKeyService.cs:16-18` remarks）。

## 构建命令

```bash
dotnet build ForgeSelf.slnx          # 或 cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test
```

## 主要目录职责（本批涉及）

| 目录 | 职责 |
| --- | --- |
| `ForgeSelf.Abstractions/` | 共享契约（本批新增 SecretRef 引用类型） |
| `ForgeSelf.Host/Security/` | 安全实现（本批新增 KEK 提供器 + AES-GCM 信封加密） |
| `ForgeSelf.Api/Entities/` | XCode 实体（本批新增 StoredSecret / CredentialGrant / SecretAuditEvent） |
| `ForgeSelf.Api/Services/` | 服务（本批新增 `Vault/` 子目录） |
| `ForgeSelf.Api/Controllers/` | 管理面 API（本批新增 SecretsController） |
| `ForgeSelf.Api.Tests/Unit/` | 单测（本批新增 5 件） |

## 代码组织方式

宿主安全实现放 `ForgeSelf.Host`（命名空间仍 `ForgeSelf.Api.Security`，先例同 MachineKeyProvider）；跨边界契约（SecretRef）放 `ForgeSelf.Abstractions`；实体/服务/控制器按既有分层落 `ForgeSelf.Api`。

## 现有工程规范（本批约束源）

- `docs/04-standards/ai-native-engineering-workflow.md`（v1.2.0，九阶段 + 三闸门）。
- AGENTS.md §5.6 门禁分档：本批碰 `ForgeSelf.Api/**` → **中档**（全量 `dotnet test`）。
- 管理面鉴权铁律 17：新控制器类级 `[Authorize("ApiKeyPolicy")]`。
- 提交内容卫生（AGENTS §2.3）：文档/注释不得含绝对地址、个人标识；xxred 两份文档入库前已逐行核对（无盘符路径、无个人称呼，引用均为公开 URL）。
- 架构技能 `architecture-design`：契约入 Abstractions；插件消费一律 `ctx.Get<T>()`；**本批不向插件 seed Vault 服务**（插件用凭据属 P4 Adapter 批）。

## 适用问题类

无（新功能批，非缺陷/回归）。预防性 lens 两条已在 Spec「Failure Modes」列明：PC-002（宿主控制器鉴权契约）、PC-034（插件侧 DI 宿主契约解析）。

## 候选低风险任务与选择原因

用户指令即任务：落地 xxred 凭据保险库方案。方案自身建议「先完成 P0–P3」（设计 §12），本批 = **P1 全部 + P2 的 API 面 + P3 的授权校验**（无 UI、无 Lease、无 Adapter），全部为**新增文件**，不改既有表结构与既有加密体系 → 风险集中于新增代码自身，可回滚（整批新增，git revert 即净退）。
