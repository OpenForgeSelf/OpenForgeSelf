# DesignSystem 插件 TODO（插件级遗留）

> 条目格式：`- [ ] <事项>（P<优先级>，来源）`；完成即移除，不留 ✅ 堆积（AGENTS §7.5）。
> 当前版本：`plugin.json` 2.7.1（生成/审计/导出/DTCG 导入回流全链路已交付，commit `61b327d`）。
> 本文件由根 TODO.md 随 worktree 销毁前迁入（2026-09-30，同 Sems/AIAgent/McpCenter 先例）。

## 🔄 等用户动作（不阻塞、agent 不动宿主）

- [ ] **设置-版本更新页对 v2.7.1 走「检查更新 → 下载 → 重启并更新」（P1，来源:v2.7.0 收口 + v2.7.1 头部重排）**：
  更新源 = 本地目录（`artifacts/update`），agent 不停/启/杀宿主进程。

## ⬜ 插件级待办（不需拍板即可做的候选）

- [ ] **`variantJson` 多轴组合表单（P3，来源:v2.6.7 有意留白）**：多轴变体目前走显式「直接写 JSON」；做成先选轴再选档位的表单，JSON 仍由界面按后端 canonical 拼。
- [ ] **语义角色序出表（P3，来源:v2.6.5 自查）**：`SemanticResolver` 的 role 清单目前只影响生成，界面没有按它排的地方；要出 `meta.roleOrder` 消费者需改 `ListVariants` 读路径 + DTO 出轴值。
- [ ] **Tokens Studio 完整格式导入（P3，来源:M13 收口）**：`$themes` / 多 set 文件；M13 只做了单文件 DTCG，`/meta.importFormats` 已留扩展位。
- [ ] **整包 `bundle` 在浏览器侧取不到 zip（P2，用户指示先不搞）**：`page.evaluate(fetch)` → `Failed to fetch`；`<a download>` → `download.path: canceled`；已排除构建慢（后端 605ms）。续做入口：宿主 `dotnet` 侧直连 `http://127.0.0.1:<port>/api/design-system/projects/{id}/export?format=bundle` 绕开代理比对，再看 `ExportedFile` 该不该流式返回。

## 📌 宿主级/仓库级暂寄（根 TODO.md 随 worktree 销毁，先寄存在此防丢；处理时可挪回根队列或对应插件 TODO）

- [ ] **测试项目 staging glob 只拷 DLL 不拷清单 → 7 项插件控制器 404（P1，需拍板，来源:全量测试排查）**：
  `ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj:82-89` 的 glob `Plugins\**\*.dll` 不含 `plugin.json`/`deps.json` →
  `PluginManager.DiscoverPlugins` 跳过无清单目录（`ForgeSelf.Api/Plugins/PluginManager.cs:262-267`）；回归由 `c7941a0`（2026-09-26 插件移到仓库根）引入。
  顺带一条假绿：`WorkflowPlanningIntegrationTests.GetExecutionStatus_NonExistentId_...` 恰因路由缺失 404 而"通过"。
- [ ] **宿主 SQLite 跨请求写锁根治 + `XCodeConfig.cs` 的 `Busy Timeout=5000` 去留（P1，需拍板，来源:v2.6.8）**：
  并发读写冒 `SQLITE_BUSY` → 接口 500；导出页并发只读实测 `database is locked`，连跑三轮 e2e 的 500 是 2/0/3（只读退避重试兜住才没红）。
  插件侧止痛（Busy Timeout/整批事务/发布串行化/只读退避）已随 v2.7.1 入库；根治需宿主级方案。
- [ ] **`ForgeConfigTests.ForgeSetting_配置文件归一到数据根Config目录` 全量跑红（P2，宿主侧，来源:v2.7.0 全量门禁）**：
  期望归一后以 `Config\ForgeSetting.config` 结尾、实际落在 `Temp\ofs_configuni_…\` 没进 Config 子目录；单跑未复现则与全量调度顺序有关。
- [ ] **Sems/ScriptRunner「真起外部进程再杀」用例组脆弱（P2，非 DesignSystem，来源:v2.6.8 全量门禁）**：
  `Sems.RunnerServiceTests.StopExternal_Kills_ExternalProcess` 全量跑红；隔离跑该类 18 项里 7 红 ⇒ 非顺序依赖，进程/环境时序敏感。
- [ ] **ScriptRunner 取消被后台收尾改写为 `Timeout`（P2，宿主侧插件缺陷，来源:目标续轮 M12c 全量门禁）**：
  根因已只读定位：`Plugins/ScriptRunner/Services/ScriptExecutor.cs:146-152` 先写 Cancelled，`:316-335` 后台收尾用唯一判据
  `externalCancellationToken.IsCancellationRequested` 再写同一行 —— 走 `CancelAsync` 时它是 `None` 恒判 Timeout；两次写间无「已终态不覆写」保护。
  改法：给 `CancelAsync` 自己的取消标记，或禁止覆写已终态。属 ScriptRunner 插件，需另立任务。
- [ ] **宿主有直接钉按钮底色的规则，主题层盖不过它（P3，宿主侧，来源:目标续轮 M11 功能证据）**：
  e2e 实测注入 `element-plus` 接缝后 `--el-color-primary` 与 EP 组件层 `--el-button-bg-color` 都已变成插件的 `#6d28d9`，
  但探针按钮最终 `background-color` 仍是宿主琥珀 —— 存在不读变量的 `background` 规则（诊断截断未定位到具体条）。
  插件侧职责已到边界（变量层已赢）；继续要改宿主样式，需用户点头。
- [ ] **e2e 宿主实例身份没校验（P2，测试基建需点头，来源:v2.6.7 e2e）**：
  `global-setup.ts` 固定用 7102；端口被旧实例占用时新宿主绑不上但健康检查通过，测试打在旧产物上（v2.6.7 版本自洽断言就这么红过）。
  修法：起宿主前探 7102，被占即 fail fast 并打印占用者可执行路径；或健康检查比对本次 publish 的构建标识。
