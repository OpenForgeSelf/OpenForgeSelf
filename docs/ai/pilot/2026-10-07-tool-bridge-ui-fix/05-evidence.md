# 05 Evidence — TB-UI-FIX-20261007

证据分级：Verified = 本会话有工具回执可对；Inferred = 推断未实测；Unknown = 没有证据。三档不混用。

## 1. 变更文件

- Plugins/ToolBridge/web/src/ToolBridgeView.vue：22124 字节 -> 24528 字节（整文件重写，两次写入，第二次只修一处反斜杠）
- Plugins/ToolBridge/plugin.json：Version 1.0.2 -> 1.0.3（941 字节，大小未变）

## 2. Verified

- V1 write_file 回执 ok=true，ToolBridgeView.vue totalBytes=24528。
- V2 git grep scratch 命中单反斜杠 placeholder（D:\scratch\agent-lab），第一次写入引入的双反斜杠回归已修复。
- V3 git grep -c data-testid 返回 24（原文 22 + 两个复制提示元素）。该检查只能证明没有整段丢失，不能证明内容正确。
- V4 pnpm build（经 pwsh -Command 包装）exitCode=0，vite 6.4.3，9 modules transformed，约 5 秒。
- V5 dist/index.js 23852 -> 25221 字节，dist/style.css 6175 -> 6341 字节。
- V6 git grep --no-index 在 dist/index.js 命中 tb-copy-result-note 1 次，新代码已进入源码树内的构建产物。
- V7 git grep 1.0.2 在 plugin.json、web/package.json、ToolBridgePlugin.cs、e2e/plugins/tool-bridge、ForgeSelf.Api.Tests/Plugins/ToolBridgeTests 无命中，版本号无其他硬编码。
- V8 dist/ 被 Plugins/ToolBridge/web/.gitignore:3 忽略，不随提交入库。
- V9 Plugins/ToolBridge/ 在 git 中为 untracked（git status 显示 ??），本批改动没有版本控制备份。

## 3. Unknown

- U1 宿主运行时加载的是源码树 dist 还是发布/输出目录里的副本。ToolBridge.csproj 与 ToolBridgePlugin.cs 无 dist 相关配置，复制动作若存在则在别处，待查。
- U2 全部交互行为：粘贴框点击全选、已有焦点再点不全选、失焦后再点重新全选、复制按钮文案恒定、提示次数递增、解析/执行/载入历史后提示清零。均未自动化验证，待用户手测。
- U3 类型检查：Plugins/ToolBridge/web/package.json 无 typescript 与 vue-tsc，构建不做类型检查，类型错误不会拦住 dist。这是该插件前端的既有缺口，非本批引入，本批不补依赖。
- U4 已有 ToolBridge e2e（6 条）与 vitest 在本批改动后是否仍绿：未运行。

## 4. Inferred（已知风险，未实测）

- I1 onPasteMouseDown 未限制鼠标键：未聚焦时右键 mousedown 也会被拦截并全选，右键菜单大概率仍可出现。
- I2 textarea 带 resize: vertical：首次点击落在缩放手柄上时，preventDefault 可能使缩放失效。
- I3 两者修法均为 if (e.button !== 0) return，合并到下一次整文件重写，不单独发。
- I4 write_file 回执的 created 字段对覆盖写也报 true（两个已存在文件均如此），不可用来判断文件是否已存在。

## 5. 流程偏差

- D1 e2e 由用户在闸门 1 明文裁剪，本批无自动化行为验证。
- D2 本目录目前只有 00-05，门禁 verify-pilot-artifacts.ps1 要求 00-07 八件齐全，06-review 与 07-final-report 未写，闸门 2 未过，不得提交。
- D3 会话内多次 write_file 整文件覆盖 24KB 文件，原因是通道只有整文件写入，没有精确编辑工具。

## 6. 宿主前端加载路径侦察（追加）

### Verified
- V10 PluginFrontendFileMiddleware 在请求时由 PluginManager 解析插件目录：存在 current 指针且 versions/<current>/web 存在则读版本快照，否则读 {插件目录}/web；只对外提供 web 目录内文件。
- V11 ForgeSelf.Api.csproj 含 Plugins/**/web/dist/** 的 Content 规则（第 63 行）和把 ../Plugins/*/web/dist/**/* 复制到 $(OutDir)Plugins/ 的 Copy 任务（第 157、165-166 行）；csproj 原文为反斜杠路径。
- V12 ToolBridge.csproj 与 ToolBridgePlugin.cs 无 dist/web 相关配置；宿主对 ToolBridge 的 ProjectReference 仅建立构建顺序（ReferenceOutputAssembly=false）。
- V13 scripts/release/build-frontend.ps1 把 Plugins/<X>/web 构建为 Plugins/<X>/web/dist 供打包拾取；release-local.ps1 的 -SkipFrontend 使用已有 dist。

### Inferred
- I5 开发宿主运行时的插件目录大概率是 ForgeSelf.Api 输出目录下的 Plugins/ToolBridge，而非源码树；源码树 dist 变新后需宿主重新构建（触发复制）才可见。复制任务的触发时机未读。
- I6 若测试宿主是已发布整包（版本化布局，存在 current 指针），读取的是 versions/<current>/web 快照，重建宿主不会更新，需发布新版本；发布属生产动作，闸门 2 未过，未执行。

### Unknown
- U1（收窄后）本次手测宿主是开发宿主还是发布整包，其插件目录的实际位置；FORGESELF_DEV_WEB_SRC 开发模式是否把前端指回源码树，待读 DevMode。

## 7. 测试宿主定位（追加）

### Verified
- V14 端口 7002 查询无监听结果，Get-Process 报 Id 为 null；端口 51888 查询成功，监听进程为 D:/src/tools/ForgeSelf/versions/2.7.3.2610062027/ForgeSelf.exe（已发布整包，版本化目录）。
- V15 宿主 csproj 的 StageAllPlugins 目标为 AfterTargets=Build，dist 复制发生在宿主构建之后；Condition 与复制细节尚未读。
- V16 宿主 bin 下三份 ToolBridge dist/index.js 均为旧版：Debug 23852 字节，Release 与 Release/win-x64 均 23829 字节；新构建为 25221 字节。
- V17 FORGESELF_DEV_WEB_SRC 只影响 Cache-Control（no-store），不改变前端加载路径；DevMode 疑点关闭。

### Inferred
- I7 同一 cmdlet 能查到 51888 却查不到 7002，故更像 7002 确实没有监听，而不是权限问题。
- I8 工具桥通道与用户反馈 bug 的页面都在 51888 的发布整包上（排除法），其 ToolBridge 为 1.0.2。
- I9 要看到 1.0.3 界面须在开发宿主（7002）重建并启动；发布整包要发布新版本才会更新，属生产动作，闸门 2 未过，未执行，也不向整包目录手工拷贝文件。

### Unknown
- U5 开发宿主与发布整包是否共用数据目录或 SQLite，同时运行有无冲突。
- U6 build.ps1 是否会启动宿主，以及其使用的 Configuration（Debug 或 Release）。
- U7 发布整包里 ToolBridge 的插件目录位置与 current 指针。

## 8. 第三次整文件重写与重新构建（追加）

### Verified
- V18 ToolBridgeView.vue 第三次整文件写入，totalBytes=24865（上一版 24528）。
- V19 git grep 命中 e.button !== 0（源码第 79 行）；placeholder 在第 289 行，仍为单反斜杠；data-testid 计数仍为 24。
- V20 pnpm build（经 pwsh -Command 包装）exitCode=0，约 6.8 秒；dist/index.js 25323 字节，dist/style.css 6341 字节。
- V21 StageAllPlugins 的三条 Copy 均无 Condition，均带 SkipUnchangedFiles=true；_PluginBin 取 Plugins/*/bin/$(Configuration)/net10.0，Debug 与 Release 各带各自配置的 DLL。
- V22 build.ps1 的 grep 被守卫拒绝（command_rejected），原因是搜索串含 Start-Process 一词；属守卫按词误杀，非 build.ps1 内容问题。build.ps1 内容仍未读。

### Inferred
- I10 缩放手柄判定区 18px 为经验值，未实测。
- I11 右键、中键不再触发全选，由 e.button 判断保证，未在界面验证。

### Unknown
- U8 发布脚本是否重新构建 dist、是否触碰源码树，未读 publish-plugin.ps1。
- U9 全部交互行为仍待用户手测；用户决定不经闸门 2 直接构建发布测试，该决定由用户明文作出。
- U10 发布后 06-review 与 07-final-report 仍缺，门禁脚本会失败，提交前须补。
