# Specification

> 阶段：Stage 2｜`2026-11-05-plugin-ecosystem`  
> 接口均来自现有类型：`IDataLocationService`、`PluginVersionLayout`、`UpdateChecker.ParseSemVer/CompareSemVer`、`IToolFunctionExtension`、`ToolRegistry`。新类型只做本契约的校验、解析与工具，不另起第二套数据根。

## Functional Requirements

1. 用户插件目录 = `IDataLocationService.GetPluginDataDirectory(pluginId)`，即 `{GetHostDataDirectory()}/plugins/{pluginId}`。内置插件扫描根不变。
2. 首次安装：解压或拷贝到 `versions/{发行串}/`，写根 `plugin.json` 与 `current`。再次安装只暂存到新的 `versions/{发行串}/`，不改 `current`。
3. `POST /api/plugin/update/{id}` 仍表示激活已暂存的最高版本。`POST /api/plugin/activate/{id}` 请求体 `{ "version": "..." }` 激活指定版本。回滚走同一条激活路径。
4. 激活失败：把 `current` 与根 `plugin.json` 写回切换前的内容，并按切换前是否在运行决定是否重新加载旧版。
5. 卸载删除根 `plugin.json`、`current`、`versions/`。保留 `src/` 与 `.forge/`。
6. ZIP 在解压前拒绝 `..`、绝对路径、盘符、重名（忽略大小写）。
7. 扩展名 `.forgeself-plugin` 校验失败，更新源不扫描它。本地更新源只扫顶层 `*.zip`。
8. 单文件 `.dll`：用 `System.Reflection.Metadata` 读内嵌资源 `ForgeSelf.PluginManifest.json`，不 `Assembly.Load`。清单版本目录与 ZIP 相同。
9. 打包只打指定 `versions/{version}/`（没有版本目录时才打扁平根），排除 `src/`、`.git/`、`tests/`、`.forge/` 与宿主共享程序集（`ForgeSelf.*.dll`、`NewLife.*.dll`、`XCode.dll`、`MX.dll`）。下载文件名 `{Name}-{发行串}.zip`。
10. 版本比较使用宿主世代比较。`1.0.0.2611052130` > `1.0.0.2611051200`。跨根裁决仍把带 `v` 前缀或预发布后缀的串视为不可比（保留先扫者），避免畸形串夺走内置根。
11. 清单增加 `hostApiVersion`，缺省 `"1.0"`；显式其它值拒绝。`repositoryUrl` 非空时必须是 http(s) 绝对地址。`dependencies` 仍要求已安装。`permissions` 拒绝空白项。
12. 宿主注册工具：`plugin.scaffold`、`plugin.source.clone`、`plugin.source.sync`、`plugin.build`、`plugin.test`、`plugin.package`、`plugin.stage`、`plugin.activate`、`plugin.rollback`、`plugin.verify`、`plugin.publish.github`、`plugin.publish.nuget`。`PluginScaffolderService` 的对外字节包行为不改。
13. `build`/`test`/`package` 的工作目录是该插件 `src/`。已是 git 仓库时用独立 worktree。每次调用写 `{插件目录}/.forge/jobs/{jobId}.json`。
14. `publish.*`：请求未带 `confirm=true`，或环境变量 `GITHUB_TOKEN` / `NUGET_API_KEY` 为空，则失败闭合。不把密钥写入 job，不打印密钥。
15. GitHub：按仓库 URL 取 Release 资产中的 `.zip` 或 `.dll`。该 Release 含 `SHA256SUMS.txt` 时必须核对，不符则拒绝。NuGet：包名 `OpenForgeSelf.Plugin.{Name}`，从 nupkg 取出 ZIP 或 DLL 再进同一安装器。来自 GitHub/NuGet 的包默认只暂存；请求体 `activate=true` 表示用户当次确认，确认写入 `.forge/activation-consent.json`。用户上传的 ZIP/DLL 按首次安装/再次暂存规则处理。
16. `PluginStore.vue` 提供 GitHub 地址、NuGet 包名、ZIP、DLL 与版本列表「激活此版本」。
17. `scripts/package-plugin.ps1` 产出 `.zip`，停止产出 `.forgeself-plugin`。三段版本在包内清单补 10 位时间码（复用 `Get-FullReleaseVersion` 的规则，不改源码 `plugin.json`，不改宿主 `VersionPrefix`）。
18. HelloWorld 源码只写在 `{隔离数据根}/plugins/hello-world/src`，含 `.github/workflows/release.yml`。本任务不执行 push、tag、Release、NuGet。

## Input

- ZIP 或 DLL 上传；GitHub 仓库 URL；NuGet 包名；激活请求 `{ version }`；工具参数 JSON。
- 环境：`FORGESELF_DATA_ROOT`（已有，不改解析）；发布密钥仅环境变量。

## Output

- 磁盘布局：`plugin.json`、`current`、`versions/{发行串}/`、`src/`、`.forge/jobs/`。
- HTTP：安装结果、激活成功或 400。工具返回 JSON 字符串（含 `ok`、`jobId`、`message`）。

## Business Rules

- 发行串 = `<major>.<minor>.<patch>.<yyMMddHHmm>`。目录名、清单 `Version`、ZIP 文件名中的版本同一串。
- NuGet `PackageVersion` 不用 10 位时间码（NU1105）。用 `<major>.<minor>.<patch>.0`。完整发行串只出现在清单与目录名。
- 保留版本数仍是当前 + 上一版。
- 数据库不新增插件表迁移。

## Boundary Conditions

- 未注入 `IDataLocationService` 且未 `Initialize` 时，新安装失败并说明数据根未接线（测试里既有插件的更新仍用 `metadata.PluginDirectory`）。
- 版本目录已存在：暂存幂等，不覆盖。
- 包版本不高于当前生效版本：拒绝（与既有测试一致）。
- 插件未运行时激活只切指针，不因为假 DLL 加载失败而回滚；曾经运行且重新加载失败才回滚。

## Error Handling

- 路径穿越、非 zip/dll、`.forgeself-plugin`、清单缺 Id/Name/Version、`hostApiVersion` 非 `1.0`、SHA256 不符：拒绝，不写 `current`。
- 激活异常或重新加载返回 false：恢复指针与清单，job 记录 `ok:false`。
- `publish.*` 缺密钥：job `ok:false`，正文不含密钥值。

## Compatibility

- 三段版本（仓内既有插件 `1.2.0` 这类）仍能比较、仍能暂存。不把它们改写成时间码目录，除非走 `package-plugin.ps1` 打更新包（包内清单才补时间码）。
- `POST /api/plugin/update/{id}` 与 `rollback` 保留。
- `PluginScaffolderService.GeneratePlugin` 签名与返回字节包不变。

## Non-functional Requirements

- DLL 安装路径不把程序集载入宿主默认上下文。
- 密钥不进日志、不进 job 文件。
- 前端样式只用既有 `--el-*` 与现有按钮类，不新增裸色值。

## Failure Modes

无已知问题类。本任务不是缺陷复发。§1.8.1 记录的「安装器未 Initialize」由数据根接线覆盖，不单独立 PC。

## Interaction Design（交互设计）

本任务改宿主插件市场，HelloWorld 骨架本身无页面（工具与 HTTP 操作，见下「无界面交互」）。

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果 | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| GitHub 安装 | 填写仓库 URL 后点「从 GitHub 安装」 | 请求 `POST /api/plugin/install/source`，kind=github | 可提交 / 提交中 / 成功 / 失败 | 成功 toast「已暂存」或「已激活」；失败 toast 服务端 message | URL 空白时按钮禁用，旁注「先填写仓库地址」 | 未勾选「本次确认激活」则只暂存 |
| NuGet 安装 | 填写包名后点「从 NuGet 安装」 | 同上，kind=nuget | 同上 | 同上 | 包名为空按钮禁用 | 包名会规范成 `OpenForgeSelf.Plugin.{Name}`，用户只填 Name 或完整包名均可 |
| 上传 ZIP | 选择 `.zip` 后点「上传安装」 | `POST /api/plugin/install` | 未选文件 / 上传中 / 成功 / 失败 | toast | 未选文件按钮禁用 | 非 zip/dll 前端先拒绝 |
| 上传 DLL | 选择 `.dll` | 同上 | 同上 | 同上 | 同上 | 无内嵌清单时后端 400，toast 显示原因 |
| 激活此版本 | 版本历史里点「激活此版本」并确认 | `POST /api/plugin/activate/{id}` | 当前版本按钮禁用并写「当前版本」 | 确认框取消则不发请求；成功 toast 后关弹窗并刷新列表 | 无版本时文案「暂无版本历史」 | 激活失败 toast，列表版本不变 |

五态：加载（按钮与弹窗 `v-loading`）、空（未填/无版本）、出错（toast + 行内错误）、无权限（401 时 toast「需要宿主令牌」）、正常（列表与版本行）。

### 视觉规格

- 对齐：安装区放在市场左栏「管理」下方，输入与按钮左缘与分类按钮左缘对齐（同一侧栏 padding）。
- 主题：背景 `var(--el-bg-color)` / `var(--el-bg-color-page)`，文字 `var(--el-text-color-primary|regular|secondary)`，边框 `var(--el-border-color)`，主按钮沿用 `.open-page-btn` 的 primary token。亮/暗都走 Element 变量，不写死色值。
- 状态×主题：禁用 opacity 沿用 `.footer-actions button:disabled`。错误文字用 `var(--el-color-danger)`。

### 交互验收标准

- AC1 数据根：设 `FORGESELF_DATA_ROOT` 的假数据根时，新插件目录在该根 `plugins/{id}/versions/{ver}`，不在用户主目录。
- AC2 未设环境变量且按 Production 形态的数据根实现：新插件在该服务返回的 `.forgeself/plugins/{id}`（用假 `IDataLocationService` 表示 Production 根，不改默认分支、不写真实用户目录）。
- AC3 拒绝 `.forgeself-plugin`。
- AC4 ZIP 含 `../` 拒绝且目标目录不出现越界文件。
- AC5 DLL 只读内嵌清单，进程不加载该程序集（元数据 API）。
- AC6 首次安装写 `current`；再次安装不改 `current`。
- AC7 卸载后 `src/` 与 `.forge/` 仍在，`versions/` 不在。
- AC8 指定版本激活后 `current` 等于该串。
- AC9 运行中插件激活坏版本后 `current` 回到旧串。
- AC10 `VersionComparer`：`1.0.0.2611052130` > `1.0.0.2611051200`。
- AC11 打包 ZIP 不含 `src/`。
- AC12 `plugin.publish.github` 在无 `GITHUB_TOKEN` 时 `ok:false`，返回文本不含密钥字面量。
- AC13 市场安装区与「激活此版本」存在于 `PluginStore.vue`（组件测试或源码断言 + 前端 check）。

### 走查符合性

隔离 dev-stack 若已由其他会话占用则不启停。本任务用单测锁定落点与激活回滚。浏览器走查不在本回合对 :51888 进行。UI 符合性以 token 与交互表为准，`design:check` 作机器门禁。

### 无界面交互（HelloWorld 与 plugin.* 工具）

- `plugin.scaffold` 参数 `{ "pluginId": "hello-world", "name": "HelloWorld" }`，在数据根 `plugins/hello-world/src` 写骨架。错误：pluginId 空白 → `ok:false`。
- `plugin.activate` 参数 `{ "pluginId": "hello-world", "version": "1.0.0.2611052130" }`。加载失败时 job 文件 `ok:false` 且指针回旧版。
- `plugin.publish.github` 无 `confirm:true` 或无 `GITHUB_TOKEN`：`ok:false`，message 说明缺授权或缺密钥，不回显密钥。

## Acceptance Criteria

见上 AC1–AC13。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| 运行中的 dev-stack 是否被其他会话占用 | 不能为了验证去停它 | 单测覆盖落点；若端口上已有 dev-stack 则只读，不启停 |
| GitHub/NuGet 公网在本机代理下是否可达 | 提供器单测用假 HttpMessageHandler | 不把真实外网当通过条件 |
