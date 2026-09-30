# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，**不提前决定具体代码实现**。
> Task ID：PILOT-051（2026-09-30-sign-default-off）｜日期：2026-09-30

## Problem

CI 流水线（`.github/workflows/release.yml` Build 步骤）强制传 `-Sign`，触发 `sign-publish.ps1` 的自签证书自动生成/信任流程。实测 run 36664225915 两次卡在「[2/5] 已生成自签代码签名证书」（20min+ 无进展），Release 从未产出，发布链路被阻塞。

## Why

签名只应在明确需要时进行（如对外商业交付）；日常/CI 构建不应强制自签——自签证书生成在 GitHub runner 上不可靠且拖慢发布链路。用户指令（2026-09-30 输入2）：脚本默认不加签名、不用自签，传参指定时才签名；流水线默认不签名。

## Expected Outcome

- 发布脚本与 CI **默认不签名**；仅在显式传 `-Sign`（或 workflow 手动加参）时执行 Authenticode 签名
- CI 重跑能走完 构建 → 打包 → Release 全链路，不再卡在自签证书生成
- 签名能力完整保留（`sign-publish.ps1` 不动、`-Sign` 参数保留、商业证书 `-PfxPath` 可插拔保留）

## Constraints

- 不删除签名能力，只改默认行为与文档表述
- 不改 `release-local.ps1` / `package-release.ps1` / `sign-publish.ps1` 的实现（`if ($Sign)` 门、switch 缺省 false 均已满足「默认不签」）
- 不改发布/打包其余逻辑
- 文档三处（真源 §1.1、AGENTS.md §2.3、plugin-publish-verify SKILL.md）表述必须同步，避免「文档说必带、CI 不传」错位
- git：只提交自己改的（4 文件 + 本 pilot 目录），不提交并行会话 PILOT-050 的 ~29 个改动

## Success Criteria

1. `.github/workflows/release.yml` 无 `-Sign`
2. `release-local.ps1` 不带 `-Sign` 运行：无 Authenticode 签名步骤、不生成证书、正常出 zip（本地实测 exit=0）
3. 文档三处表述全部改为「默认不签、参数指定才签」
4. git 提交通过 pre-commit hook（PILOT 工件 00-07 齐全）→ push github → 重打 tag `v2.2.2026.0930` 推送
5. CI 重跑走完（Release 产出，不再卡在签名步骤）
