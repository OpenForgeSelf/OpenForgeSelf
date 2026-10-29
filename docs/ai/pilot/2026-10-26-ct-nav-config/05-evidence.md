# Evidence

> Task ID：2026-10-26-ct-nav-config｜日期：2026-10-26｜来源分级：Verified / Inferred / Unknown

## Changed Files

| 文件 | 动作 |
| --- | --- |
| `ForgeSelf.Web/ct/stories.ts` | 新增 `NavGroupConfig` / `NavConfig` 接口与 `navConfig`（侧栏标题、分组显示名与顺序、折叠阈值） |
| `ForgeSelf.Web/ct/main.ts` | 侧栏按配置渲染；深链保留侧栏并高亮；`chrome=0` 关闭侧栏；点击同步地址栏；未知 id 错误页与侧栏共存 |
| `ForgeSelf.Web/src/components/ct-support.ts` | `openStory` 的 URL 统一追加 `chrome=0`（CT 基线不受导航影响） |
| `ForgeSelf.Web/src/components/Gallery.ct.spec.ts` | 深链用例改为「保留侧栏 + active」；新增 `chrome=0` 用例；补分组标题断言 |
| `ForgeSelf.Web/ct/README.md` | 深链语法与 `navConfig` 配置说明 |

## 验证结果

| # | 项 | 命令 / 方式 | 结果 |
| --- | --- | --- | --- |
| V1 | 类型 | `pnpm run type-check` | exit 0。首跑曾因函数 `groupStories` 与循环解构变量同名报 TS6133 / TS7022 / TS2448，重命名为 `buildNavGroups` 后通过 | Verified |
| V2 | Lint（本批） | `npx eslint ct/main.ts ct/stories.ts src/components/ct-support.ts src/components/Gallery.ct.spec.ts` | 0 error 0 warning（无输出） | Verified |
| V3 | 组件规范守卫 | `node scripts/check-component-conventions.mjs` | `PASS（story 4 个 / ct.spec 4 个，均已注册、配对、标注）` | Verified |
| V4 | CT | `CT_FRESH=1 pnpm run test:ct`（5193） | **13 passed**（10.7s），含新增「深链保留侧栏 + 高亮」「chrome=0 无侧栏」与既有 12 条基线比对。首跑直接打 5190 时因开发服务器占用端口报 `already used`，改用 `CT_FRESH=1` | Verified |
| V5 | 设计守卫 | `pnpm run design:check` | PASS（token 族 209 = 基线 209；裸色值 1761 < 基线 2501） | Verified |
| V6 | 5190 现场走查 | browser_use 打开三地址（深链 / 根/另一故事 / `chrome=0`） | 四组标题顺序与计数正确；`AIAgent/MessageToolAndText`、`ForgeCard/Default` 均金色高亮；`chrome=0` 侧栏消失、组件贴左。三地址全 PASS | Verified |
| V7 | 控制台归因 | 同上 + 拉取服务端模块 | 重启 5190（清 HMR 缓存）后功能全 PASS；`GET http://127.0.0.1:5190/main.ts` 返回 22899 B、正文无 `groupStories` 标识（只有 `buildNavGroups` / `setActiveStory`），确认报错为旧标签页历史消息而非当前代码 | Verified |

## AC 覆盖

| AC | 证据 |
| --- | --- |
| AC1 navConfig 生效 | V4（`Gallery.ct.spec.ts` 断言首组标题含 `ForgeCard · 玻璃卡片`）+ V6（四组顺序与计数） |
| AC2 深链保留侧栏 + active | V4（「深链模式保留侧栏并高亮当前故事」用例）+ V6 |
| AC3 chrome=0 无侧栏 | V4（「chrome=0 不渲染侧栏」用例：`.nav-group` count=0、`body` 无 `has-nav`）+ V6 |
| AC4 既有 12 条不回归 | V4（13 passed；4 张像素基线比对通过） |

## 环境与偏差（如实）

- CT 端口：5190 被画廊 dev server 占用时 `pnpm run test:ct` 直接失败（`webServer` 不复用既有服务）；改用 `CT_FRESH=1` 走 5193，不杀掉用户正在浏览的服务。
- 首跑类型错误属于命名冲突（函数与解构变量同名），已改 `buildNavGroups`，不是类型系统误报。
- 未跑全量 `pnpm run check` / `npx vitest run`：本批未改产品组件；全量仍受并行会话两处红灯影响（见 `2026-10-26-component-ct-gallery/05-evidence.md` V11/V12），不以全量绿作本批结论。
- 5190 旧标签页的 `groupStories2` 报错：HMR 缓存残留，重启后消失；服务端模块已实证无旧标识。
