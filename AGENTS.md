# OpenForgeSelf — AI Agent 工作规则

## 前端代码变更后的校验流程

每次修改 `OpenForgeSelf.Frontend/` 下的代码后，**必须**按顺序执行以下校验，全部通过才算完成：

```bash
cd OpenForgeSelf.Frontend
npm run check    # vue-tsc --noEmit && eslint（类型检查 + 代码规范）
npm run test     # vitest（单元测试）
```

- `check` 失败 → 先修类型错误，再修 lint 问题
- `test` 失败 → 修测试或补测试，不允许跳过
- 两项都通过 → 变更完成

### 脚本速查

| 命令 | 作用 |
|------|------|
| `npm run check` | 类型检查 + ESLint 联合校验 |
| `npm run lint` | 仅 ESLint |
| `npm run lint:fix` | ESLint 自动修复 |
| `npm run type-check` | 仅 vue-tsc 类型检查 |
| `npm run test` | 单元测试（vitest） |
| `npm run build` | 完整构建（含类型检查） |

## 后端代码变更后的校验流程

每次修改 `OpenForgeSelf.Backend/` 下的代码后，执行：

```bash
cd OpenForgeSelf.Backend
dotnet build
```

后端测试（按需）：

```bash
cd OpenForgeSelf.Backend.Tests
dotnet test
```

## 设计稿

设计稿位于 `forgeself-design/` 目录，HTML 文件可直接在浏览器打开预览。
实现层（Vue 组件）必须对齐设计稿的视觉与交互。

<!-- OPENWIKI:START -->

## OpenWiki

This repository uses OpenWiki for recurring code documentation. Start with `openwiki/quickstart.md`, then follow its links to architecture, workflows, domain concepts, operations, integrations, testing guidance, and source maps.

The scheduled OpenWiki GitHub Actions workflow refreshes the repository wiki. Do not hand-edit generated OpenWiki pages unless explicitly asked; prefer updating source code/docs and letting OpenWiki regenerate.

<!-- OPENWIKI:END -->
