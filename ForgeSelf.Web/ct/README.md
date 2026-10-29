# 组件画廊（CT Gallery）

> 规则真源：`docs/04-standards/frontend-component-development.md`｜操作 SOP：`.agents/skills/component-visual-loop/SKILL.md`
> 本文件只做「快速上手」，不重复规范细节。

## 三条命令

```bash
cd ForgeSelf.Web
pnpm run ct:server        # 打开 http://127.0.0.1:5190/ 左侧侧栏点击即预览（改 .vue 即 HMR）
pnpm run test:ct          # 与基线做像素对比（防回归）
pnpm run test:ct:update   # 重写像素基线（视觉有意的变更才用）
```

## 深链语法

```text
/?story=ForgeCard/Default                     # 人类浏览：保留侧栏并高亮当前故事
/?story=AIAgent/MessageToolAndText&chrome=0   # CT 截图：关闭侧栏与左侧留白，基线不受导航干扰
```

未知 id 会列出全部可用 story（不白屏），侧栏仍然保留。

## 侧边栏导航配置

唯一配置点 = `ct/stories.ts` 的 `navConfig`：

- `title`：侧栏标题；
- `groups`：分组显示名与顺序（`key` 必须等于 story id 的 `/` 前缀，如 `AIAgent`）；未列出的分组按前缀自动追加在末尾；
- `collapseWhenMoreThan`：故事总数超过该值时分组默认收起。

## 加一个组件的故事

1. 组件旁新建 `<组件名>.story.ts`，导出 `stories`（`{ id, component, props?, slots? }`）；
2. 每个变体上方写 `// 测试状态：…`；
3. 在 `ct/stories.ts` 追加 import 与展开项（守卫会校验）；
4. 需要像素回归再加 `<组件名>.ct.spec.ts`（先断言、后 `toHaveScreenshot()`）。

## 已知边界

- 只提供 Pinia，不提供 vue-router：依赖路由的组件请改用 props 注入或暂不接入；
- 跨包组件（插件前端）由 `src/components` 下的薄宿主壳承接，插件目录不新增测试依赖；
- 像素基线 `*-snapshots/` 是本机 Chrome 渲染产物，不入库（`scripts/check-component-conventions.mjs` + 根 `.gitignore` 已配套）。
