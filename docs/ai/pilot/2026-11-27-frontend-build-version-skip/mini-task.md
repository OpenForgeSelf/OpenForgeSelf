# mini-task: 前端构建版本感知增量（版本一致跳过复用）

## 五要素

- **Intent（为什么/做什么）**：本地打包链每次全量重建前端（宿主 ~78s + 15 插件），其中绝大多数插件版本未变。以 package.json version 为构建版本标识：上次构建记录的版本与当前一致且产物存在 → 跳过构建复用产物；完整构建成功后记录版本。
- **Spec（规格）**：
  - 记录文件：`artifacts/frontend-build-versions.json`（`artifacts/` 已 gitignore），结构 `{ "<key>": { "version": "<pkg version>", "builtAt": "..." } }`。
  - 宿主 key=`ForgeSelf.Web`，产物判据=`ForgeSelf.Api/wwwroot/index.html`；插件 key=插件目录名，产物判据=`dist/index.js`。
  - 跳过条件（全部满足）：manifest 记录版本 == 当前 package.json version **且** 构建产物存在。
  - 版本约定（2026-11-27 输入3 用户拍板）：宿主 web package.json version **紧跟 ForgeSelf.Api.csproj 的 VersionPrefix 三段**（如 2.4.22，不带时间码）；需要重打宿主前端时**手动同步修改 api 与 web 两处版本**。插件版本沿用各自 package.json（与 plugin.json 同步纪律，手动维护）。
  - 一致性校验：build-frontend 启动时若宿主 version ≠ csproj VersionPrefix → 黄色警告（不阻断，提示两处需同步 bump）。
  - Failure Modes：manifest 损坏/缺失 → 视为无记录，全量重建（安全侧）；dist 被手动删 → 产物判据失败 → 重建；改码未 bump 版本 → 不在本机制防护范围（用户明确选择人工版本纪律，弃用源码指纹方案）。
  - **Interaction Design**：无界面交互（纯构建脚本改动，不涉及任何 UI）。
- **Plan（真实文件）**：
  - `ForgeSelf.Web/package.json`：version 0.0.0 → 2.4.22（对齐 csproj，reason: 宿主 0.0.0 恒定会使版本判断永真跳过）。
  - `scripts/release/build-frontend.ps1`：新增 manifest 读/写函数 + `Test-BuildUpToDate`；`Invoke-PnpmBuild` 增加 Key/Artifact 参数，构建前判断跳过、构建成功后回写；宿主步骤前加版本一致性警告。
- **Task（边界）**：Allowed=上述 2 文件 + pilot 工件；Forbidden=release-local/package-release（不受影响）、插件 package.json 版本、任何插件源码。
- **Acceptance Criteria**：
  - [x] AC1 首跑（manifest 无记录）→ 宿主/插件重建并写入 manifest（05 场景①，54.3s + manifest 记录 2.4.22）
  - [x] AC2 二跑（版本未变）→ 输出 [skip] 且不执行 pnpm（05 场景② 0.0s；终验全量 16 单元全 [skip]）
  - [x] AC3 版本变化 → 仅该构建单元重建，其余跳过（05 场景③，AIAgent 1.3.0→1.3.1 重建并回写 manifest，还原后重建回 1.3.0）
  - [x] AC4 宿主 version ≠ csproj 版本时出现黄色警告（05 场景④，2.4.23 vs 2.4.22 触发 WARNING）

## 适用问题类

无（非缺陷类增强）。关联环境约束：PC-047（safe-delete 配额，TODO 挂账）——本机制的跳过路径反而减少子进程链长度，缓解该约束。

## 闸门1

用户输入3（2026-11-26 16:55 需求 + 2026-11-27 方案拍板回答「宿主紧跟 api 版本、手动同步修改、统一三段不带时间码」）= 规格与方案批准。
