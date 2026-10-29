# Final Report

> Task ID：2026-10-26-ct-nav-config｜日期：2026-10-26｜状态：**已实现并验证，待用户验收（闸门2）**

## 1. 任务

配置组件画廊（自建 CT Gallery，非 Storybook）的侧边栏导航：分组显示名与顺序可配、深链保留导航并高亮、CT 截图基线不受导航影响。

## 2. Changed Files

见 `05-evidence.md`「Changed Files」：`ct/stories.ts`、`ct/main.ts`、`src/components/ct-support.ts`、`src/components/Gallery.ct.spec.ts`、`ct/README.md`。

## 3. Validation

| 项 | 结果 |
| --- | --- |
| `pnpm run type-check` | exit 0 |
| 本批 eslint | 0 error 0 warning |
| 组件规范守卫 | PASS（4 story / 4 ct.spec） |
| `CT_FRESH=1 pnpm run test:ct` | **13 passed** |
| `pnpm run design:check` | PASS（棘轮基线未上涨） |
| 5190 现场（浏览器走查三地址） | 全 PASS；控制台旧报错已归因为 HMR 缓存残留 |

## 4. Review

`06-review.md`：八问 PASS，Final Decision = **APPROVED**；Minor 2 项、Info 1 项，均不阻断。

## 5. 行为变更

- 深链 `?story=<id>`：由「一律不渲染侧栏」改为「人类浏览保留侧栏并高亮当前故事」。
- 新增 `&chrome=0`：不生成侧栏、无 250px 留白（`ct-support.ts` 统一使用，保证像素基线稳定）。

## 6. 风险

- 契约变更需用户知晓（已在旧任务 `02-spec` AC1 / `03-plan` 偏差表 / `05-evidence` 标注）。
- HMR 旧标签页可能残留旧模块，刷新或重启 dev server 即消除。

## 7. 下一步（闸门2 / 闸门3）

1. 用户在 http://127.0.0.1:5190/ 验收侧边栏效果（dev server 已常驻）。
2. 验收通过后，与 `2026-10-26-component-ct-gallery` 主批一起按显式路径分批提交（闸门3）。
