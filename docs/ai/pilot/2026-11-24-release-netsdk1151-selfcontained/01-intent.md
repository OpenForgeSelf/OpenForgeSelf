# 01 Intent — 修复 NETSDK1151 并产出本地 zip

## 用户原始输入（字面）
1. "看下发布本地zip的命令有什么问题，打个本地包，解决报错、失败"
2. "是你改的脚本让打包过程每个插件都重新安装依赖？"（澄清：非我所改，是脚本既有逻辑 + 并行会话 PC-048）
3. 贴出 NETSDK1151 完整报错："我执行发布本地zip，每个插件都要求重建依赖，我都确认了，但最后报错了"
4. 对方案 A 回复 "a"（授权修复）

## 意图（钢人版）
真正目标不是"让报错消失"，而是**本地 zip 完整产出并通过校验**，且同类 NETSDK1151 不再复发。

## 验收标准（AC）
- **AC1**：UpdateAgent 无条件显式声明 SelfContained=false，不再有带 Condition 的写法。
- **AC2**：同参数 `dotnet publish` 最小复现 EXIT=0，产物为 FDD 单文件（无自包含运行时目录）。
- **AC3**：G2 守卫 `Pc049_…` 通过，台账 check-problem-classes 与 Registry 全绿。
- **AC4**：本地 zip 完整产出，SHA256 校验通过，关键文件（launcher / update-agent.exe / versions/current / 业务 exe）齐备。

## 边界
- 不改 ForgeSelf.Api.csproj、不改 publish-host.ps1、不改 SDK / shim。
- 不碰并行会话在途改动（AGENTS.md、工程流程文档、hooks 等）。
