# Intent

> Task ID：`2026-11-06-dbadmin-tables-loading`｜日期：2026-11-06

## Problem

点击 DbAdmin 左栏连接后，`loadTables` 在请求返回前没有任何反馈；用户自建连接块会先显示「（无表）」，看起来像「点了没反应 / 没有表」。同类异步加载（切数据源、查询、详情）在其它插件的交互规范里也没有统一要求，未来新插件容易重复。

## Why

- 用户体验：加载表是主链路（新建连接 → 展开查看表），无反馈会误导用户重复点击或以为连接失败。
- 流程：现有 `ui-ux-design` 只写「加载态有 spinner/skeleton」，没有「异步动作必须即时 loading、空态不得抢先、重复点击去重 / 竞态防护」的可验收条款；`plugin-development` §3.4 也没有异步加载项；CT 画廊没有加载表状态。
- 用户要求：补这类交互规范，后续新插件按流程检查 UI（背景色、状态效果等），并在 CT 画廊增加对应测试验证、输出报告。

## Expected Outcome

1. 点击连接后 ≤100ms 出现局部「加载表…」spinner；完成前不显示「（无表）」。
2. 同一连接加载中重复点击不重复发请求；force 重拉时旧响应不覆盖新结果。
3. 规范补强：`ui-ux-design` 走查清单、`plugin-development` §3.4、`02-spec.tpl.md`、`DESIGN.md`、`AGENTS.md` §8 均写入异步 loading/竞态/空态规则。
4. CT 画廊新增 `DbAdmin/TablesLoading` story + CT 行为断言 + 像素基线，`test:ct` 绿。
5. 输出 pilot 05-07 报告。

## Constraints

- 不改后端接口；不改连接交互语义；不引入新依赖。
- loading 用局部 spinner，不整页遮罩；颜色只用 `--el-*` token。
- 不提交 git / 不发布；不触碰用户运行实例。

## Success Criteria

- 插件构建通过；`pnpm run test:ct`（先 update 再 test）通过；CT 截图已读图。
- 新增/修改规范条目落地；pilot 00-07 齐；e2e DbAdmin 冒烟仍通过（或给出环境归因）。
