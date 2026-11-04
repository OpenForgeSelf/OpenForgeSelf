---
task: 2026-11-04-smart-terminal
risk: { level: L3, triggers: [Plugins/SmartTerminal/** 新插件:execute_command 权限+密码/密钥处理面, 内容触发 ApiKey/Password/Secret, 规模 54 文件 L2->L3], raised_by_agent: false }
feature: 043
requirements: [FR-1, FR-2, FR-3, FR-4, FR-5, FR-6, FR-7, FR-8]
rollback: git revert 合并提交即可整体回滚（插件目录自包含）；或宿主侧禁用插件；无数据迁移、无共享 DLL 变更
expected_files:
  - Plugins/SmartTerminal/**
  - Plugins/SmartTerminal/**
  - ForgeSelf.Api.Tests/Plugins/SmartTerminalTests/**
  - ForgeSelf.Web/e2e/plugins/smart-terminal/**
  - ForgeSelf.Web/src/components/SmartTerminal*
  - ForgeSelf.Web/ct/stories.ts
  - ForgeSelf.slnx
  - ForgeSelf.Api/ForgeSelf.Api.csproj
  - ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj
---

# 04 · Task — 2026-11-04-smart-terminal

## Allowed
- 新增/修改上列 expected_files 范围内文件（SmartTerminal 插件本体/测试/CT 故事/e2e/pilot 工件/043 功能文档）。
- csproj 与 slnx 仅做「新增插件项目登记」最小改动（构建顺序，ReferenceOutputAssembly=false）。

## Forbidden
- 修改宿主核心（ForgeSelf.Api 非 csproj 源码）、其他插件、e2e 全局基建（global-setup/playwright 配置）。
- 新增大依赖；修改鉴权/权限内核；把密码写入任何持久化存储或日志原文。
- 触碰本任务范围外文件（expected_files 之外一律不做，门禁机械拦截）。

## 验证命令（真实执行，结果记 05）
```
pwsh scripts/ci/run-gates.ps1 -Base 2aa93aa          # pilot/risk/style 三门禁
dotnet build                                          # 0 error
dotnet test --filter "FullyQualifiedName~SmartTerminal"
cd Plugins/SmartTerminal/web && pnpm install --frozen-lockfile && pnpm build
cd ForgeSelf.Web && pnpm run check && pnpm run test && npx vue-tsc -b
pnpm exec playwright test --config=playwright.config.ts e2e/plugins/smart-terminal   # 冒烟 1 条
```

## 步骤
1. 工件链（本提交）→ 2. 裸色值 token 化 → 3. 历史 trailer 补齐 + rebase 到 main(2aa93aa) → 4. 构建/测试/e2e → 5. dev-stack 走查（05 回填）→ 6. 05/06/07 定稿 → 合并。