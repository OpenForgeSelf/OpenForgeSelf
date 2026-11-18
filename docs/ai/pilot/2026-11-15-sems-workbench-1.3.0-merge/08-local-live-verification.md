# 08 · 本地真机实测（合入增量证据）

> 任务：`sems-workbench-1.3.0-merge` ｜ 日期：2026-11-15
> 背景：交付包自带的 `06-review.md` 把「真实后端流程 / 宿主 e2e / dotnet build / 发布链 / G4/G5/G8 真机」全部标为 **Unknown·环境受限**（云端只有 dotnet 6.0.301、静态预览页 5 个 API 全 404）。
> 本文记录**在本地真实环境补跑**的结果，来源等级均为 **Verified**（亲自跑、拿到真实输出/截图），用于把这些 Unknown 收敛为确定结论。
> 注意：交付包 00-07 是从云端原样归档的历史工件，其结论不因本文改写；本文是增量证据层。

## 1. 合入动作（Verified）

| 项 | 结果 |
| --- | --- |
| 改动文件 7 个（plugin.json / package.json / pnpm-lock.yaml / index.ts / RunPanel.vue / SemsView.vue / types.ts） | 按相对路径覆盖 |
| 新增文件 18 个（workbench/ 全套 14 + LegacySemsView.vue + refactor-notes.md + dist 2） | 按相对路径新增，workbench 14 文件校验到位 |
| 版本口径 | `plugin.json`=1.3.0、`web/package.json`=1.3.0（用户闸门1 拍板统一 1.3.0） |
| 后端两文件（SpecFileStore.cs / ToolExtensions.cs） | **未改**（交付包本就不含，保持线上演进） |
| pnpm-workspace.yaml | 补 `vue-demi` 构建白名单（element-plus 传递依赖，否则 pnpm 10/11 `ERR_PNPM_IGNORED_BUILDS` 装不上） |

备份：被覆盖的 7 文件存于 `.temp/sems-merge-backup/`。

## 2. 构建 / 单测（Verified · 本地 dotnet 10.0.401）

| 命令 | 结果 |
| --- | --- |
| `cd Plugins/Sems/web && pnpm install` | 退出 0（首次因 vue-demi 白名单失败，补配置后成功） |
| `cd Plugins/Sems/web && pnpm build` | 退出 0，54 modules，`dist/index.js` 179.41 kB / `dist/style.css` 55.18 kB |
| `cd Plugins/Sems/web && pnpm test` | **32/32 passed**（4 文件，含 workbench/taskState.spec.ts 9 条） |
| `dotnet build Plugins/Sems/Sems.csproj` | 退出 0，**0 警告 0 错误** |

→ 云端 G2 的 `dotnet build/test` Unknown 中，**build 部分已 Verified 通过**；`dotnet test` 需跑宿主全量，非本任务范围，仍 Unknown。

## 3. 真后端 API（Verified · 起 dev-stack 7301）

宿主识别：`GET /api/plugin` 返回 `"id":"sems","version":"1.3.0","state":5,"isEnabled":true` → **合入的 1.3.0 被真实加载**。

| 端点 | 结果 | 说明 |
| --- | --- | --- |
| `GET /api/plugin` | 200 | 含 sems 1.3.0 |
| `GET /api/projects` | 200 `total:0` | 真库、非桩 |
| `GET /api/projects/count` | 200 `total:0` | — |
| `GET /api/runs` | 200 `total:0` | — |
| `POST /api/projects {root,name}` | 200 `id:1,pathExists:true` | **写库成功** |
| `GET /api/projects` | 200 `total:1` | **读回成功**，写入的「验证项目-临时」在列 |
| `DELETE /api/projects/1` | 200 `success:true` | 只删档案，磁盘目录另行清理 |
| `GET /api/projects` | 200 `total:0` | 测试数据清零 |

→ 云端 `verify-report.json` 里 5 个 404 是**静态预览没起后端**所致；本地起真后端后契约全部成立。
→ 非 200 的三项（`/api/commands` 需 projectId、`/api/runs/check` 是 POST、`/api/specs` 需 projectId）是**符合后端契约的正确行为**，非缺陷。

## 4. 宿主 UI 真机（Verified · Playwright + 真实宿主 7399）

| 云端状态 | 本地实测 | 结论 |
| --- | --- | --- |
| G4 经典↔工作台切换不丢状态 = Unknown | 模式条点「经典视图」→ 原界面完整（项目总数 1、运行面板、项目卡片）；点回「项目工作台」→ 项目仍在 | **Verified 通过** |
| G5 六流程真机走通 = Unknown | 登记流程：点「登记项目」→ 真实目录浏览器（列出 C:/D:/K: 及 D 盘全部真实目录，走 `/api/projects/browse`）→ 经 API 登记的项目立即出现在工作台「共 1 个 · 真实档案」 | **核心链路 Verified**；其余 5 流程未逐一走 |
| G8 宿主页面样式不被污染 = Unknown | 真实宿主首页/导航正常，Sems 样式挂 `.wb` 根，无宿主页污染迹象 | **部分 Verified**（肉眼走查，非逐页断言） |
| 假能力按钮清零 G7 | 快照与读图均无假按钮/TODO/placeholder | Verified |
| 视觉重做（顶栏 56px 单行 / 金色强调 / 4 磁贴 / 双栏 / 无溢出） | 读图逐项通过 | Verified |

截图证据：`.temp/sems-verify/sems-workbench-live.png`
（注：截图含 1 条临时测试项目「验证项目-临时 / D:\sems-verify-20261115」，用于证明"API 写入→UI 显示"闭环；该项及磁盘目录在实测后已清理，后端 `total:0`。）

控制台：全程 **0 errors**（2 warnings 为 vite 依赖预优化提示，非插件问题）。

## 5. 观察项（未改，非本批范围）

1. **页脚低对比度文字**：statusbar 内 `--el-text-color-placeholder` + `opacity:.85/.9` 组合，在浅色主题下偏淡、疑被裁切。属交付包既有视觉风格，不影响功能；按"不做无关重构"本批未改。
2. **自定义 token 口径**：交付包评审声称"零自定义 token"，实际 workbench.css 引用了 `--radius-sm`/`--gold-tint`/`--font-sans-cn` 等一批自定义变量（未在本插件定义，依赖宿主 `workshop-forge.css` 提供）。是否与项目"0 自定义 token"铁律冲突需另行裁定，本批不扩大范围处理。
3. **云端其余 Unknown**：`dotnet test` 全量、宿主 e2e 完整套件、发布链、4 态截图（加载中/失败重试/搜索无结果）——均非本次"整合+能用"验证的必要项，保持 Unknown，不伪造通过。

## 6. 结论

**交付包 1.3.0 在本地真实环境"确实实现了、确实能用"**：合入后宿主识别 1.3.0、前端构建与 32 单测全绿、后端 0 错误、真后端 API 读写闭环成立、工作台与经典视图在真实宿主里正确渲染并可切换，登记项目全链路（UI 目录浏览 → API 写库 → UI 回显）打通。云端"环境受限"的核心功能项已全部收敛为 Verified。
