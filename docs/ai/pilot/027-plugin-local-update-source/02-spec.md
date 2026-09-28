# Specification — 插件本地目录更新源

> 阶段：Stage 2｜Task ID：PILOT-027 ｜ 返工 v2：采纳审查意见（P0-1/P1-1~8/P2 全项）

## Functional Requirements

**FR-1 插件更新源配置（MVP：仅本地目录）**
- 新模型 `PluginUpdateSettings`：仅 `LocalDir`（string）。**无 Provider 字段**（P2-4：MVP 无多源读写路径，避免死字段）。
- 新服务 `PluginUpdateSettingsService`（同构 `UpdateSettingsService`）：初始默认 LocalDir=""（未配置）→ 落盘 `{数据根}/Config/plugin-update-settings.json` → 启动读文件覆盖；`Update(Action)` 修改并落盘；与调用方共享同一实例引用（运行时修改立即可见，无需重启）。
- 校验（P1-3）：`LocalDir=""` 视为**未配置/停用**（接受，允许清空）；非空时目录必须存在，否则 400 并明确报错。对齐 PluginController 既有响应风格（`ApiResponse` + BadRequest 400，审查 P2-2：**不是**宿主 UpdateController 的 200+success:false 风格）。

**FR-2 API（PluginController，随类级 ApiKeyPolicy 鉴权）**
- `GET /api/plugin/update-settings` → `ApiResponse<PluginUpdateSettings>`（返回当前配置）。
- `PUT /api/plugin/update-settings` → body `{ "localDir": "D:\\...\\plugins" }`：
  - `localDir` 为空 → 保存为未配置（停用）；
  - `localDir` 非空且目录存在 → 保存；
  - `localDir` 非空且目录不存在 → `BadRequest(ApiResponse.Error("插件更新源目录不存在", 400))`。

**FR-3 检查更新扫描更新源目录**
- `PluginVersionService.CheckForUpdates()` 扩展：除现有 `_backups` 扫描外，若 `PluginUpdateSettings.LocalDir` 非空且目录存在，扫描目录内**顶层** `*.forgeself-plugin` 包（P2：MVP 不递归）：
  - `PluginPackagerService.ReadPackageMetadata(packagePath)` 读包内 plugin.json（Id/Name/Version）；
  - 与当前生效版本比较（`VersionComparer`）；包版本更高 → 加入 `updates` 列表；
  - 同一插件 `_backups` 与包来源并存时取最高版本；来源标注 `PluginUpdateInfo.Source` = "backup" | "package"（P2-3：**不返回 SourcePath**，避免本地路径泄露）。
  - 无效包（ValidatePackage 失败）跳过并记 Warn，不中断整体扫描。
- **版本比较基准统一（P1-2）**：展示（CheckForUpdates）与落地（UpdatePlugin）都用同一规则 ——「包版本 > 当前生效版本 且 > `_backups/<id>/` 现有最高版本目录」才视为可更新；任一条件不满足则不产生更新项、不 stage。

**FR-4 更新动作从包 stage（复用版本化链路）**
- `PluginVersionService.UpdatePlugin(pluginId)` 扩展（P1-1 插入点修正）：
  - 在读取 `_backups/<id>/` 版本目录**之前**（即现有代码 `var backupDir = ...; if (!Directory.Exists(backupDir))` **之前**，确保纯包源场景不早退），调用 `EnsureStagedFromPackageSource(pluginId, currentVersion)`：
    1. 扫 `PluginUpdateSettings.LocalDir` 顶层 `*.forgeself-plugin`，筛选包内 `Id == metadata.Id` 且 `Version > currentVersion` 且 `Version > _backups/<id>/ 最高目录版本` 的最高者（P1-2 统一基准）；
    2. `ValidatePackage` → 通过后**补入口 DLL 校验（P1-6）**：包内根存在 `plugin.json.EntryAssembly` 对应文件（用 `ZipArchive` 查 entry 或解包后查文件），缺失 → 记错误、返回失败（**不允许**报"更新成功"）；
    3. `PluginPackagerService.ExtractPackage(packagePath, _backups/<id>/<ver>/)` 解包 stage（目标为全新版本目录，side-by-side）；
    4. 返回该版本；主流程继续 `StageVersion → 切 current → ALC 换载`，回滚/版本历史/保留 N 版本自动生效。
  - `EnsureStagedFromPackageSource` 内部 try/catch（P1-4）：解包/stage 异常捕获后记 XTrace 错误并返回 null（视为无包源），**不得把异常传播到 500**；失败不中断 `UpdatePlugin` 既有语义（返回 false）。
- 不动 `UpdateFromPackage`（覆盖式，保留给 `/install` 上传场景）。

**FR-5 打包脚本**
- 新增 `scripts/package-plugin.ps1`：
  - 参数：`-Plugin <PascalCase目录>`（必填）、`-OutDir <目录>`（必填）、`-Configuration`（默认 Release）、`-Force`；
  - 流程：读 plugin.json（Id/Version）→ `dotnet publish` 插件 csproj → 收集产物（**排除宿主共享 DLL**：`ForgeSelf.*.dll`/`NewLife.*.dll`/`XCode.dll`/`MX.dll`，与 publish-plugin.ps1:139-143 同规则）→ 复制 plugin.json + `web/dist/**`（若存在）→ `Compress-Archive` 成 `<id>-<ver>.forgeself-plugin` 输出到 OutDir；
  - 幂等：目标包已存在且未 `-Force` → 跳过并提示；
  - 输出摘要（包路径/大小）到 stdout。
  - **编码铁律（P2-11）**：脚本含中文必须 UTF-8 BOM（RepositoryScriptTests 自动守卫；计划内验证包含该项）。

**FR-6 前端（审查 P2-8：落点=插件管理 tab）**
- `pluginApi.ts`：新增 `fetchPluginUpdateSettings()`（GET）/ `updatePluginUpdateSettings(localDir)`（PUT，走 `authFetch`；命名遵循 pluginApi 既有 `fetch*`/`update*` 约定）。
- `PluginsPanel.vue`（设置-插件管理 tab）：新增「插件更新源」小节：一行说明「插件包目录：用 scripts/package-plugin.ps1 打出 .forgeself-plugin 包后放这里，插件市场检查更新会发现新版本」+ 目录输入框 + 保存按钮；保存调 updatePluginUpdateSettings；成功 ElMessage.success，失败 ElMessage.error 显示后端 message；清空输入框保存 = 停用。
- 类型：`src/types/plugin.ts` 增 `PluginUpdateSettings` 接口（P2-3 补列）。

## Input

- 用户设置：插件更新源本地目录路径（插件管理 tab 输入，可清空）。
- 更新源目录内容：顶层 `<id>-<ver>.forgeself-plugin` 包（脚本产出；包内 plugin.json 的 Id/Version 为真源，文件名仅展示用途，P2-6）。

## Output

- `GET /api/plugin/updates`：既有 `PluginUpdateInfo` 列表，含来自更新源目录的更高版本（`Source=package`）。
- `POST /api/plugin/update/{id}`：成功切换至更高版本（无论来源 `_backups` 或包目录），失败返回明确错误（false + 日志）。
- 插件管理 tab 插件更新源小节：当前配置回显 + 保存/清空。

## Business Rules

1. 版本比较一律 `VersionComparer`；`EnsureStagedFromPackageSource` 的 stage 条件 = `packageVer > currentVersion` **且** `packageVer > _backups/<id>/ 最高目录版本`（P1-2 统一基准，展示与落地一致）。
2. 更新源目录中包的版本若不满足规则 1，不产生更新项、不覆盖已有 staged。
3. 解包 stage 目标为**全新目录** `_backups/<id>/<ver>/`，绝不覆盖正在加载的版本（side-by-side，铁律）。
4. 包内 Id 与文件名不一致：以包内 plugin.json Id 为准（P2-6）；包内 Id 与已安装插件不一致：不归属该插件，忽略。
5. 空 LocalDir / 目录不存在：更新源扫描静默跳过（视为无包源），不报错；目录无权限：扫描跳过记 Warn，保存时 400（P2-5）。
6. 并发更新：沿用 UpdatePlugin 现状（无锁，服务端串行处理），本设计不新增并发控制（P2-5 声明不处理）。
7. 配置损坏（JSON 解析失败）：LoadPersisted catch 后回退默认（同构 UpdateSettingsService），不阻塞启动（P2-5）。

## Boundary Conditions

- 插入点：`EnsureStagedFromPackageSource` 必须在 `UpdatePlugin` 的两个提前 return（`backupDir` 不存在 / 无版本目录）**之前**执行（P1-1），纯包源场景必须能走到版本切换。
- LocalDir 指向不存在目录：保存时 400；已保存后目录被删：扫描跳过、日志 Warn。
- 包损坏（ValidatePackage 失败）：跳过该包记 Warn，不中断；包缺入口 DLL：视为更新失败（P1-6）。
- 插件未安装（GetPluginMetadata null）：update 返回 false（现状不变）。

## Error Handling

| 场景 | 行为 |
| --- | --- |
| LocalDir 非空但目录不存在（保存） | 400，消息「插件更新源目录不存在」 |
| LocalDir 为空（保存） | 接受 = 停用更新源 |
| 包校验失败 | 记 Warn 跳过，扫描/更新流程继续 |
| 包缺入口 DLL（P1-6） | 更新失败（false + XTrace.Error），禁止报"成功" |
| EnsureStagedFromPackageSource 内部异常（P1-4） | 捕获 → 记错误 → 返回 null（无包源），不传播 500 |
| stage/切换失败 | 沿用 UpdatePlugin 现有 catch → false + XTrace 错误 |
| 前端保存失败 | ElMessage.error 显示后端 message |

## Compatibility

- 向后兼容：`_backups` 扫描、`/install` 上传、`UpdateFromPackage`、rollback/versions 全部不变；新端点独立新增。
- 配置存储独立于宿主 `update-settings.json`（审查确认正确，避免与宿主四源配置语义污染）。
- 鉴权：新端点随 PluginController 类级 `[Authorize("ApiKeyPolicy")]`（铁律 17 ✅）。
- `PluginUpdateInfo` 增 `Source` 字段（可空，缺省 null 兼容既有序列化）；**不加 SourcePath**（P2-3）。

## Non-functional Requirements

- 运行时配置修改立即可见（共享实例引用），保存无需重启宿主。
- 扫描开销：每插件最多读 N 个包文件头（ReadPackageMetadata 只读 plugin.json entry），目录规模小，无性能风险。
- 安全：路径拼接 = `Path.Combine(_backupsDirectory, metadata.Id, version)`，`metadata.Id` 来自已安装清单、`version` 必须 `Version.TryParse` 通过且为正则 `^\d+(\.\d+){0,3}$`（P1-5），杜绝路径穿越。

## Acceptance Criteria（闸门1 确认清单）

- [ ] AC1 后端 build 0 error；`PluginUpdateSettingsService` 单测（默认空/保存落盘/清空=停用/损坏回退）+ `CheckForUpdates` 包目录扫描单测 + `UpdatePlugin` 从包 stage 单测（含**纯包源场景**）全绿。
- [ ] AC2 前端 `pnpm run check` 0 error；vitest 全绿（PluginsPanel 插件更新源保存/清空逻辑用例，`.test.ts` 命名）。
- [ ] AC3 `GET/PUT /api/plugin/update-settings` 行为符合 FR-2（含目录不存在 400、清空=停用）。
- [ ] AC4 `GET /api/plugin/updates` 在更新源目录放置更高版本包后返回 `Source=package` 更新项；展示与落地版本基准一致（P1-2）。
- [ ] AC5 `package-plugin.ps1 -Plugin <X> -OutDir <目录>` 产出 `<id>-<ver>.forgeself-plugin`，zip 根含 plugin.json，`ValidatePackage` 通过、入口 DLL 存在（脚本产物交叉验证）；脚本含中文时带 UTF-8 BOM（RepositoryScriptTests 绿）。
- [ ] AC6 既有插件相关测试不回归（PluginVersionServiceTests / PluginMenuItemsMergeTests / PluginFrontendManifestTests 经「仅补 ctor 参数」适配后仍绿，P0-1；rollback/versions/install 相关用例仍绿）。
- [ ] AC7 文档同步（docs/02-features/038-plugin-local-update-source.md + Plugins/README.md 补节）。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| 更新源目录是否允许放多个插件的包（混放） | 低（设计已按多插件共用目录扫描实现） | 保守假设：允许，按包内 Id 归属 |
| 包目录是否要递归子目录扫描 | 低 | MVP 只扫顶层；如需递归后续扩展（记 TODO） |
| 端到端「页面检查更新→下载→切换」实测 | 中 | 自动门禁覆盖服务层+脚本；真实页面链路待发布到用户实例后由用户实测（记录为风险） |
| UI 落点（插件管理 tab） | 中 | 按审查 P2-8 建议定；闸门1 确认时向用户明示，可改 |