# 05 Evidence：IM 网关会话视图方向/几何修复批（2026-10-31）

> 证据分级：**Verified**（亲自跑过拿到真实输出）/ Inferred（凭代码或日志推断）/ Unknown（未验证）。所有日志落 `.temp/tmp/`（会话产物，不入库），关键数值摘录于此。

## E1 实现（im-dev，Verified）

| 项 | 证据 |
|---|---|
| D1 | `Plugins/ImGateway/web/src/ImGatewayView.vue:629-634`：`.ig-chat__side{flex:0 0 300px; min-width:0; …}` + B3 双面铁律注释（lead 源码实核通过） |
| D2 | `Plugins/ImGateway/web/src/components/ChatThread.vue:179-188`：`.ig-msg--in{align-self:flex-end; align-items:flex-end}`（右）/ `.ig-msg--out{align-self:flex-start; align-items:flex-start}`（左）+ 拍板留痕注释（lead 源码实核通过） |
| 版本 | `Plugins/ImGateway/plugin.json` Version 2.1.0 → **2.1.1** |
| 插件门禁① | `cd Plugins/ImGateway/web && pnpm run build` exit 0（vite 12 modules，dist/index.js 24.22kB / style.css 12.99kB；日志 im-dev-build-web-211.log） |
| 设计门禁 | `cd ForgeSelf.Web && pnpm run design:check` exit 0 PASS：style-tokens 1759（基线 2501，零新增）；compliance a=209/b=0/c=0/d=0（棘轮零增量；日志 im-dev-design-check-211.log） |
| 发货特征 | publish dist style.css 三特征 grep 全命中：`.ig-chat__side{…min-width:0}` / `.ig-msg--out{…align-self:flex-start}` / `.ig-msg--in{…align-self:flex-end}` |

## E2 e2e 双锁（im-test run5，12:52，57.3s，Verified）

- **4 passed / 0 failed**（`e2e/plugins/im-gateway/im-gateway.spec.ts`，真实 e2e publish 宿主；日志 im-test-e2e-locks-run5.log）：
  1. 配置页主链路（既有）✅
  2. 会话视图主链路（既有）✅——徽标 **v2.1.1** 对真宿主验证
  3. **几何锁（新）**✅：种子 120 字中文消息 → `.ig-chat__side` width ≤360 实过 + `.ig-chat__main` > side + 预览 ellipsis/nowrap + 截 60 契约 + scrollWidth>clientWidth
  4. **语义锁（新）**✅：`.ig-msg--out` align-self=flex-start（左）/ `.ig-msg--in`=flex-end（右）+ out 气泡中心 x < in 中心 x
- 截图：`ForgeSelf.Web/screenshots/e2e/im-gateway/im-gateway-geometry-lock.png` / `im-gateway-direction-lock.png`（读图：out 在左带「回复」tag、in 在右、侧栏 ~300px、120 字预览省略截断）
- 过程 4 轮红均为环境/测试侧（testhost 占 7102 / sqlite3 .read 参数 / XCode 惰性建表 / clean 先于建表），非被测代码缺陷；run1 红不构成代码判据（页面打到 testhost 旧注册表 2.1.0）
- `vue-tsc -b` 两检（12:35/12:53）均 0 错；种子哨兵 `wecom:e2e-` 前缀双清，跑后实查残留 0 行

## E3 起源码走查（插件门禁③，lead 亲自，Verified）

- **实例**：复用 e2e globalSetup 全新 publish 产物（`.temp/e2e/wt-b26d4625/publish`，含 12:52 构建的插件 dist），按 B2「锁下现场验收等效姿势」自起隔离实例：`FORGESELF_PORT=7321` + `FORGESELF_DATA_ROOT=.temp/tmp/walkthrough-20261031-data` + 独立 `FORGESELF_INSTANCE_ID` + `--console` 首参；**未触碰** :51888、并行会话 dev-stack（:7301/:7399）与其浏览器标签。令牌经官方 helper `scripts/get-forge-token.cjs` 解密注入。走查后只停自己拉的 PID（72052），种子残留实查 **0/0**。
- **压力种子**：1 会话（`wecom:lead-walk-1`）+ 2 消息（1 in + 1 out），末条 out 70+ 汉字（建表 DDL 与 XCode 实库逐字一致 + `CREATE TABLE IF NOT EXISTS` 兜底）。
- **几何数值（getBoundingClientRect，60+ 字符压力下）**：

| 指标 | 目标态 | 实测 | 判定 |
|---|---|---|---|
| 侧栏宽度 | 恒 ≤360px（基准 300） | **300.0px** | ✅ |
| 主栏宽度 | > 侧栏 | **658.0px**（334→992） | ✅ |
| 横向滚动 | 无 | `scrollWidth == clientWidth` | ✅ |
| 预览省略 | 生效 | overflowX=hidden + nowrap + clipped（sw 647 → cw 211） | ✅ |
| 方向（out） | 左 | align-self=flex-start，中心 x=594.3 | ✅ |
| 方向（in） | 右 | align-self=flex-end，中心 x=865.2（out cx < in cx） | ✅ |

- **双主题截图**（`ForgeSelf.Web/screenshots/dev-stack/2026-10-31-imgateway-layout-direction-fix/`，读图核对）：`walkthrough-light.png` / `walkthrough-dark.png`——v2.1.1 徽标、侧栏 ~300px + 省略预览、回复=左（金 tint + 回复 tag）、来讯=右，暗色 token 渲染正常（注：宿主暗态以 `html.dark` 类切换，与应用暗态同一套样式表）。
- **清单符合性**（ui-ux-design 走查清单新增项逐条）：双栏比例实测留痕 ✅（本表）；长文本压力 ✅（70+ 字）；方向语义 ✅（Spec 显式 + 闸门1 拍板）。五态：加载/空态（「从左侧选择一个联系人」）在本轮走查自然覆盖，错误气泡态由既有 e2e 用例覆盖（本批未改态行为）。

## E4 中档门禁（scripts/** 触发 §5.6 中档，Verified）

- 首跑假红：MSB3027——并行会话 dev-stack 宿主（PID 140436）锁 `bin/Debug` Core/Abstractions DLL；**不碰他人进程**，按 B4 走 `-p:OutDir` 旁路重跑（r2）。
- **r2 全量：3273 通过 / 80 失败 / 3353 总**（11m35s，日志 norms-fulltest-20261031-r2.log）：
  - **归本批 2 红（已修复闭环）**：`RepositoryScriptTests.ScriptsWithNonAscii_MustHaveUtf8Bom` ×2——**lead 的 edit 把两份 .ps1 的 UTF-8 BOM 剥掉**（HEAD=EF BB BF → worktree=23 21 2F；先前冒烟误判「shebang 无 BOM 原状」未对照 HEAD）。字节级恢复（B4 方法）后过滤复跑 **RepositoryScriptTests 28/28 绿**。
  - 其余 78 红初步分桶（Inferred，待正常 bin 位置全量复跑终判，已登记 TODO）：ImGateway 域 18 红=`no such table: ImGatewayMessage`（PC-014 类数据根/首绑在 OutDir 下的环境红；本批零后端源码改动）；基线桶（ProjectWorkspaceDefaultRoot 15 / ChatControllerIntegration 16 / ChatControllerInvariant 4 / RealLLM 4 等）。
- 问题类机器校验：`check-problem-classes.ps1` **OK（19 PC）**；`ProblemClassRegistryTests` **5/5 绿**（修复 cases/README 索引缺行——含 PILOT-077 批漏登的 aiagent case 行）。

## E5 工件门禁自检（Verified）

`verify-pilot-artifacts.ps1 -TaskId 2026-10-31-imgateway-layout-direction-fix -EnforceHeader -EnforceAcCoverage -EnforceInteraction`：本目录 05/06/07 齐备后复跑（见 07 附录）。

## Unknown / 未验证项

- 正常 bin 位置（无 OutDir 旁路）的全量终判——待并行会话 dev-stack 锁释放（TODO 登记）。
- :51888 运行实例的只读复验（步骤⑤）——须用户更新 :51888 到含 2.1.1 插件的版本后进行。
