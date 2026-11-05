# mini-task：PC-014 数据根污染族专项——夹具自愈 + AIProviderRegistry 锚定（task-12）

- **Intent**：全量 ~106 噪音红（Todo33/ImTaskDispatch37/ImGateway18+4/Memory3/DbConfigStore4 等，11-02 时 64 条持续膨胀），隔离复跑全绿=共享数据根污染。一次治本。
- **Spec**：根因=宿主启动型测试（WebApplicationFactory/XCodeConfigTests）经 XCodeConfig.AddXCode→DAL.AddConnStr 把进程级 ConnStrs 窃绑到自身临时根（跑完即删），受害族查询即 no such table（SQLite 自动建空文件）；隔离绿因 bin\Data 自动兜底积表假绿。修法①XCodeTestFixture ctor 自愈 ReassertBindingsIfStolen：比对 _expectedConnStrs vs DAL.ConnStrs，失窃才重登记+EnsureTablesCreated（不逐类换库，规避 PILOT-051 竞态）。②AIProviderRegistryTests 挂 [Collection("XCode")]+XCodeTestFixture（无锚摸 AIModel 实体的纪律缺口）。
- **Plan**：解剖机制 → 修夹具 → 过滤集健全 → 全量两轮验收。
- **Task**：Allowed=ForgeSelf.Api.Tests/XCodeTestFixture.cs, ForgeSelf.Api.Tests/Unit/AIProviderRegistryTests.cs; Forbidden=生产源码, db-dev 的 DbAdminTestFixture（task-15/17 域）。
- **risk**: medium —— 测试基建变更；以全量两轮+隔离健全兜底，git revert 可回滚
- **expected_files**: ForgeSelf.Api.Tests/XCodeTestFixture.cs; ForgeSelf.Api.Tests/Unit/AIProviderRegistryTests.cs
- **feature**: 100-INFRA-1（测试基线治理）
- **rollback**: git revert；自愈只在失窃时触发，回滚后回到 11-05 前行为

## 无界面交互

纯测试基建，无 UI。
