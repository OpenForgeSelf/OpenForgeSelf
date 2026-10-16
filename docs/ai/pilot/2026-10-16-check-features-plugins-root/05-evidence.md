# 05 Evidence — check-features 插件根路径清账（2026-10-16）

> 来源等级：Verified = 本会话 2026-10-16 亲自实跑；历史 Verified = 10-07 pilot 留档实跑。

## 1. 本批实跑（2026-10-16，来源等级：Verified）

```text
PS> node ForgeSelf.Web/scripts/check-features.mjs
[info] 功能清单共登记 22 项，视图信号 23 个、控制器信号 12 个、插件信号 13 个。
✅ 功能清单校验通过（22 项，代码与清单一致）。
EXIT=0
```

宿主全量单测（含 `src/__tests__/features.test.ts` 3 例）同批刷新：**85 文件 / 985 测试全绿，EXIT=0**（来源等级：Verified，见 `2026-10-16-aiagent-web-batches-landing/05-evidence.md` §1）。

## 2. 历史证据（来源等级：历史 Verified）

- 挂账出处：`docs/ai/pilot/2026-10-07-todo-agent-dispatch/05-evidence.md`（检查器对 HEAD 落后，21 项不一致、其中 16 项假红）与 06-review.md §3（挂账 4 条之一）。
- 修复由前序会话完成并经用户侧确认「此前验证通过」（本会话输入原文），改动即当前工作树 diff：`pluginsDirs` 候选根 + 递归命名文件匹配 + `Dev`/`Update` 基建排除。

## 3. 改动对表（来源等级：Verified，git diff 实读）

| 改动 | 内容 | 是否越界 |
| --- | --- | --- |
| `pluginsDir` → `pluginsDirs` | 仓库根 `Plugins/` + `ForgeSelf.Api/Plugins` 双候选 | 否，仅检查器 |
| `hasNamedFile` / `hasPluginFile` | 视图/控制器信号可命中插件 `web/src` / `Controllers` | 否 |
| `CONTROLLER_DENY` + `Dev`/`Update`；`PLUGIN_DENY` + `Dev` | 基建产物不计孤儿 | 否 |
| `features.ts` 等产品代码 | **零改动** | 符合 mini-task 禁改边界 |

## 4. AC 覆盖对照

| AC | 结论 | 证据 | 来源等级 |
| --- | --- | --- | --- |
| AC1 检查器 exit 0 | ✅ | §1 实跑输出 | Verified |
| AC2 全量 vitest 不新增红 | ✅ | §1（985/985） | Verified |
| AC3 如实记录 + TODO 清账 | ✅ | 本文 + §2 历史标注 + TODO.md 对应条目移除 | Verified |
