# Review

> Task ID：2026-10-26-ct-nav-config｜日期：2026-10-26

## 八问

1. **需求是否满足**：侧栏标题、分组显示名与顺序、折叠阈值集中到 `navConfig`；深链保留侧栏并高亮；`chrome=0` 关闭侧栏。PASS
2. **是否最小改动**：5 个文件，无新增依赖，无新目录（工件除外）。PASS
3. **是否影响业务组件**：只改画廊壳与测试助手，未触碰任何产品组件或插件源码。PASS
4. **测试是否真实**：13 条 CT 在真实 Chrome 执行；另用浏览器对 5190 现场三地址走查。PASS
5. **是否引入回归**：既有 12 条 CT 全绿，4 张像素基线比对通过。PASS
6. **文档是否同步**：`ct/README.md` 已更新；旧任务 `02-spec` / `03-plan` / `05-evidence` 已标注深链契约调整。PASS
7. **风险**：
   - Minor M-1：深链行为由「一律无侧栏」改为「人类保留 / CT 用 `chrome=0` 关闭」，属契约变更；已同步旧任务 AC1 与用例，但仍需用户在闸门2 一并确认。
   - Minor M-2：HMR 场景下旧标签页可能残留旧模块（`groupStories2` 报错），重启 dev server 即消失；非代码缺陷，服务端模块已实证干净。
   - Info I-1：`navConfig.groups.key` 与 story 前缀靠约定，守卫未校验；写错 key 会落到末尾（可见但不报错）。
8. **未做**：搜索、收藏、多级嵌套、引入 Storybook 依赖（明确非目标）。

## Final Decision

**APPROVED**（无 Major；Minor 2 项、Info 1 项已在 `05-evidence.md` 记录，不阻断验收）
