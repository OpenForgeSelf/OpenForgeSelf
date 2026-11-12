# Mini-Task — 2026-11-12-clockbadge-plugin-intake

## 1. 为什么（Intent）

用户 2026-11-12 指令「继续，分批提交剩下的工件以及对应的修改」：ClockBadge 插件为 PILOT-079 演示期（:51888 插件开发演示）产出的规范最小插件，源码 14 文件滞留工作区未入库，存在丢失风险；且 slnx/csproj 的注册改动与其强绑定，须同批落地。本任务=**入库归档 + 工程注册**，非插件交付批。

## 2. 做什么（Spec）

- ClockBadge 源码 14 文件入库：`Plugins/ClockBadge/**`（csproj / plugin.json / ClockBadgePlugin.cs / Controllers/ClockBadgeController.cs / web/ 7 文件含 pnpm-lock 与 .gitignore）。
- 工程注册同批：`ForgeSelf.slnx`（+1 行项目项）+ `ForgeSelf.Api/ForgeSelf.Api.csproj`（+2 行 ProjectReference，`ReferenceOutputAssembly="false"` 仅构建顺序，同 BrowserUse/SmartTerminal 先例，注释明示）。
- 验收标准：后端构建 0 警 0 错；插件前端 `pnpm run build` 产出 dist/index.js；pre-commit 工件门禁 PASS。

## 3. 怎么做（Plan）

1. 门禁实测：`dotnet build Plugins/ClockBadge/ClockBadge.csproj`（结果 0 警 0 错，2.02s）→ `pnpm install && pnpm run build`（✓ 5 modules，dist/index.js 3.40 kB gzip 1.52 kB）。
2. `git add` 上述三组文件 + 本目录 → 提交（trailer 见 07）。

## 4. 边界（Task）

**Allowed**：`Plugins/ClockBadge/**`、`ForgeSelf.slnx`、`ForgeSelf.Api/ForgeSelf.Api.csproj`（仅注册行）、本工件目录。
**Forbidden**：任何发布动作（本地目录更新源 / tag / 通知更新——§8.5(4) 需单独同意）；`:51888` 运行实例与一切进程；插件行为/界面代码修改（源码为既有演示产物，原样入库）；其他插件的注册项。

## 5. Interaction Design（无界面交互——本任务零界面改动）

本任务为入库归档，无任何界面/交互改动。插件自带界面的既有交互为：菜单「时钟徽标」→ `/clock-badge` 路由加载 `ClockBadgeView`（展示服务器时间 + 刷新按钮 + 版本徽标），数据源 `GET /api/clock-badge/now`（只读，返回 `{ time, timestamp }`）。该界面的交互验收属插件交付批职责，**未执行 = Unknown**（见 05/07 披露）。

## 验证命令

- `dotnet build Plugins/ClockBadge/ClockBadge.csproj` → 0 警 0 错
- `cd Plugins/ClockBadge/web && pnpm run build` → ✓ built，dist 产出
- pre-commit（design:check + 工件门禁 + check-problem-classes -PilotTask）→ PASS
