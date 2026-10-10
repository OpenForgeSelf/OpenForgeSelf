# Evidence

> 阶段：Stage 7。只记录实际发生的事件，均标注证据等级。

## Task
PILOT-2026-1010-ABOUTVER（2026-10-10-aboutpanel-real-version）

## Changed Files
- `ForgeSelf.Web/src/components/settings/AboutPanel.vue`（+15/-1：script 块新增取数三态逻辑；模板 `v0.1.0` → `v{{ version ?? '…' }}`）
- `ForgeSelf.Web/src/components/settings/__tests__/AboutPanel.test.ts`（新增，4 例）

（git diff --stat 核对：仅上述文件，未触碰其他业务文件；只读检查未发现本会话对 .trash/ 或 publish/ 的写入。）

## Static Analysis（pnpm run check = vue-tsc -b && eslint）

Command:
```bash
cd ForgeSelf.Web && pnpm run check
```

Result: PASS（Verified）——改前基线 EXIT=0（0 error/76 warnings），改后 EXIT=0（0 error/76 warnings，持平）。

```text
? 76 problems (0 errors, 76 warnings)   # 改前基线一致
? 76 problems (0 errors, 76 warnings)   # 改后一致（06-review 附基线口径说明）
```

## Unit Test

Command:
```bash
cd ForgeSelf.Web && pnpm vitest run src/components/settings/__tests__/AboutPanel.test.ts
# 及全量回归
cd ForgeSelf.Web && pnpm vitest run
```

Result: PASS（Verified）
- 新增单测：Test Files 1 passed (1)；Tests 4 passed (4)（占位 v…/真实回显/拒绝降级 v未知/空值降级 v未知）。
- 全量回归：Test Files 67 passed (67)；Tests 769 passed (769)；EXIT=0；Duration ≈14.7s。

## Build

N/A 独立构建步：vue-tsc -b（type build check）已含于 check PASS；未执行生产打包（非本任务门禁必需）。

## Integration Test

N/A（无集成面新增）

## E2E

N/A（如实记录，非掩盖）：
- 51888 运行实例加载的是 publish 产物，本次源码改动未编译进该实例；用户硬性禁止改 publish/ 与停启进程，live 走查不可行。
- e2e 需 dev 栈（AGENTS.md §2.3 禁止 agent 自起 dev 栈），故未跑。
- 降级证据：同接口同字段（UpdatePanel 的 currentVersion 展示）已在 live 走查中长期Verified（工作区台账 2026-10-10 输入2 记录 51888 已更新 2.4.0.2610101148 且 AIAgent/WorkflowEngine 走查通过）——本组件为同源调用方式（Inferred）。

## 真实版本号取证

- 【Verified】`GET http://localhost:51888/api/update/status` 实测返回 **401 Unauthorized** 接口存在且受 ApiKeyPolicy 保护（只读 Invoke-WebRequest，未伪造 token、未动进程）。
- 【Inferred】运行实例真实版本 = 2.4.0.2610101148（依据：工作区台账 `.forgeself/memory/2026-10-10.md` 会话记录「51888 已更新 2.4.0.2610101148」；publish/ForgeSelf.exe 的 PE ProductVersion=1.0.0+commit hash 为程序集元数据，非运行时版本，不作依据）。
- 【Unknown】组件 live 渲染截图（受上述边界限制无法取得；单测已验证取数与渲染链路）。

## Screenshots

N/A（单测断言覆盖渲染文案；live 截图受禁令限制，见 E2E 节说明）。

## 验收标准对应证据（门禁对齐）

| AC | 证据（05-evidence 原文） | 等级 |
|----|------------------------|------|
| AC1 | 版本行以 `v` 开头 + 加载占位 `v…`：Unit Test 节新增单测 4 例含「占位 v…」，4/4 通过 | Verified |
| AC2 | 接口正常回显真实版本：真实版本号取证节（Inferred 运行实例 2.4.0.2610101148）+ 单测「真实回显」用例 | Inferred |
| AC3 | 接口异常降级 `v未知` 且不抛错：Unit Test 节「拒绝降级 v未知 / 空值降级 v未知」2 例 | Verified |
| AC4 | 样式类与布局不变：Static Analysis 节 vue-tsc -b + eslint 0 error（76 warnings 与基线持平） | Verified |

## Known Limitations
- 改动需随下次前端发布（publish/渲染管线）才会出现在 51888 关于页——本任务授权边界内不可执行发布。

## Unresolved Issues
（无失败遗留；门禁全绿）
