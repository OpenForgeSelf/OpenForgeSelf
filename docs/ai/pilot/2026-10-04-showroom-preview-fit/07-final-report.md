# Final Report

> 阶段：Stage 9｜任务：展厅预览区「太小、看不全」的结构性修复（输入21 报障 / 输入22 拍板）

## 一句话结论

展厅舞台现在有**视图三档**（适应 / 1:1 / 最大化）+ **可拖右下角改尺寸** + **档位记忆**，并用 CSS `zoom` 做等比缩放（永不放大）；同时修掉了第二个成因（展厅栏与工作台共用 `max-width:1240` ⇒ 放大窗口画布不变宽）。1372x768 下桌面稿从"右侧被裁一半"变成"整幅 61% 入镜"，1920x1080 下直接是 **1:1 全幅、零裁切**。唯一没兑现的是"滚动条常驻可见"——本机 Chrome 是浮层滚动条且忽略自定义条样式，已如实登记为已知缺口 **G22**。

## Validation（真跑过的门禁，全部 Verified）

| 门禁 | 命令 | 结果 |
|---|---|---|
| 插件前端 | `cd Plugins/DesignSystem/web && pnpm run check` | 0 error（无输出） |
| 插件单测 | `pnpm run test` | **262 passed / 20 files**（含新增 `fit.test.ts` 12 条） |
| 插件构建 | `pnpm run build` | `dist/index.js 445.75 kB`、`dist/style.css 97.14 kB` |
| 宿主前端 | `cd ForgeSelf.Web && pnpm run check` | **0 error / 81 warning**（与基线同数；本批文件 0 告警） |
| 插件层 e2e（定向） | `--grep "预览视图档" --workers=1` | **6 passed**（E1~E6，1372x768 + 1920x1080） |
| 插件层 e2e（整目录，终态源码） | `E2E_FRONTEND_PORT=7402 E2E_BACKEND_PORT=7502 … e2e/plugins/design-system --workers=1` | **39 passed / 0 failed (6.7m)**（既有 33 + E 片 6）。此前一轮默认端口跑出 36/2 红，两条都是 `Failed to fetch` 环境红 ⇒ 钉端口复跑 8/8 绿，判据未动 |
| 反向探针 | `fitScale` 写死 `return 1` | 单测红 2 条 + e2e 红 2 条（E1/E4），还原后复绿 |
| 读图 | 7 张 e2e 截图 + 输入21 现场旧图 | 逐张核对（含抓出"有骨架没皮肤"的空图并据此加强判据） |
| 本地插件包 | `scripts/package-plugin.ps1 -Plugin DesignSystem -Force` | `design-system-3.1.0.forgeself-plugin`，SHA256 `FFD2733F…0EE1`；**按包内容验真**（Version 3.1.0 / 无宿主共享 DLL / `index.js` 445,751 B 与本次构建同尺寸 / 新代码与新样式全部命中 / 无探针残留） |

## Review 摘要

`06-review.md` 八问全答；Final Decision = **APPROVED（自签，非独立验收）**。需求 6 条：5 条完整兑现，1 条（滚动条常驻）经实测证明不是本插件能承诺的 ⇒ 降级为 G22 + 提示行 + 可达判据。

## 交付物清单（本批 10 条路径）

- 代码：`Plugins/DesignSystem/web/src/showroom/fit.ts`（新）、`fit.test.ts`（新）、`Stage.vue`、`Showroom.vue`
- 判据：`ForgeSelf.Web/e2e/plugins/design-system/design-system-showroom.spec.ts`（新增 E 片 6 条）
- 文档：`Plugins/DesignSystem/README.md`、`docs/02-features/036-design-system.md`、`docs/04-standards/agent-workflow.md`、`docs/07-decisions/not-taken-decisions.md`（031/032/033）、`.agents/skills/design-system-verify/SKILL.md`（#72）
- 工件链：`docs/ai/pilot/2026-10-04-showroom-preview-fit/00`~`07` + 原 `mini-task.md`
- 日志：`.forgeself/memory/2026-10-04.md`（不入库）

## 代价与回滚

- 代价：插件产物 +240 B（index.js）/+80 B（style.css）；运行期多一个 `ResizeObserver`（卸载即 disconnect）。
- 回滚：`git checkout --` 上述路径即回；未提交前 HEAD = `5dd975e`。
- 未做（NOT-to-do，已入台账）：031 自绘滚动条组件、032 侧栏折叠图标条、033 画布盒内居中。

## 状态与下一步

**⚠️ COMPLETED_WITH_RISK**（风险＝G22 的浏览器观感需用户真实走查确认 + 运行实例只读复验未做）。
等用户：① 闸门2 验收（读图 + 读数在 `05-evidence.md`）；② 提交授权——**注意本批与 M3 未提交集合共用 `Stage.vue`/`Showroom.vue`/`README.md`/e2e spec 等文件，无法拆成两次提交**，需一并处理；③ 升级后我在 `:51888` 做只读复验（含"大屏 1:1 全幅"这一条的真实观感）。
