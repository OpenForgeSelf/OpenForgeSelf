# 06-review：AI-agent 插件文件三件套默认根修复

## 审查八问
1. **需求是什么？** 文件三件套（read_file/write_file/list_files）把「GUI 未选目录」从硬前置降级为默认根回退：GUI 选定 > `FORGESELF_DEFAULT_PROJECT_ROOT` > `AppContext.BaseDirectory` 上溯找 `.slnx`/`.git` 祖先；绝对路径根内直达、根外仍拒；错误封套带 hint/usedRoot。
2. **实现是否覆盖需求？** 覆盖。`ProjectWorkspaceService.cs`：接口加只读 `DefaultRoot`（三级候选锁内缓存）+ `ResolveSafePath` root=`ProjectRoot??DefaultRoot`、双 null 引导异常、绝对路径直通仍受前缀约束；`ProjectFileToolFunctions.cs`：三态封套（service 不可用 / 无根可兜 / 正常走默认根），成功响应与日志带 usedRoot。与用户指令口径一致（实施偏差已在 mini-task §Plan 留痕，覆盖早版「进程 CWD」与「usedRoot 恒在」的废弃口径）。
3. **验证是否真实跑过？** 跑过：`dotnet build` 0 错误（旁路绕锁）；过滤集 24/24 通过（0 失败）。原文见 05-evidence.md。
4. **测试是否充分？** 单测级充分（三级候选、优先级、越界、信封三态、env try/finally、临时目录自建）；缺真实宿主浏览器链路验证（属 ②-⑥ 闭环范围，本批明确不含）。
5. **有没有改不该改的？** 无。改动面=2 个服务文件+1 个测试文件+工件目录；`web/`、宿主 `ForgeSelf.Api/**`、其他插件未触碰（git status 对照核对）。folder_stats（#2）未动，符合范围。
6. **错误路径是否可用？** 封套三态：workspace 不可用→`项目工作区服务不可用`+hint 检查插件宿主；无根→原语义升级为带 env 变量指引的引导文案；越界→`拒绝访问项目目录之外的路径`+root 可得时 hint 显示当前生效根。
7. **有什么风险？** ① 上溯候选在非常规包装目录下可能解析不到根（有引导兜底，不阻塞）；② 旧调用方若硬编码「未选目录必失败」行为会受益于放宽，属预期改进非破坏。
8. **可否交付？** 单测级可交付；浏览器走查与发布属后续批次。

## Final Decision: **APPROVED**（单测级；②-⑥ 闭环另批）
- 依据：门禁双绿（build 0 错误 / 24/24）+ 范围合规 + 需求逐条核验通过（见 05-evidence）。
- 遗留：真实宿主走查/发布、folder_stats ACL Failed（#2）、未知工具信封增强（#3）、list_allowed_directories/get_working_directory 标准工具——均在 TODO.md 在册。
