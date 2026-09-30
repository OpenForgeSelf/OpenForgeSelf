# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 原则：所有条目必须来自**真实仓库内容**，禁止凭常识推测。

## 项目结构

OpenForgeSelf（铸己匣）——宿主 + 插件架构桌面应用。发布链路涉及：

- `.github/workflows/release.yml`：tag `v*` 触发 CI，调 `release-local.ps1` + `publish-release.ps1`
- `scripts/release/release-local.ps1`：本地/CI 统一打包入口（`[switch]$Sign` 缺省 false）
- `scripts/release/package-release.ps1`：zip 组装 + 签名门（`if ($Sign)` 才调 sign-publish）
- `scripts/sign-publish.ps1`：Authenticode 签名（自签证书自动生成/复用 + DigiCert 时间戳）
- `docs/04-standards/packaging-upgrade-backup.md`：打包/升级/备份规则**唯一真源**
- `.agents/skills/plugin-publish-verify/SKILL.md`：发布验证技能

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 后端 | ASP.NET Core（.NET 10）+ NewLife.XCode | ForgeSelf.Api/ForgeSelf.Api.csproj |
| 前端 | Vue 3 SPA | ForgeSelf.Web/package.json |
| 打包 | PowerShell 脚本链 | scripts/release/release-local.ps1 |
| CI | GitHub Actions | .github/workflows/release.yml |
| 签名 | signtool + 自签证书 | scripts/sign-publish.ps1 |

## 架构特点

宿主 + 插件（`Plugins/<X>` + plugin.json 注册）；QQNT 式安装布局（公共层 + `versions/<ver>/` + plugins 与 versions 并排）；发布 = 打 tag 自动发布 → CI 打包 GitHub Release → 页面自动更新（update-agent 自更新，禁止 agent 停/启/杀宿主进程）。

## 测试方式

后端 `dotnet build` + `dotnet test`（ForgeSelf.Api.Tests）；前端 `pnpm run check` + `pnpm run test`；e2e 走 Playwright（`ForgeSelf.Web/e2e/`）。本任务为 CI/文档策略变更，验证重点 = 本地打包实测 + git/hook 链路。

## 构建命令

```powershell
powershell -File scripts/release/release-local.ps1 -Version <v>   # 默认不签
dotnet build   # 后端门禁（本任务不改代码，仅确认）
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| scripts/release/ | release-local / package-release / publish-release / sign-publish |
| .github/workflows/ | release.yml（CI 发布） |
| docs/04-standards/packaging-upgrade-backup.md | 打包/升级/备份唯一真源 |
| .agents/skills/plugin-publish-verify/ | 发布验证技能 |
| docs/ai/pilot/2026-09-30-sign-default-off/ | 本任务工件目录 |

## 代码组织方式

发布策略分散在三处表述：CI workflow（强制行为）、真源文档（规则表述）、技能与 AGENTS.md（操作指引）。本次变更需三处同步，避免「文档说必带、CI 不传」的错位。

## 现有工程规范

- AGENTS.md §2.3：发布规范句「发布必带 `-Sign`（输入42 起；CI 已接线）」——本次要改
- 真源 `docs/04-standards/packaging-upgrade-backup.md` §1.1：签名行「发布必带 -Sign（输入42 起铁律）」——本次要改
- plugin-publish-verify SKILL.md：`-Sign` 为发布必带（输入42 铁律）——本次要改
- 用户指令 2026-09-30 输入2：「脚本默认不用加签名，不用自签，传参指定的时候再签名……流水线默认不签名」
- git 铁律：只提交自己改的（工作区混有并行会话 PILOT-050 改动）；同一任务汇总一次性提交；提交前用户已授权（输入2 含「提交推送打tag再试试」）

## 候选低风险任务

发布签名语义反转：CI 默认不签名、仅显式传 `-Sign` 才签。低风险：不改打包/签名实现逻辑，仅改默认行为与文档表述；`sign-publish.ps1` 能力完整保留。

## 选择该任务的原因

CI run `36664225915` 两次卡死在自签证书生成（「[2/5] 已生成自签代码签名证书」20min+ 无进展），Release 从未产出，阻塞发布链路。用户明确指令（输入2）要求默认不签、参数指定才签。
