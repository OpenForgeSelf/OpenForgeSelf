# 插件验收标准（Plugin Acceptance Checklist）

> 用途：**发布/走查前**、或评审「插件是否完备可提交」时**按需加载**。
> 呼应：AGENTS.md §0 出口清单 / §10 Verification-Centric Completion / plugin-development §四 维护闭环 /
> 本技能铁律 1-18 / MEMORY 铁律。与 §四 维护闭环的差异：这里是**可勾选清单**，那边是**步骤**。
> 用法：逐项勾选并写清验证动作；**任一硬性项不满足即未达验收标准，禁止宣称完成**。

## 0. 状态分级（AGENTS §10）

- 每项验证标注来源等级：✅ **Verified**（实跑）/ ⚠️ **Inferred**（代码推断）/ ❌ **Unknown**（未验证）
- 最终状态只能取：✅ COMPLETED / ⚠️ COMPLETED_WITH_RISK / 🟡 PARTIALLY_COMPLETED / 🔴 NOT_COMPLETED / ⏸️ BLOCKED
- 禁止模糊表达：「应该没问题」「基本完成」「看起来正常」。

## 1. 流程闭环（§四 维护闭环 + AGENTS §0）

- [ ] 已读 `plugin-development` + 对应专项技能（publish-verify / e2e-testing / frontend-scaffold）
- [ ] 门禁全过：后端 `dotnet build` 0 error、插件测试全绿、前端 `pnpm run check` + `pnpm run test`（涉及前端时）
- [ ] 插件层 e2e 已跑（`e2e/plugins/<id>/`，零 mock，走 `e2e-testing` 技能）
- [ ] 已发布到运行宿主（**走发布脚本** `run-plugin-publish-verify.ps1`，禁止手工 Copy-Item）
- [ ] 浏览器走查完成：按用户视角点一遍 + 截图读图（图标/间距/颜色/留白/对齐/遮挡/溢出）
- [ ] 走查造的测试数据已清理

## 2. 代码与数据安全（铁律 9/10/11/12）

- [ ] 实体改动走 `Data/Model.xml` → `xcode Model.xml`，无字段漂移；业务写 `.Biz.cs`
- [ ] 无任何自动删除数据目录/数据库的代码（含 ProcessExit / Dispose / AppExit 挂钩）
- [ ] 测试库指向随机隔离目录，不碰真实库 / 用户目录 / 发布目录
- [ ] 插件已自行建表（`Data/<X>Tables.cs` 的 `EnsureCreated()` 在插件启动路径调用，不依赖程序集扫描）
- [ ] 唯一性校验/存在性判断直查 DB，不复用 `Meta.Cache` / `Find`

## 3. 安全与暴露面（铁律 17）

- [ ] **全部管理/CRUD/配置控制器类级 `[Authorize("ApiKeyPolicy")]`**
  - 验收动作：grep 插件 `Controllers/` 每个类的类声明；对照宿主 `Controllers/AIProviderController.cs:21`
  - 实测：无 token curl 任意管理端点应 401（例：`curl http://host:port/api/<插件管理端点>` → 401），带 token 200
- [ ] 对外服务端口（MCP/网关等）有**独立令牌**，且该令牌与宿主 API 令牌职责分离、不回显明文
- [ ] 敏感配置脱敏：GET 接口返回 token/secret 只显示掩码（如 `****尾4`）
- [ ] 密钥不落库明文（或按设计加密存储）；日志不打明文密钥

## 4. 网关/工具类插件（铁律 18，MCP/统一工具/代理类必查）

- [ ] 对外万能工具 description 包含：**入参格式示例**（`{tool, parameters}`）、**常规能力分类**（读写文件/执行命令/搜索/计算/系统监控等具体工具名）、**发现工具入口**（如 `list_tools`）、**外部工具命名空间**（如 `mcp.<服务器id>.<工具名>`）
- [ ] 提供工具枚举能力（`list_tools` 或等价），返回名称 + 说明 + 参数 schema，支持关键字过滤
- [ ] 未注册/未知工具名返回明确错误（含已注册数量或引导），不静默
- [ ] 防自引用（万能工具不可调用自身）

## 5. 界面与交互（§3.2 / §3.4 / 铁律 13）

- [ ] 根视图标题旁**版本徽标**（铁律 13，数据来自 `GET /api/plugin` 按 id 过滤）
- [ ] 破坏性/状态变更操作二次确认（`ElMessageBox.confirm` + 可单测的确认编排函数）
- [ ] 点即保存落盘 / 操作成败可见（失败留窗打印原因）/ 轮询无闪（先比较再赋值）/ 空态分级 / 筛选分页边界
- [ ] 滚动容器子区块 `flex-shrink: 0`（铁律 8）
- [ ] 前端不 `import` 宿主模块；external 声明齐全（构建后 `grep 'from "vue-router"' dist/index.js` 应有命中）
  - **按引用面判定**：只有源码真用了该依赖才查该依赖（FileTools 实测 src 不引用 `vue-router`，dist 命中 0 属正常）；改查 `vue`/`pinia`/`element-plus` 各自须有命中，且 src 里无 `from 'ForgeSelf.Web/…'`
- [ ] **已版本化的插件：发布动作必须写 `versions/<新ver>/` + `current` + 同步根清单**（`current` 存在时宿主只读版本快照，铺扁平根＝没发布；混入宿主共享 DLL＝宿主启动即崩）

## 6. 生命周期（铁律 14）

- [ ] 长连接/后台任务**自管生命周期**（构造函数自初始化 + Apply 幂等校正 + 销毁 StopAll），不依赖宿主级 HostedService
- [ ] 热重载后功能正常；入口 DLL 变更按「停 → 覆盖 → 起」冷启动，不硬热更

## 7. 文档与记录（AGENTS §7.5 / §四 5·6）

- [ ] 功能文档 `docs/02-features/<NNN>-<功能>.md` 已同步（新能力/新端点/契约变化/版本号）
- [ ] 契约变化同步 `specs/<当前 spec>/contracts/`
  - **本 worktree 无 `specs/` 目录**（实测 2026-09-29）。此时契约的真相落点 = `docs/02-features/<NNN>-*.md` 的「契约」节（例：FileTools 8 个 folders 端点表在 `036-filetools-folder-ranking.md:22-30`），不得因缺目录而跳过契约同步
- [ ] **插件目录 TODO.md 存在**并记录剩余问题/未尽事宜（新增插件必建；已建插件检查是否过时）
- [ ] 当天工作日记已记（输入原文 + 任务拆解 + 验证结果 + 下一步）
- [ ] TODO.md 待办已移除/更新（完成即移除，不留 ✅ 堆积）
- [ ] 可复用规律已写 MEMORY.md

## 8. 汇报（AGENTS §10.4）

- [ ] 按 §10.4 格式汇报：状态 / 完成内容 / 主要变更 / 验证结果（带来源等级）/ 设计决策 / 代价·收益 / 不做事决策 / 风险 / 结论
- [ ] 存在未验证场景已显式列出；存在阻塞不宣称完成

## 验收结论

- 全部勾选 → ✅ **COMPLETED**
- 有已知风险/未覆盖场景 → ⚠️ **COMPLETED_WITH_RISK**（在「风险」显式列出）
- 部分未做 → 🟡 **PARTIALLY_COMPLETED**（列出未做项与原因）
- 验证未过 / 有阻塞 → 🔴 **NOT_COMPLETED** / ⏸️ **BLOCKED**
