# 07 Final Report — 前端构建版本感知增量

## 1. Repository Understanding

pnpm 前端构建链：宿主 `ForgeSelf.Web`（vite outDir=ForgeSelf.Api/wwwroot）+ 15 个插件 `Plugins/*/web`（dist/）。`scripts/release/build-frontend.ps1` 为唯一构建入口，`release-local.ps1` 发布链首步调用。宿主 package.json version 原为恒定 0.0.0；csproj VersionPrefix=2.4.22 为发行线真源。

## 2. Selected Task

构建脚本版本感知增量：以 package.json version 为标识，上次构建记录一致且产物在 → 跳过；构建成功后记录。轻量级（mini-task.md），闸门1 依据 = 用户输入3 原文 + 方案拍板回答。

## 3. Changed Files

见 05-evidence §Changed Files（2 个业务文件 + 运行时 manifest + 本工件链）。

## 4. Validation

7 个场景全绿（见 05-evidence 表，全部 Verified）：首建/跳过/版本变更重建/一致性警告/全量 16 单元 0.0s 复用。

## 5. Evidence

见 05-evidence.md。

## 6. Review

见 06-review.md，APPROVED，Risk L1。

## 7. Risk

L1。最大残留风险 = 改码未 bump 版本导致误跳过（用户明确选择人工版本纪律）。

## 8. Problems Found

1. PowerShell 5.1 `Set-Content -Encoding utf8` 写 JSON 带 BOM 会炸 PostCSS/JSON 解析（验证手段教训，已留痕 05）。
2. 宿主构建单跑 54s（沙箱）——增量机制使打包链常态不再包含它。

## 9. Process Evaluation

方案关键分叉（宿主 0.0.0 处理）先钢人论证 + 单问题请拍板后再动手，避免返工；无信息丢失。

## 10. 问题类归档

无（非缺陷类任务，未命中 PC 台账）。

## 11. 重构提案

无。

## 12. 悬空教训清单

（必须为空）

- 无。

## 13. 后续

- 闸门2：本报告交用户验收；验收通过前不提交。
- 提交范围（待用户点头）：`ForgeSelf.Web/package.json`、`scripts/release/build-frontend.ps1`、本 pilot 目录。
