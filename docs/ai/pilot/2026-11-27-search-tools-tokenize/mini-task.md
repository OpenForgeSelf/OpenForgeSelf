# mini-task：2026-11-27-search-tools-tokenize（轻量工件链，Intent/Spec/Plan/Task 合并）

## Intent
多关键词搜索 0 命中（卡 t_ea1b505f 需求）：`database sql`、中文词组、全角空格分隔等组合查询在 public tools 检索中返回空，用户只能单关键词精确命中。修复后组合关键词按分词逐面匹配。

## Spec（含边界）
- SearchToolsToolFunction：关键词查询先分词（空格/全角空格一致处理），逐词对工具 Id/Name/Description 三面匹配，matchedIn 标注命中面；单关键词行为保持不变。
- UniversalTool / UniversalToolForwarder：透传链路同步适配分词结果结构。
- 守卫语不回归：无意义串（如 `zzz_no_hit_xxx`）必须仍 0 命中；工具总数与注册表不因分词改变。
- 交互：无界面交互（纯后端服务/测试改动）。

## Plan
1. 实现卡 t_20bc15cf（impl，worktree wt/t_20bc15cf，4 文件 +204/-82）。
2. review 卡 t_ca02a5ed（diff 审查）。
3. qa 卡 t_b0a5e1fe（单测传感器 + live 多实例探针双关）。
4. 本卡（tl）：用户批准提交后 commit + FF merge + 打包。

## Allowed / Forbidden
- Allowed：上列 4 文件；Api.Tests 对应用例。
- Forbidden：未经卡批准改其他插件；触碰 live 18890 进程（宿主更新由用户执行）。

## AC
- AC1：`database sql` 多关键词查询命中数 0→6（含 matchedIn 面）。
- AC2：中文/全角空格分词行为与单关键词一致（total=3 / total=6）。
- AC3：守卫语 `zzz_no_hit_xxx` 仍 0 命中；`database` 单关键词 total=4 保持。
- AC4：Api.Tests 过滤 SearchTools|UniversalTool 全绿（54/54）。
