# Evidence — db-admin 并入 main（收口与验收）

> Task：`2026-11-05-db-admin`｜证据分级：**Verified**（本会话亲自跑出真实输出）/ Inferred（代码推断）/ Unknown（未验证）。
> 状态：**部分回填版（T-C 前置）**——e2e/构建/工件链证据已回填；rebase/门禁/走查证据随 ②⑥⑦ 回填。

## 0. 证据清单（实测）

| # | 证据 | 命令 / 来源 | 结果 |
|---|------|-------------|------|
| E1 | 历史重排 | git log/range-diff | ⏳ 待 ②（T-C 后 rebase） |
| E2 | run-gates -Base main | 日志正文 | ⏳ 待 ② |
| E3 | dotnet build 0 error + 插件 DLL 落宿主 bin | `dotnet build ForgeSelf.Api`（后台 job，日志 `.temp/log-build-api.txt`） | **Verified**：0 错误；DbAdmin.dll 落 `ForgeSelf.Api/bin/Debug/net10.0-windows/Plugins/DbAdmin/`（铁律12b 判据 True） |
| E4a | 插件 web build + vitest | `pnpm run build` + `pnpm test`（日志 `.temp/log-dbweb-build.txt`/`log-dbweb-test.txt`） | **Verified**：dist/index.js 68.60kB + style.css 11.08kB；vitest 9 文件 **61/61 通过** |
| E4b | 宿主前端 check/test/vue-tsc | `pnpm run check`/`test`/`npx vue-tsc -b` | ⏳ 待 ③（rebase 后对最终树跑） |
| E5 | e2e 冒烟 + 截图读图 | `pnpm exec playwright test e2e/plugins/db-admin`（日志 `.temp/log-e2e-dbadmin4.txt`） | **Verified**：**1 passed (1.0m)**；7 张截图落 `ForgeSelf.Web/screenshots/e2e/db-admin/`（01-loaded ~ 07-rejected-clean），**7 张全部读图核对通过**（详见 §2） |
| E6 | 路由一致性条目 + qa-ci 知会 | menu-route-consistency.spec.ts ④ 清单 | **Verified（条目已落）**：`['mcp-center','design-system','smart-terminal','db-admin']`（提交 fb1df53）；qa-ci 知会待 rebase 后 |
| E7 | dev-stack 走查 8 项 + 动线成本 | screenshots/dev/db-admin/ | ⏳ 待 ⑥ |
| E8 | 工件链机器校验 | pre-commit（3 次提交均触发） | **Verified**：76a61f6 / fd50fc0（amend 后 fb1df53）/ 回填提交——均 `PASS: 全部 PILOT 工件链齐全` |

### E5 迭代史（诚实记录：4 轮跑绿，3 处 spec 断言修正，零产品代码改动）

| 轮 | 结果 | 根因与修正（均为 spec 侧） |
|----|------|---------------------------|
| 1 | failed@步骤① | spec 从 `@playwright/test` 直接导入 test，绕过 `fixtures/e2e` 的 beforeEach token 注入 → 清单 401「这个页面没有加载出来」。修正：改从 `../../fixtures/e2e` 导入 |
| 2 | failed@步骤④ | `loadTables` 对已拉取连接缓存短路（DbAdminView.vue:258），批准建表后树不自动刷新。修正：改为 `page.reload()` 后展开连接（顺带真实覆盖 S-R 恢复验收点，04 截图即证据） |
| 3 | failed@步骤⑥ | 拒绝路径 UI 是**顶部 warn 横幅**（resolveApproval: banner=「已拒绝：语句未执行」），非 result-error 红条。修正：断言 banner 文案 |
| 4 | **passed (1.0m)** | 全链绿 |

### E5 后端侧交叉证据（backend.log，与 UI 断言互相印证）

- `CREATE TABLE e2e_items` 真实执行（`[DbAdminU_1] CREATE TABLE …` + write_result 审计 + history 插入 0 行 23ms）
- INSERT 审批入队（DbApproval 插入）→ 拒绝后**无任何 INSERT 执行 SQL**（拒绝=语句未执行的强证据）
- 复跑 SELECT `0 行`（e2e_rejected_row 未落库）

## 1. AC1..AC8 映射（token 逐字对应 02-spec §9）

| AC | 断言要点 | 证据 | 状态 |
|----|----------|------|------|
| AC1 | 历史重排：工件提交垫底 + 10 代码提交带 Pilot trailer + st 11 提交去重 + 内容零意外变化 | E1 | ⏳ 待 ② |
| AC2 | run-gates -Base main PASS（日志正文） | E2 | ⏳ 待 ② |
| AC3 | dotnet build 0 error + 过滤集全绿 | E3（build 半）+ ⏳（过滤集待 ③） | 部分 Verified |
| AC4 | 插件 web + 宿主前端三项绿 | E4a（插件半）+ ⏳（宿主待 ③） | 部分 Verified |
| AC5 | e2e 冒烟 1 条绿 + 截图读图 | E5 | ✅ Verified |
| AC6 | 路由一致性条目 + 知会 qa-ci | E6 | 条目 Verified；知会待 ② |
| AC7 | 走查 8 项 + 截图读图 + 清数据 + 动线成本 | E7 | ⏳ 待 ⑥ |
| AC8 | 工件链齐 + expected_files 覆盖 + 07 三节 + 报 lead | E8 | 部分 Verified（pre-commit ×3 绿；07 真值待 ⑦） |

## 2. 用户动线成本（e2e 冒烟段已可测；⑥ 走查后补全）

- 建一条可用连接：开表单→填 4 项（名称/Provider/路径）→点「测试连接」看绿条→保存，**6 次点击级**（02 截图：绿条带服务器版本 3.50.4，反馈明确）。
- 跑一条写语句并放行：编辑器输入→Ctrl+Enter/执行→审批弹窗（风险标注「中：修改数据」+SQL 全文回显）→批准→历史回显，**4 次点击级**（03/04 截图）。
- 拒绝一条写语句：审批弹窗→拒绝→顶部「已拒绝：语句未执行」横幅→复跑 SELECT 查证 0 行，**2 次点击级**（06/07 截图）。
- 刷新恢复（S-R）：整页 reload → 横幅明示恢复范围与边界（连接/草稿恢复，结果集/密码不恢复，待审批服务端重拉），**0 操作**（04/07 截图横幅在案）。
- ⏳ dev-stack 走查后补：8 项功能逐项动线 + 计数。

## 3. 已知边界与风险

- 本轮冒烟的库文件删除失败（宿主 DAL 连接占用）——按预案留 `.temp/e2e-db/`（gitignored，随 worktree 惰性清理）；连接配置已删（publish 数据根即弃，无残留状态）。
- 12 个非目标插件 dist 在新 worktree 首建遇 pnpm 共享 store esbuild 错版硬链（`Expected 0.25.0 but got 0.25.12`），已切 worktree 本地 store 重装重建中；**不阻塞本插件冒烟/走查**（DbAdmin/SmartTerminal dist 已建），全量缺失只影响 menu-route-consistency ② 在本 worktree 的本地复跑（该项由 qa-ci 在 main 侧执行）。
