# Evidence

> 阶段：Stage 7｜Task ID：2026-10-30-sems-b1-ui｜日期：2026-10-30
> 只记实际跑过的命令。中文日志若被控制台码页打乱，以 ASCII 结果行和截图为准。

## 命令

插件前端单测（Verified）：

```text
cd Plugins/Sems/web
pnpm test
Test Files  2 passed (2)
Tests  21 passed (21)
```

其中 `src/specOps.spec.ts` 13 条，`src/confirmOps.spec.ts` 8 条。

插件前端构建（Verified）：

```text
pnpm build
dist/style.css  23.29 kB
dist/index.js   66.14 kB
built in 307ms
```

设计门禁（Verified）：

```text
cd ForgeSelf.Web
pnpm run design:check
[style-tokens] PASS：无新增硬编码色值
[design-compliance] PASS：无新增设计规范违反
```

规格主链路 e2e（Verified）：

```text
cd ForgeSelf.Web
pnpm exec playwright test e2e/plugins/sems/sems.spec.ts -g "规格主链路" --workers=1
Running 1 test using 1 worker
1 passed (49.5s)
```

日志 `.temp/sems-b1-ui-e2e.log` 的中文是乱码，ASCII 行是 `1 passed (49.5s)`，进程 `EXIT=0`。截图已读：`ForgeSelf.Web/screenshots/e2e/sems/sems-specs.png`。画面上有项目卡片、规格 (1)、标题「e2e 规格」、正文、AC1「已登记 1 条」、关联 `v1 · commit · e2e-ref-1`，以及 toast「已创建规格」「已关联 commit e2e-ref-1」「已登记用例标识」。说明文案写着登记不表示用例已通过。

后端 C# 本轮未改。先前 `.temp/results-sems-b1b/sems-b1b.trx` 的 103/103 仍是上一批证据，不把本轮说成重新跑过。

## AC 对照

- AC1 Verified。e2e 在创建前断言「还没有规格。」，随后用例通过。
- AC2 Verified。截图与 toast 有「e2e 规格」v1；用例向 `GET /api/projects/{id}/specs` 对账 title 与 version=1。
- AC3 Verified。`buildCreateBody` 单测：空白标题得到 `title 必填`，没有请求体。
- AC4 Verified。`buildVersionBody` 单测：沿用上一版时对象没有 criteria；正文空为 `content 必填`。dev-stack 上点了「追加版本」，v2 仍保留 AC1。
- AC5 Verified。截图有 `commit · e2e-ref-1`。用例断言反查按钮文本含该 specId，且整测通过。
- AC6 Verified。截图状态为「已登记 1 条」，说明不写「已通过」当状态。用例断言 coverage 里 AC1 的 linked 为 true。
- AC7 Verified。`listPhase(false, 错误文案, 0)` 为 error。
- AC8 Verified。`listPhase(false, '', 0)` 为 empty。
- AC9 Verified。`listPhase(false, 后台失败, 2)` 为 list。
- AC10 Verified。重复 id `AC1` / `ac1` 在 `parseCriteria` 被拒绝。

## dev-stack 走查（Verified）

`pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/dev-stack.ps1` 退出码 0。日志有「后端就绪 (200)」「前端就绪 (200)」。状态文件 `.temp/dev-stack.json`：前端 `http://localhost:7399`，后端 `http://localhost:7301`。没有停用户宿主。

在该前端打开 `/sems`（注入 `forge_api_token` 后）：

- 页头版本徽标为 v1.2.0。
- 点「规格」看到「还没有规格。」和「新建规格」。
- 空标题点保存，对话框留下 `title 必填`，没有关掉。
- 填标题「走查规格」、正文和 `AC1 能在卡片里看到标题` 后保存。列表与详情出现，toast 为「已创建规格「走查规格」v1」。
- `GET /api/projects/1/specs` 返回 title=走查规格、version=2（下一步追加之后）、criteria 仍是 AC1。
- 追加版本默认勾选沿用验收标准，正文改为第二版。界面变为 v2，AC1 仍在，状态「未登记」，toast「已追加 v2」。
- 版本号填 1 再点「读取该版本」：横幅「正在查看 v1。列表上的版本号是最新版。」正文回到第一版。列表按钮仍显示 v2。
- 视口 390×844 时 `.specs` 右缘 334、页宽 390，没有横向溢出。

截图：`ForgeSelf.Web/screenshots/dev-stack/sems-specs-desktop.png`、`ForgeSelf.Web/screenshots/dev-stack/sems-specs-mobile.png`。规格文件落在 dev-stack 数据根的插件数据目录，不在仓库工作区里。

## 未跑

- 匿名访问规格端点：Verified。dev-stack `http://127.0.0.1:7301` 不走代理。无令牌时下列请求都是 401：`GET /api/projects/1/specs`、`POST /api/projects/1/specs`、`GET …/specs/by-ref?kind=commit&ref=none`、`GET …/specs/aaaaaaaaaaaa`、`GET …/links`、`GET …/coverage`。同一列表带令牌为 200。第一次 502 是本机代理把 localhost 转走了，设 `NO_PROXY` 后重测。
- 本地发布与 `:51888`：未做。

## 截图读图

规格区在项目卡片内，一个金色「新建规格」。列表、正文、验收标准、关联、版本号和反查在同一列。成功 toast 叠在卡片上沿，不挡住规格正文。窄卡片里按钮换行，没有溢出到卡片外。
