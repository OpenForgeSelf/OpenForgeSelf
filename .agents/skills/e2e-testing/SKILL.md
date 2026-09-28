---
name: e2e-testing
description: OpenForgeSelf 端到端测试统一技能（应用层 + 插件层归口同一套 e2e）。单 Playwright 配置 + globalSetup 自动构建宿主、起 publish 宿主（真实后端 7102）、起前端 dev server（7002）、解密真实 API 密钥注入浏览器；所有用例零 mock 走真实前后端。含每个插件一个 e2e + 截图读图常规化视觉检查（图标/间距/颜色/留白/对齐/遮挡/溢出）。当用户要求「跑 e2e」「给插件写 e2e」「验证页面」「截图视觉检查」「统一/整合 e2e 测试」时使用。
version: 1.0.0
---

# 端到端测试统一 SOP（e2e-testing）

> 本项目 e2e 是**唯一**的前后端集成测试手段（前端无独立单测体系），故所有 e2e 归口本技能：
> **单一 Playwright 配置 + 单一 globalSetup**，应用层用例（首页/聊天/待办/插件商店…既有 28 个）
> 与插件层用例（每插件一个目录）共用同一真实环境，不再另设独立技能/配置。

## 何时用

- 全量/单功能回归：应用层功能（首页、聊天、待办、插件商店、API 网关…）
- 给某个插件补 e2e：验证其远程界面真实渲染 + 视觉质量
- 截图读图常规化视觉检查（图标/间距/颜色/留白/对齐/遮挡/溢出）

## 快速开始（Level 1）

```bash
cd ForgeSelf.Web

# 全量（应用层 + 插件层，整体回归）
pnpm exec playwright test --config=playwright.config.ts

# 仅某插件
pnpm exec playwright test --config=playwright.config.ts e2e/plugins/sems

# 仅某应用层用例
pnpm exec playwright test --config=playwright.config.ts e2e/app.spec.ts
```

环境全自动：globalSetup 负责构建宿主、起 publish 宿主、起前端 dev、解密真实 token；teardown 只杀本次拉起进程。

## 单一环境（globalSetup 做了什么）（Level 2）

1. **构建宿主**：`dotnet publish` 到 `.temp/e2e/<ts>/publish/`（独立目录，避开运行中宿主对 DLL 的独占锁）；
2. **起 publish 宿主**：`ForgeSelf.exe --console`，端口取 `ForgeSetting.config` 的 `PortNumber`（默认 `7102`；`51888` 是历史手动冷启验收端口，非默认，勿硬编码），**必须带实例标识** `FORGESelf_INSTANCE_ID=<id>`（env）或 `--instance-id=<id>`（CLI）——宿主有硬编码全局 Mutex 单例锁，缺省会与开发实例（如用户手动起的 `ForgeSelf.exe`）互斥而「另一个实例已在运行」拒启动；e2e 用独立 id（如 `e2e-<ts>`）即可并存。数据目录经 `ASPNETCORE_ENVIRONMENT=Development` 落到 `publish/Data`（全新、隔离用户 `~/.forgeself`），轮询 `GET /api/health` 直到 200；
3. **起前端 dev server**（webServer `pnpm run dev`，`7002`）—— 既有 app spec 走此；vite 代理把 `/api`、`/plugins`、`/plugin-view` 转发到 7102 宿主；
4. **初始化真实 API 密钥**：宿主启动即幂等生成并加密存储 `ApiToken`；globalSetup 调 `GET /api/api-server/init-token`（首启无鉴权）拿明文 → 注入 worker 环境 `E2E_API_TOKEN`；`e2e/helpers/real-auth.ts` 的 `getRealApiKey` 优先用 `E2E_API_TOKEN`，回退 AES 解密 `ForgeSetting.config`（路径由 globalSetup 经 `FORGE_SETTING_CONFIG` 指向临时数据目录）；fixture 自动 `injectRealApiKey` 注入 `localStorage['forge_api_token']`；
5. 写出 `.temp/e2e/<ts>/state.json`：`{ baseURL, apiBaseUrl, apiKey }`；
6. **teardown**：只杀本次拉起的宿主/前端进程，不误杀用户手动实例。

> 现状（已落地）：`playwright.config.ts` 已接入 `e2e/global-setup.ts`/`e2e/global-teardown.ts`；每次运行把宿主 `dotnet publish` 到 `.temp/e2e/<ts>/publish/`，并以 `ASPNETCORE_ENVIRONMENT=Development` 起宿主（数据根 = `.temp/e2e/<ts>/publish/Data`，全新且隔离用户 `~/.forgeself`），应用层与插件层用例共用同一环境。

## 目录与用例布局（Level 2）

```
ForgeSelf.Web/e2e/
  helpers/real-auth.ts        # 真实 token 解密 + 注入（已存在，全局共享）
  global-setup.ts             # 构建+起宿主+起前端+初始化 token → state.json（已实现）
  global-teardown.ts          # 只杀本次拉起进程（已实现）
  fixtures/e2e.ts             # 自动注入真实 token 的插件层 fixture（已实现）
  *.spec.ts                   # 应用层 e2e（既有 28 个，保持不动）
  plugins/
    <插件id>/<id>.spec.ts     # 插件层 e2e（每插件一个目录）
```

- 应用层用例直接 `import { test, expect } from '@playwright/test'`（baseURL 7002 已就绪），零 mock。
- 插件层用例 `import { test, expect } from '../fixtures/e2e'`，认证 `injectRealApiKey(page)`，路由走 `plugin.json` 的 `frontend.route`（sems=`/sems`）经宿主 `/plugin-view/<id>` 命名空间注册。

## 插件 e2e 写法（Level 2）

1. 建 `e2e/plugins/<id>/<id>.spec.ts`，从 `../fixtures/e2e` 导入。
2. 认证：`injectRealApiKey(page)`（真实 token，不 mock）。
3. 真实后端数据驱动，断言真实渲染（不 mock 业务接口）。
4. 截图 + 读图视觉检查（见下）。

## 新增一个插件 e2e（Level 2 细化）

同上「插件 e2e 写法」四步；一个插件一个目录，不堆进应用层 spec。

## 跑多深：三档门禁（2026-09-28 定，与 AGENTS §5.6 同源）

**不要每次无脑全量**（全量一轮 ≈38 分钟），也**不要拿定向结果报「门禁绿」**。按改动面选档：

| 档 | 命令 | 何时 |
|----|------|------|
| **快** | `node node_modules/@playwright/test/cli.js test --config=playwright.config.ts e2e/plugins/<id> --output=../.pw-out-<id> --reporter=list` | 每次改完本插件/本页面的代码 |
| **中** | 同上 + 目标应用层入口（如 `e2e/menu-route-consistency.spec.ts e2e/plugin-remote-view.spec.ts`） | 改了 `plugin.json.frontend`、路由注册、菜单声明 |
| **深** | 全量单配置整跑（不带文件参数） | 改了 `e2e/global-setup.ts` / `playwright.*.config.ts` / `e2e/fixtures/**`（全体插件共用）、发版/tag 前、或专跑基线治理批次 |

- **深档跑完先对基线再判责**：本仓全量并非全绿（2026-09-28 实测 102 passed / 82 failed），四类既有红 = 陈旧断言（`chat.spec.ts` 28 等，签名 `.input-textarea`/`.menu-button`/`.hero-section`）、需真实 LLM 通道、依赖 :51888 实跑宿主却跑在隔离配置下（`quick-links` 那批）、4 worker 并发超时。**新增的红才是自己引入的**；非自己的也要贴真实报错 + 归属 + 记 TODO。
- 汇报必须写明**跑的是哪一档、覆盖哪些文件**；跨切面改动停在「快」就报绿 = 流程违规。
- 依赖 :51888 的 spec 与默认隔离配置天然冲突（会 `积极拒绝`）：这类要么显式走 `playwright.live.config.ts`，要么改造成隔离实例可跑。

## 运行态宿主（51888）手工走查：token 注入标准入口（2026-09-24 内置）

e2e 全自动场景已由 globalSetup 覆盖（自动解密 + `injectRealApiKey`）。**运行态宿主手工走查**
（如验证用户正在用的 :51888 实例）需 token 时，一律用仓内工具，**禁止每次现写解密探针**：

```bash
# 1) 拿当前宿主明文 token（读 ~/.forgeself/Config/ForgeSetting.config，支持 --config 覆盖）
node scripts/get-forge-token.cjs

# 2) 注入浏览器 localStorage['forge_api_token'] = <上一步输出> 后导航（bu / 任意浏览器通道）
```

- 注意：宿主每次启动轮换 `ApiToken`、`init-token` 一次性 —— 走查前确认目标宿主为当前运行实例；
  宿主重启后须重新 `get-forge-token.cjs`（旧 token 立即失效）。
- DLL 字符串验证（发布产物是否含/不含某实现）用 `node scripts/probe-dll-string.cjs <dll> <str> [--expect-absent]`。
- 发现缺正规入口的场景 → 先补工具/用例再走（AGENTS §5.0：不写一次性 temp 脚本当验证手段）。

## 交互确认类用例（危险操作 / 可见状态突变，Level 2 必做）

凡「点一下立即生效、且用户看得见状态变化」的按钮（归档 / 删除 / 发布 / 批量改 / 取消运行…），
**先确认该动作是否该有二次确认**，再按两条路径各写一条断言，缺一不可：

| 路径 | 必须断言 |
|------|----------|
| 取消 | 弹窗可见（`expect(box).toBeVisible()`）→ 点取消 → 弹窗消失 + **列表/DOM 未变** + **后端状态未变**（用 `request.get` 复核，不能只看 UI） |
| 确认 | 点确认 → 状态按预期变更（同样用 `request` 复核后端真实状态，而非只信 UI 消失） |

- **只测确认路径会漏掉「确认根本没接上、点一下就执行」这类回归**——AIAgent 归档按钮 2026-09-22 即此（用户实测反馈）。
- 弹窗定位：Element Plus → `.el-message-box`；按钮在弹窗内 scope 后 `getByRole('button', { name })`，避免命中页面同名按钮。
- 文案也要断言一条关键语义（如软标记动作必含「消息会完整保留」），防止将来被简化成「确定吗？」而丢掉风险说明。
- **真实宿主走查（Level 3）遇到不可逆 / 无 UI 回退路径的动作，只点「取消」，不要点确认**，并在汇报里写明「确认路径已由 e2e 在隔离宿主验证」——不要在用户真实数据上执行不可回退操作。

## 截图读图常规化视觉检查（Level 3，每插件必做，应用层可选）

> 截图产物固定：`ForgeSelf.Web/screenshots/e2e/<插件id>/<场景>.png`。截图后**必须读取图片**逐项核对，不限于以下清单：

| 检查项 | 检查内容 | 判定 |
|--------|----------|------|
| 图标 | 菜单/卡片/按钮图标是否渲染（无破图/占位方框/emoji 乱码） | 图标类是否出现且非空 |
| 间距 | 卡片/列表项间距均匀，无挤压/重叠 | 目测 + 必要时 getBoundingClientRect 断言 gap |
| 颜色 | 主题色/文字色/背景色对齐 `--el-*` tokens，无纯白闪屏 | 计算样式对比 |
| 留白 | 页面四周/标题下合理留白，无贴边 | 目测 |
| 对齐 | 标题/按钮/文本左右对齐一致 | 目测 |
| 遮挡 | 弹窗/浮层不遮挡关键内容，元素不被裁剪 | 目测 |
| 溢出 | 长文本不溢出容器/不出现横向滚动条 | 目测 + scrollWidth>clientWidth 断言 |
| 响应 | 窄屏下布局不崩溃（可选） | resize 后截图 |
| **数值自洽** | 界面显示的字节/占比/合计彼此对得上（**光看 DOM 断言过不代表数对**） | 批次C 实测：e2e 全绿时截图里是「总占用 29B / 行 160KB / 占比 564965%」 |
| **版本徽标** | 插件自带页面标题旁有 `v<X.Y.Z>`（铁律13） | `.ft-version` 类徽标文本非空且匹配 `/^v\d+\.\d+\.\d+$/` |

纯视觉问题（无法用 DOM 断言）用截图对比替代并记录原因（AGENTS §6.2）。

## 三条硬规矩（批次C 五跑 e2e 换来，2026-09-28）

1. **跑完必须读截图，且要为「中间态」写断言。** 只断言终态或只看 DOM 存在性会系统性放过时序缺陷：
   进度/部分结果类功能要在 `Running`/`Cancelled` 态回读接口原值，断言不变式（如 `Σ行 + 其他 + 本级 == 总量`、
   `占比 ≤ 100`）。单测侧同理 —— 把**每一个**轮询到的中间视图收下来断言，比只断言终态强，且不 flaky。
2. **大树夹具用「固定路径 + 完成哨兵」复用，不要每次 UUID 新建。** 实测同一份 12000 文件树首建耗时
   40s ↔ 150s 波动，会把用例超时吃光（三跑就是这么红的）。小样本（几百文件）秒扫完，
   取消/进度类用例的取消键已置灰 → **小样本会掩盖时序缺陷**，必须有一棵"扫得慢"的树。
   铁律10（只建不删）本就允许复用，`%TEMP%/ForgeSelfE2E_<插件>_big/.fixture.json` 作完成哨兵。
3. **管道 / 后台通知的 `exit 0` 不是证据。** 首跑后台通知写 exit 0，落盘日志真实是 6 failed。
   判定成败只读 `--output` 之外的**落盘日志摘要**（`X passed` / `已通过! - 失败: 0，通过: N`）。

选择器口径两处高频坑：自绘 tablist 的页签是 `role="tab"`（不是 `button`）；
弹窗按钮名要用 `exact: true`（`name:'取消'` 会子串命中确认键「取消扫描」→ strict mode 撞 2 个）。
同名 class 跨面板复用（如 `.rank-table` 同时是排行表和快照表）时，断言必须限定父容器（`.folders-panel > .rank-table`）。

## 鉴权改造后必查前端 fetch 调用方（2026-09-24 教训）

后端控制器加 `[Authorize("ApiKeyPolicy")]` 后，**前端所有裸 `fetch()` 都会 401**（管理页/远程视图空态，
曾把插件管理页空态误判为「浏览器自动化环境问题」）。改造鉴权时必须：
1. `grep "await fetch(" ForgeSelf.Web/src/services` 列出全部裸 fetch；
2. 对应已鉴权控制器的 service 一律改走 `src/services/authFetch.ts`（自动带 Bearer token，与 request.ts 同键；
   **跨域/第三方 URL 禁用**）；
3. 用 `e2e/plugin-store.spec.ts`（插件管理页鉴权回归守卫：渲染非空态）回归验证；
4. 重发前端：改前端后必须 `build.ps1` 全量重建 wwwroot 再起宿主走查（dev 修复 ≠ 发布态生效）。

另：Playwright 的 globalSetup 与 worker 是独立进程，globalSetup 写的 `process.env.*` 不会传到 worker ——
token 兜底走 `real-auth.ts#readLatestStateToken()`（读 state.json，跨进程真源）。
## 失败排查（Level 4）

- `宿主构建失败` → 看 globalSetup 报错；多为编译错误需 `dotnet restore`。
- `等待宿主启动超时` → 看 `.temp/e2e/<ts>/backend.log`（DB 连不上、插件加载崩多在此暴露）。
- `401 鉴权失败` → token 唯一真源是 `ForgeSetting.config` 的 `ApiToken`（AES 解密），非 appsettings 的 `ApiKey`（易错点，见 MEMORY）。
- `插件页面 404` → 确认 `plugin.json` 的 `frontend.route` 与宿主 `/plugin-view/<id>` 命名空间一致；宿主须加载新鲜产物（陈旧快照会 404，见 MEMORY 插件快照部署铁律）。
- `500 IPlugin 类型分裂` → 插件快照混入宿主共享 DLL（`ForgeSelf.*`/`NewLife.*`/`XCode.dll`）→ ALC 加载第二份类型 → `Apply` 永不执行。清理快照重发（见 `plugin-publish-verify`）。
- 用例失败但环境正常 → `pnpm exec playwright test --config=playwright.config.ts e2e/plugins/<id> --headed --debug` 复现。

## 代码事实速查（不凭记忆，改机制前先 grep 复核）

- **后端端口**：默认 `7102`（`ForgeSetting.Current.PortNumber`，`ForgeSetting.config` 可改）；`51888` 是历史手动冷启验收端口，非默认，勿硬编码。
- **前端 dev 端口**：`7002`（Vite，`ForgeSelf.Web/vite`）。
- **认证**：`localStorage['forge_api_token']` = 宿主 `ApiToken`（AES-256-CBC 解密）；globalSetup 经 `init-token` 拿明文注入 `E2E_API_TOKEN`，`e2e/helpers/real-auth.ts`（`getRealApiKey` 优先 `E2E_API_TOKEN`/`injectRealApiKey`）注入浏览器。
- **插件前端**：`plugin.json` 的 `frontend.{route,menu,entry,icon}`；宿主经 `/plugin-view/<id>` 命名空间注册（id=kebab-case）。**插件路由取 `frontend.route` 直接注册**（如 sems=`/sems`），仅当与宿主静态路由冲突才回退 `/plugin-view/<id>`；e2e 导航应读 `plugin.json.frontend.route`，勿硬编码 `/plugin-view` 命名空间。
- **宿主实例标识**：`Program.cs` 硬编码全局 Mutex 单例锁；多实例并存需 `FORGESelf_INSTANCE_ID`（env）或 `--instance-id=`（CLI）覆盖 Mutex 名，否则「另一个实例已在运行」拒启动（e2e 用独立 id 与开发实例并存）。
- **宿主必须从 publish 运行**（Production → 数据根 `~/.forgeself`；dev → `程序目录/Data`，两者不互通）。
- **既有 e2e 基线**：`ForgeSelf.Web/e2e/*.spec.ts`（28 个，dev server 7002 + 手动/预发布后端），`playwright.config.ts`（webServer `pnpm run dev`，baseURL 7002）。本技能将其与插件 e2e 统一到同一 config + globalSetup。

## 机制备忘（改机制前先读）

- `e2e/fixtures/e2e.ts` —— 从 `state.json` 解析 baseURL/apiKey
- `e2e/global-setup.ts` —— 构建目录 / 起宿主 / 解密 token / state.json
- `e2e/global-teardown.ts` —— 只杀本次拉起进程
- `playwright.config.ts` —— 单一配置；`globalSetup` 开关（`E2E_SKIP_GLOBAL_SETUP=1` 走手动模式）

## 与现有技能的关系

- 发布/热更新验证 → `plugin-publish-verify`（先发对，再测对）
- 新建插件前端 → `plugin-frontend-scaffold`
- 架构/能力接缝设计 → `architecture-design`
- 本技能聚焦：**应用层 + 插件层 e2e 统一归口**，含截图读图视觉检查。
- （历史）曾单列 `plugin-e2e-testing` 技能，已并入本技能，避免多套 e2e 流程分裂。
