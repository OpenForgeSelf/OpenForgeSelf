---
task_id: 2026-11-09-json-facade-unify
feature: none（JSON 门面统一 v2：全仓序列化/反序列化/解析调用收敛至 ForgeSelf.Abstractions.ForgeJson，守卫 v2 拦生产代码直调；用户输入9）
risk: { level: L2, triggers: ["155 个生产文件机械改写（Plugins 119 / ForgeSelf.Api 36）", "工作区叠加批1+并行批未提交项（194 处），回滚须逐文件", "对外协议输出必须字节级等价"], raised_by_agent: false, notes: "来源=用户输入9 直接指令；只收敛入口不改语义；18 处自定义 options 逐点搬移；全量测试兜底；无 DB/鉴权/生产环境触碰" }
gate1: { mode: user, at: null, ref: "待拍板：① JsonDocument.Parse 198 处是否入批（默认入批）② 未提交工作区上叠加还是先提交批1（默认先提交批1）" }
gate2: { mode: reviewer, at: null }
gate3: { mode: user, at: null }
expected_files: [ForgeSelf.Abstractions/ForgeJson.cs, ForgeSelf.Api/AppBuilder.cs, ForgeSelf.Api/**/*.cs(~36), ForgeSelf.Api.Tests/Architecture/JsonNonAsciiEscapingGuardTests.cs, Plugins/**/**/*.cs(~119, 除 Tests/web), docs/ai/pilot/2026-11-09-json-facade-unify/**]
rollback: 本批不提交 git（闸门3 等用户）；回滚 = 按 05-evidence 文件清单逐文件 `git checkout -- <file>`（⚠️ 工作区有批1+并行批未提交项，禁整树 checkout/clean）；ForgeJson.cs 为 untracked 新文件可单文件还原为批1 38 行 v1
---

# 04 Task — 2026-11-09-json-facade-unify

## Task ID
PILOT-2026-11-09-json-facade-unify（PC-027 机制延伸，非缺陷任务）

## Objective
生产代码（除 ForgeJson.cs）零 STJ 直调（Serialize/Deserialize/Parse/options/Writer），守卫 v2 先红后绿，全量后端测试零新增红，行为字节级等价。

## Scope
### Allowed
- `ForgeSelf.Abstractions/ForgeJson.cs`（v2 扩展）
- `ForgeSelf.Api/**/*.cs`（仅序列化/反序列化/解析调用点 + AppBuilder Encoder 引用）
- `Plugins/**/**/*.cs`（仅同类调用点；DbAdmin/AIAgent/McpCenter 等与并行批同文件——只碰 JSON 调用行，改前重读）
- `ForgeSelf.Api.Tests/Architecture/JsonNonAsciiEscapingGuardTests.cs`（守卫 v2 + G1 单测）
- `docs/ai/pilot/2026-11-09-json-facade-unify/**`、`TODO.md`、当天日记

### Forbidden
- `ForgeSelf.Web/**`、`e2e/**`、plugin 版本号、`Data/Model.xml`、`Data/Entities/*` 生成件、任何存量 Tests、`openwiki/**`、`docs/**`（本任务工件除外）
- 任何序列化语义变更（命名策略/缩进/忽略条件/注释容忍现状不变；inline 配置项必须逐点搬移）
- `JsonNode.Parse` 收敛（本批边界外）
- git 提交/推送、动 :51888 运行实例、动 wt-db/wt-qa/wt-st worktree、整树 `git checkout`/`git clean`

## Acceptance Criteria
- [ ] AC1 G1 单测绿（CreateOptions 强制 Encoder / Deserialize 等价性 / Parse 包装 / WriterOptions / Serialize null-options ≡ Default）
- [ ] AC2 守卫 v2 先红后绿（RED ~800 处明细 → GREEN 0 + 阳性对照 >600 处/ >150 文件，见 05-evidence）
- [ ] AC3 dotnet build 0 error
- [ ] AC4 全量 dotnet test 新增红 = 0
- [ ] AC5 05-evidence 全条目标 Verified（Inferred/Unknown 显式标注）
- [ ] AC6 verify-pilot-artifacts.ps1 八件齐 + 关键节 PASS

## Expected Files
见头部 expected_files。

## Verification Commands
```
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~JsonNonAsciiEscapingGuard"
cd ForgeSelf.Api.Tests && dotnet test            # 全量（中档）
pwsh scripts/check-problem-classes.ps1
pwsh scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-09-json-facade-unify -EnforceHeader -EnforceAcCoverage -EnforceInteraction
```
