# 02 Spec：发行链加固（task-13）

## AC1 磁盘水位预检

release-local 在路径解析后、任何构建前，对 OutputRoot/UpdateDir/buildTemp 所在盘执行 Get-PSDrive 检查；free < -MinFreeGB（默认 2.5GB）立即 throw（报错含盘符/现值/阈值/清理建议），exit 1 零构建。阈值参数化使低水位可模拟（-MinFreeGB 9999 = 必中止）。**验收标准**：水位测试立即中止且报错可读；正常水位跑程不再因磁盘满以模糊 IO 错失败。

## AC2 ESBUILD_BINARY_PATH 护栏

组包最前（TEMP 环境设置后）检测该环境变量；存在即黄字提示 + Remove-Item（子脚本同进程继承），使插件 web `pnpm install` 的 esbuild@0.25.0 postinstall 不再按全局 0.25.12 二进制校验而炸。**验收标准**：带变量的一轮发布日志出现「removed ESBUILD_BINARY_PATH」且 12 插件 install 全过。

## AC3 worktree -UpdateDir 锚定

缺省 updates 目录计算沿 repoRoot 祖先链检测 `<主仓>\.temp\wt-*` 形态：命中则把 updates 根锚回主仓根上级（真源），否则维持 Split-Path -Parent 原行为。**验收标准**：逻辑双场景单测正确（wt-* 子路径→主仓上级；主仓根→原行为）；真轮 main 路径不受影响。

## AC4 UpdateDir 验包固定步骤

三件套（zip/SHA256SUMS.txt/RELEASE-NOTES）拷贝到 -UpdateDir 后，按 SHA256SUMS.txt 逐条对目标文件复算 SHA256 并对表；文件缺失/哈希失配/零可解析条目均 throw；PASS 打印复算文件数。**验收标准**：正常轮 PASS 且独立 Get-FileHash 复算一致；人为缺文件时中止。

## AC5 store 毒残留治本（方案 b，lead 批准）

12 个插件 web（AgentHub/AIAgent/CostScope/DesignSystem/FileTools/Home/ImGateway/McpCenter/QuickLinks/Sems/TodoTracker/ToolBridge）的 pnpm-lock.yaml esbuild 解析版 0.25.0 → 0.25.12（`pnpm update esbuild --lockfile-only --depth=Infinity`，vite 声明范围内重解析）。**验收标准**：12/12 lockfile=0.25.12；git 面仅 lockfile 变更，package.json 零改动；沙箱发布轮 install 全过。根因=ESBUILD_BINARY_PATH 穿透 store 硬链接致 0.25.0 永久损坏（fresh install 必炸）。

## Interaction Design（无界面交互）

纯脚本链改造，无 UI。操作面=命令行参数（-MinFreeGB/-UpdateDir）与 stderr 报错文案；报错文案含盘符/现值/阈值/建议动作。
