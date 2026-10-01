# Evidence

> 阶段：Stage 7｜**只记录实际发生的事情**，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。禁止混用。
> ⛔ 禁用表述：「应该可以」「理论上通过」「看起来没问题」「大概率是」「估计可以」。

> **状态：NOT_STARTED（交接骨架）**——M2 尚未开工（前置：M1 闸门2 通过 + 用户批准 M2 闸门1，见 04-task「闸门状态」）。
> 本文件所有栏位待实现方填写；空栏 = 未做，不代表通过。规划方事后按 06-review.md 独立复验，不采信本文自述。

## Task

PILOT-ds-m2-showroom（设计插件 v3.0.0：向导 + 展厅 + 交付与接入）

## 开工复核（00 末尾清单，实现方填）

| #   | 复核项                                                                                                                                 | 命令 / 做法 | 结果 | 来源等级 |
| --- | -------------------------------------------------------------------------------------------------------------------------------------- | ----------- | ---- | -------- |
| 1   | M1 已合入且 AC 全 Verified；`presets / recommend / quick-create / agent/tools / agent-access / brief / agent-rules` 在真实宿主上可调用 |             |      |          |
| 2   | 基线：后端过滤集（总数==发现数）、web `check/test/build`、既有 `e2e/plugins/design-system`（记存量红）                                 |             |      |          |
| 3   | `preview-css` 可行性探针（用完即删）：内存 `TokenGraph` → `ToCss` 不触库；与落库导出的差异是否仅限注释                                 |             |      |          |
| 4   | `GET api/mcp-center/config` 字段名现状                                                                                                 |             |      |          |
| 5   | `git status` 清点并行会话改动，确认不与 `web/**` 重叠                                                                                  |             |      |          |

## Changed Files

<!-- 以 git status 为准，与 04-task Expected Files 逐项对账，多出的要解释 -->

-

## AC → 证据矩阵（28 行必须全填；e2e 不可行写 Unknown + 原因 + 替代证据）

| AC   | 片  | 判据（摘自 02-spec）                                                                       | 验证命令 / 用例全名 | 结果 | 来源等级 | 输出摘要（贴关键原文） |
| ---- | --- | ------------------------------------------------------------------------------------------ | ------------------- | ---- | -------- | ---------------------- |
| AC1  | A   | `preview-css`：8 预设 × light/dark 返回 CSS；前后 12 表行数不变                            |                     |      |          |                        |
| AC2  | A   | 同源：落库后导出 CSS == `preview-css`（去注释规整空白后逐字相同）                          |                     |      |          |                        |
| AC3  | A   | 确定性、`theme` 缺省 light、非法参数 400、类级鉴权                                         |                     |      |          |                        |
| AC4  | A   | 变量契约：模特用到的 `--ds-*` ⊆ 每个预设 × light/dark 定义集                               |                     |      |          |                        |
| AC5  | A   | 版本 3.0.0 三处一致；`meta.capabilities` 含 `preview-css`                                  |                     |      |          |                        |
| AC6  | A   | 四模式外壳 + 默认模式规则 + 工作台 14 入口不变                                             |                     |      |          |                        |
| AC7  | A   | 守卫：`window.prompt/alert` 为 0、`confirm` 仅白名单；反向探针                             |                     |      |          |                        |
| AC8  | A   | 向导状态机（推进/回退/必填/单飞/失败保留/陈旧丢弃）                                        |                     |      |          |                        |
| AC9  | A   | 术语词典守卫与开关持久化                                                                   |                     |      |          |                        |
| AC10 | A   | 皮肤：旧行为不变；作用域参数化；`pickVars`；`composeCss`；id 净化                          |                     |      |          |                        |
| AC11 | A   | 展厅数据层：排序/上限/过滤；并发≤3；缓存；防抖 + 序号守卫                                  |                     |      |          |                        |
| AC12 | A   | 模特零字面量守卫 + 反向探针                                                                |                     |      |          |                        |
| AC13 | A   | e2e：向导落库；画布底色 == 后端 `surface-bg`（预设与已存项目各一）；微调不落库、保存才落库 |                     |      |          |                        |
| AC14 | A   | 6 个模特页真渲染（元素数下限）、控制台无 error                                             |                     |      |          |                        |
| AC15 | B   | 五类场景注册表 + 设备框三档 + mobile 强制手机框                                            |                     |      |          |                        |
| AC16 | B   | 并排对比：双作用域互不污染、底色各自等于各自后端值、差异条值一致                           |                     |      |          |                        |
| AC17 | B   | B 片模特过 AC4/AC12                                                                        |                     |      |          |                        |
| AC18 | C   | 交付页网关地址、令牌不明文、`isRunning=false` 提示                                         |                     |      |          |                        |
| AC19 | C   | `agent-rules`/`brief` `<pre>` == REST 导出；复制回退                                       |                     |      |          |                        |
| AC20 | C   | 工具表 == `agent/tools` == `meta.agentTools`；写开关 PUT 重读/持久；收尾改回               |                     |      |          |                        |
| AC21 | C   | 试审查与 REST 一致；>200KB 客户端拦截                                                      |                     |      |          |                        |
| AC22 | C   | 深链 `parseHash/formatHash` 往返 + e2e 直达                                                |                     |      |          |                        |
| AC23 | C   | 方向键/焦点/溢出 ≤2px                                                                      |                     |      |          |                        |
| AC24 | C   | 视觉 QA：≥3 预设 × 5 场景 × 明/暗逐张读图                                                  |                     |      |          |                        |
| AC25 | A–C | web 三件全绿；无新依赖                                                                     |                     |      |          |                        |
| AC26 | A–C | 后端过滤集总数==发现数；既有 e2e 无新增红；spec diff 仅 `enterWorkbench`                   |                     |      |          |                        |
| AC27 | A–C | `Model.xml` / 宿主 / McpCenter / 14 个 section 零 diff                                     |                     |      |          |                        |
| AC28 | A–C | 文档与技能同步                                                                             |                     |      |          |                        |

## Build

Command:

```bash
```

Result: PASS / FAIL（来源等级：）

```text
```

## Unit Test

<!-- 后端过滤集（总数/发现数/失败数）+ 前端 vitest（文件数/用例数）分别记录 -->

Command:

```bash
```

Result: PASS / FAIL（来源等级：）

```text
```

## Integration Test

Result: PASS / FAIL / N/A（依据：）

## E2E

<!-- 新 spec A/B/C 三块 + 既有 e2e/plugins/design-system 回归；记录网关端口来源 -->

Result: PASS / FAIL / N/A（依据：）

## Static Analysis

<!-- pnpm run check；dotnet build warning 数 -->

Result: PASS / FAIL / N/A

## Screenshots

<!-- ForgeSelf.Web/screenshots/e2e/design-system/m2/ 下的实际文件清单；读图记录逐张写：文件名 / 核对项 / 发现 / 处理。无法读图的标 Unknown 并交规划方 -->

## 反向探针记录（自查表 #24）

| 探针        | 操作                                  | 预期                                                  | 实际（贴原文） |
| ----------- | ------------------------------------- | ----------------------------------------------------- | -------------- |
| prompt 守卫 | 临时造 `window.prompt(`               | `dialogs.test.ts` 变红                                |                |
| 字面量守卫  | 模特片段里临时放 `#fff`               | `mannequins.test.ts` 变红                             |                |
| 变量契约    | 临时让某预设 CSS 缺一个模特用到的变量 | `MannequinVariableContractTests` 变红并指出变量与出处 |                |
| 陈旧响应    | 受控 promise：旧响应后到              | 不覆盖新状态                                          |                |
| 零写库      | 试穿/微调前后 12 表行数               | 不变                                                  |                |

## 数据口径记录

| 项                                                                | 记录 |
| ----------------------------------------------------------------- | ---- |
| `preview-css` p95 耗时（开发机，n=？）                            |      |
| `pnpm run build` 产物体积（`dist/index.js` + `style.css`）前 → 后 |      |
| 衣柜首屏缩略图完成耗时（8 预设，并发 3）                          |      |
| 宿主页面容器实测可用宽度（三栏断点依据）                          |      |

## Plan 偏差汇总

<!-- 条数 + 03-plan「Plan 偏差记录」位置；无写「无」 -->

## Known Limitations

## Unresolved Issues

<!-- 失败时如实记录：FAIL + 原因 + 已尝试 1./2./3. + 最终状态 BLOCKED -->

## 阻塞

<!-- 无写「无」 -->
