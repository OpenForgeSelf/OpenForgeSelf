# mini-task：IM 网关会话视图 D1 布局比例 + D2 气泡方向修复（含 e2e 几何/语义锁）

> 日期：2026-10-31｜来源：输入9 用户对 :51888 v2.1.0 截图指认｜任务级：轻量（≤3 文件缺陷修复 + 守卫随批）
> 状态：**闸门1 获批（输入11「先更新，然后执行」）→ 已实施完毕，闸门2 待用户验收**（05/06/07 齐备）

## Intent（为什么 做什么 / 到什么程度）

用户实测截图指认两缺陷：

- **D1 布局比例失衡**：左栏联系人列表被撑宽（实测 ~800px），右栏消息线程被挤窄（~460px）——主次颠倒。
- **D2 气泡方向反**：用户拍板目标态 = **AI 回复（out）在左、用户发来（in）在右**；当前实现相反（in 左 out 右）。

完成程度：更新 :51888 后双主题走查——长预览下左栏恒 ≤360px 且省略号生效、out 左 / in 右；e2e 几何锁 + 语义锁常绿；工件 05/06 补齐。

## Spec（根因 + 改动界定）

### D1 根因（已实测坐实，发布 zip 内 CSS 验证）

- 发货 CSS：`.ig-chat__side{flex:0 0 300px;…}`（源码 ImGatewayView.vue:629-642）**缺 `min-width:0`**。
- 机制：aside 是 `.ig-chat`（flex row）的子项，默认 `min-width:auto` → automatic minimum = 自身 min-content 宽；
  条目预览 `white-space:`（Chat.vue:-118）+ 后端预览截 60 字符（02-spec FR-2）→ 60 个 CJK 字符
  ≈720px min-content 逐级上传（内层 `.ig-clist__body` 的 `min-width:0` 只约束自身 flex 上下文，不封顶 intrinsic
  贡献）→ aside 被撑到 ~788px，`main(1fr)` 挤到 ~460px。与用户截图比例吻合。
- 走查未暴露原因：种子数据览短（张三/四，min-content<300px）→ aside 恒 300px；走查做了比色没做几何压力。
- 修复：`.ig-chat__side` 加 `min-width: 0`（一行；300px 基准宽保留，长预览由内层省略链接管）。
  内层 ellipsis 链（ChatList.vue:88,112-118）已齐，不动。

### D2 改动界定

- ChatThread.vue:179-186 交换：`.ig-msg--in` → `align-self:flex-end`（右）、`.ig-msg--out` → `flex-start`（左）；
  meta（时间 / 回复 tag）随 `.ig-msg` 的 align-items 自动跟随。气泡配色不动（out 仍 gold 16% 混色底）。
- 基准变更留痕：02-spec 修订记录（方向语义=用户 2026-10-31 拍板，推翻原「收左回右」）+
  ui-design-brief §三/§五 + README 会话面板节同步。

## Interaction Design（交互设计 · 2026-10-30 规则必写）

- **方向语义（需求拍板项，2026-10-31 用户拍板）**：AI 回复（out）= **左**气泡；用户发来（in）= **右**气泡。时间/回复 tag 随气泡侧自动跟随；配色不变（out 仍 gold 16% 混色底、in 主题底）。
- **布局比例（目标态）**：侧栏固定 300px（长文本压力下实测仍 ≤360px）、主线程 `1fr` 占主导；60+ 字符预览省略号生效、零横向溢出、气泡侧不因文本长度漂移。
- **五态**：沿用 2.1.0 既有五态约定（`Plugins/ImGateway/docs/ui-design-brief.md` §四），本批不改态行为，只改方向与几何。
- **机器验收**：e2e 语义锁（out=`align-self:flex-start` 左 / in=`flex-end` 右）+ e2e 几何锁（压力数据下侧栏 ≤360px 且主栏>侧栏）双绿。

## Plan（步骤）

1. [im-dev] D1：ImGatewayView.vue `.ig-chat__side` + `min-width:0`；D2：ChatThread.vue 方向交换。
2. [im-dev] 同类扫查（problem-class-closure）：全插件 web/src 中「flex/grid 子项无 min-width 约束且内容含
   nowrap/长文本面」清单 → 命中同根因就地修；与 PC-019（flex-shrink 裁切面）合并为「flex 尺寸约束类」扩面归档。
3. [im-test] e2e 增两把锁（`e2e/plugins/im-gateway/im-gateway.spec.ts`）：
   - **几何锁**：压力数据（sqlite3 种 60+ 字符预览会话，沿走查种子先例）→ 断言 `.ig-chat__side` 实测宽 ≤360px
     且 `.ig-chat__main` 宽 > side；
   - **语义锁**：out 气泡 computed `align-self=flex-start`（左）、in=`flex-end`（右）。
4. [lead] 门禁：插件 build + e2e（含新锁，安静窗口）+ design:check + dev-stack 走查（长预览压力 + 双主题截图）。
5. [lead] 文档同步（brief/README/02-spec 修订记录）+ 05/06 补记 + 版本 2.1.0→2.1.1。

## Task（Allowed / Forbidden + 验证命令）

- **Allowed**：`Plugins/ImGateway/web/src/**`、`Plugins/ImGateway/docs/ui-design-brief.md`、
  `Plugins/ImGateway/README.md`、`e2e/plugins/im-gateway/**`、`ForgeSelf.Api.Tests/Plugins/ImGatewayTests/**`、
  本 pilot、TODO/日记、`./skills/ui-ux-design/SKILL.md`（走查清单回填）。
- **Forbidden**：宿主 `ForgeSelf.Api/**`、`ForgeSelf.Web/src/**`、其他插件业务文件（同类扫查只读+登记，跨插件
  修复另走小批）、:51888 宿主进程（只读走查除外）、用户浏览器标签。
- **验证**：`cd Plugins/ImGateway/web && pnpm run build`（exit 0）；
  `pnpm exec playwright test e2e/plugins/im-gateway`（安静窗口，含新锁全绿）；
  `pnpm run design:check`（PASS，棘轮零增量）；走查截图双主题落
  `ForgeSelf.Web/screenshots/dev-stack/2026-10-31-imgateway-layout-direction-fix/`。

## 闸门

- **闸门1**：本文件经用户批准后开工。
- **闸门2**：05/06 + 双主题走查截图 → 用户验收。
- **闸门3**：用户验收后 git 提交（含 2.1.1）。

## 关联决策待拍板（随批呈报）

1. **DESIGN.md 增两条最小规范**（真源属用户权威）：①双栏列表页——侧栏固定宽 + `min-width:0` 铁律 + 主内容
   `1fr` 占主导；②会话类视图的气泡方向语义 = 需求拍板项，spec 必须显式写明并经闸门1 确认。
2. **verify-pilot-artifacts `-EnforceInteraction`**（02-spec 交互设计节机器校验）已在 TODO（L4 宪法层，须
   授权），本批携带动。

## 收口记录（2026-10-31）

- [x] D1 `min-width:0` + D2 方向对调 + 版本 2.1.1（im-dev；lead 源码实核）
- [x] 同类扫查：ImGateway 内 0 命中；跨插件 4 组低风险登记（→ TODO 候选小批）
- [x] e2e 几何锁 + 语义锁双绿（4/4；vue-tsc 0 错）
- [x] 走查：隔离实例压力种子，几何 6/6 达标 + 亮/暗截图（05 §E3）
- [x] 规范沉淀（输入11 授权）：DESIGN.md §7/§8 + B3 双面铁律 + 清单 +3 + `-EnforceInteraction` 三处接线
- [x] PC-019 扩面归档（双面不变量 + case + 双索引）+ 机器校验 OK（19 PC）/ Registry 5/5
- [x] 中档门禁：全量 3273/80（本批归因 2 红=BOM 已修复闭环，28/28 复跑绿；78 红分桶待正常 bin 终判）
- [ ] 闸门2：用户验收（本行）
- [ ] 闸门3：验收后 git 提交
