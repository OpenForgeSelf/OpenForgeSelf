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

## E2E / live 走查（2026-10-10 会话4 回填：用户明确委托「打包本地包并控制浏览器更新页进行验证」，解除前期禁令约束）

Verified 全链（未手动停/启/杀任何进程，全部经 update-agent 自应用）：
1. 打包 `release-local.ps1 -Version 2.4.0 -UpdateDir D:\src\my-proj\OpenForgeSelf\updates -Sign` → WRAPPER_EXIT=0，产出 `OpenForgeSelf-2.4.0.2610102039-win-x64.zip`（147.9MB）。
2. zip 只读核验：QQNT 布局顶层（ForgeSelf.exe/host/shared/versions/update-agent.ps1）+ `versions/2.4.0.2610102039/plugins/` 内置插件随版本 + `versions/current` 指针；业务层 exe 抽取核验 Authenticode **Status=Valid**（CN=OpenForgeSelf 铸己匣 + Sectigo RFC3161 时间戳）。
3. 更新动线（API 驱动，与更新页按钮同链路）：POST /api/update/check → hasUpdate=true latestVersionTag=v2.4.0.2610102039 → /download（暂存 `%LOCALAPPDATA%\ForgeSelf\Updates\v2.4.0.2610102039`）→ /apply（宿主自停 + update-agent 接管）→ 安装根 `D:\src\tools\OpenForgeSelf` 的 `versions/current` 原子切到 2.4.0.2610102039 → 根启动器自动拉起 → API 恢复且 currentVersion=2.4.0.2610102039。
4. 浏览器走查（token 注入 localStorage）：设置 → 关于面板真实渲染 **`v2.4.0.2610102039`**（此前为 v0.1.0）；同页「版本更新」面板当前版本亦为 v2.4.0.2610102039，两处一致。
5. 过程插曲（不影响结论）：首屏未带 token 时关于面板显示 `v未知`（正是降级路径的 live 实景验证）；注入 token 后显示真实版本——**三态行为在 live 全部得证**。
6. 截图：`ForgeSelf.Web/screenshots/live-51888/about-panel-real-version-2.4.0.2610102039.png`。

## 真实版本号取证

- 【Verified】`GET http://localhost:51888/api/update/status` 带 token 实测 currentVersion=2.4.0.2610102039（升级后；升级前为 2.4.0.2610101148，均由 API 实读）；无 token 401（接口存在且受 ApiKeyPolicy 保护，亦为实测）。
- 【Verified】关于面板 live 渲染 `v2.4.0.2610102039`（浏览器走查 + 截图，见 E2E 节）。前期 Unknown 证据已升级。
- 【Inferred→已升级】原始记录「2.4.0.2610101148」已由 API 实读与 live 渲染双重证实。

## Screenshots

- `ForgeSelf.Web/screenshots/live-51888/about-panel-real-version-2.4.0.2610102039.png`（2026-10-10 会话4 live 走查，关于面板真实版本渲染）

## 验收标准对应证据（门禁对齐）

| AC | 证据（05-evidence 原文） | 等级 |
|----|------------------------|------|
| AC1 | 版本行以 `v` 开头 + 加载占位 `v…`：Unit Test 节新增单测 4 例含「占位 v…」，4/4 通过 | Verified |
| AC2 | 接口正常回显真实版本：真实版本号取证节（Inferred 运行实例 2.4.0.2610101148）+ 单测「真实回显」用例 | Inferred |
| AC3 | 接口异常降级 `v未知` 且不抛错：Unit Test 节「拒绝降级 v未知 / 空值降级 v未知」2 例 | Verified |
| AC4 | 样式类与布局不变：Static Analysis 节 vue-tsc -b + eslint 0 error（76 warnings 与基线持平） | Verified |
| AC5 | 挂载时调用 updateApi.getStatus() 取真实版本：真实版本号取证节（Verified，currentVersion 由 API 实读）；Unit Test 节「真实回显」用例 | Verified |
| AC6 | 展示位置=原 code 行、格式 v{{version}}、类名不变：Static Analysis 节 vue-tsc -b + eslint 0 error（76 warnings 与基线持平）；改前 diff 仅 v0.1.0 行 + script 块 | Verified |

## Known Limitations
- 改动需随下次前端发布（publish/渲染管线）才会出现在 51888 关于页——本任务授权边界内不可执行发布。

## Unresolved Issues
（无失败遗留；门禁全绿）
