# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，不提前决定具体代码实现。
> Task ID：PILOT-076｜日期：2026-10-30

## Problem

发布本地 zip 必须每次人工指定版本号（`release-local.ps1 -Version 2.4.x`），「当前版本行」没有仓内真源——它散在操作者的记忆、聊天记录与 Sems 命令参数里：本会话 15:00 刚登记的 Sems 命令就把 `-Version 2.4.23` 写死在脚本串里，下次发版必然漂移或忘改；而宿主工程文件 `ForgeSelf.Api.csproj` 里的版本属性还停在 `VersionPrefix 2.3`（落后现行发行线 2.4.x 十九个 patch），没有承担真源职责。

## Why

用户指令（输入3，2026-10-30 15:13）确立新规则：**版本行真源 = csproj**；发布 zip 不需指定版本；csproj 版本行由用户控制、非经允许不得改。这使「点一下 Sems 命令就出正确版本号的包」成为可能，也把「改版本线」从每次发布的隐式动作收拢为一个显式、经授权的动作。

## Expected Outcome

1. `ForgeSelf.Api.csproj` 版本属性块承载**当前最新发行线的固定三段**（默认 2.4.22，闸门1 可改拍 2.4.23），开发者本地兜底版本形态不变（三段+时间码）。
2. `release-local.ps1` 本地不传 `-Version` 时：读 csproj 版本行 → 解析固定部分（三段）→ 自动补时间码；**CI（GITHUB_REF_NAME）与显式传参行为逐字节不变**。
3. 文档同步新规则（真源 + 三处现行引用点）；Sems 命令 6 去掉 `-Version`。
4. 新增仓库级守卫测试锁住上述不变量（红→绿）。

## Constraints

- 规范 §1 硬性约束对照：不涉生产环境 / DB / 鉴权 / 大依赖；不做无关重构；改动仅限本任务清单文件。
- **CI 通道是红线**：`GITHUB_REF_NAME` 存在时行为必须与今天完全一致（tag 原样、不改写——R10 ⑤「产物版本 ≠ tag」禁令）。
- 发布注入通道（`FORGESELF_RELEASE_VERSION` 环境变量）与 `publish-host` / `publish-bootstrapper` **零改动**。
- `.ps1` 编辑保持 UTF-8 BOM（PC-006）；脚本一律 pwsh 7。
- csproj 版本行取值（2.4.22 vs 2.4.23）属闸门1 待拍板项，本文默认 2.4.22（用户原话「最新版本号」= 最新已发行线）。
- 发布执行本身不在本任务范围（属发布动作，须用户单独同意）。

## Success Criteria

- `Get-CsprojReleaseVersion`（新函数）对真实仓库返回合法三段号，与 csproj `<VersionPrefix>` 一致（行为测试 + 独立第二解析器互证）。
- 新守卫测试集先红（对现状）后绿（对实现）。
- 中档门禁（全量 `dotnet test`）无新增红（对表既有基线）。
- 四份文档 + Sems 命令更新到位，逐处可指认。
- BOM 复验绿（PC-006 守卫复跑通过）。
