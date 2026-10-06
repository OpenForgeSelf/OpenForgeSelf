---
name: plugin-frontend-scaffold
description: 从一个模板插件快速搭建新的「带独立 Web 界面」的后端插件前端（spec 010 插件界面远程加载）。用于新建插件 UI、从 AIAgent 模板起步、刷新共享依赖 shim、构建插件产物并跑加载器单测。当用户说「新建插件界面」「搭插件 UI」「从模板生成插件前端」时使用。
---

# 插件前端脚手架（spec 010）

## 何时用

- 要给某个后端插件加独立 Web 界面
- 想从现有 AIAgent 插件 `web/` 模板起步，避免重复搭 Vite lib + import map external 配置

## 一键跑

```powershell
pwsh .agents/skills/plugin-frontend-scaffold/scripts/scaffold-plugin-frontend.ps1 -Plugin <PascalCase目录> [-Template AIAgent] [-Force]
```

动作：

1. 复制 `Plugins/<Template>/web/` → `Plugins/<Plugin>/web/`（排除 node_modules、dist）
2. 跑宿主 `scripts/generate-shared-shims.mjs` 刷新共享依赖 shim（宿主升级 vue/element-plus 后需要）
3. `cd Plugins/<Plugin>/web && pnpm i && pnpm run build` 产出 dist
4. 跑宿主 `pluginViewLoader` 单测（`ForgeSelf.Web` 下 `pnpm run test` 对应用例）

## 之后

- 按 `docs/05-guides/plugin-frontend-development.md` 改组件、注册 `plugin.json` 的 `frontend`
- 发布验证走 `.agents/skills/plugin-publish-verify/`

## 约束

- 新插件 `web/` 是 AIAgent 复制件，需自行改组件名 / 路由 / 样式
- 入口 DLL 改动不能热更，见 `docs/05-guides/plugin-hot-reload-limitations.md`

## 为什么不许手写 `web/`（2026-10-06 CostScope 实测，两坑全踩）

脚手架是**整目录复制模板**，模板里下列文件已经是对的。手写 `vite.config.ts` / `package.json`
就会同时踩这两个坑，**构建直接失败**：

### 坑 1：`pnpm install` 报 `ERR_PNPM_IGNORED_BUILDS`（esbuild）

```
[ERR_PNPM_IGNORED_BUILDS] Ignored build scripts: esbuild@0.25.0
[ERROR] Command failed with exit code 1: pnpm install
```

pnpm 11 默认**拦截依赖的 postinstall 脚本**，而 esbuild 的 postinstall 负责把平台二进制放进
`node_modules` ⇒ esbuild 不可用 ⇒ `vite build` 必挂；且 `pnpm install` 先退出 1，连带 `pnpm build` 一起失败。

**修法（白名单放对文件）**：

```yaml
# Plugins/<Plugin>/web/pnpm-workspace.yaml
allowBuilds:
  esbuild: true
onlyBuiltDependencies:
  - esbuild
```

⚠ **pnpm 11 不再读 `package.json` 里的 `pnpm` 字段**，日志会明确提示
`The "pnpm" field in package.json is no longer read by pnpm`。写进 `package.json` **无效**。
（仓内 DesignSystem / FileTools / McpCenter / AgentHub 的 `package.json` 里还留着那个失效字段，属噪音，不影响构建。）

### 坑 2：`Could not resolve entry module "index.html"`

非 lib 模式下 Vite 以 `index.html` 为入口，而插件**没有 HTML 入口**（界面由宿主页面挂载）。

```ts
build: {
  outDir: 'dist', emptyOutDir: true,
  cssCodeSplit: false,                        // CSS 独立产物，名字必须稳定
  lib: {
    entry: fileURLToPath(new URL('./src/index.ts', import.meta.url)),
    formats: ['es'],
    fileName: () => 'index.js',               // 宿主按此名远程加载
  },
  rollupOptions: {
    external: ['vue', 'vue-router', 'pinia', 'element-plus'],  // 漏了会内联第二份 Vue
    output: {
      entryFileNames: 'index.js',
      chunkFileNames: '[name].js',
      assetFileNames: 'style[extname]',       // ⇒ dist/style.css，宿主注入 <link>
    },
  },
}
```

### 顺带：`pnpm check` 要能跑

`vite.config.ts` 用了 `node:url`，需 `@types/node` + `tsconfig.json` 里 `"types": ["vite/client", "node"]`，
否则 `vue-tsc` 报 `TS2307`。`noUnusedLocals` 会顺手抓出组件里的死 ref（如声明后模板没用）。

### 沙箱说明（更新）

§3.2 提到的「沙箱内 `pnpm i` 被 safe-delete 拦截」在 2026-10-06 实测**未复现**（插件目录内直接
`pnpm install` + `pnpm build` 成功）。优先在插件目录内直接构建，失败再走宿主树内出树构建兜底。
