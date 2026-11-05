# 05-evidence — task-17 DevModeTests 静态态复位

> 证据分级：✅ Verified（亲跑真实输出）｜Inferred（代码推断）｜Unknown（未验证）

## 证据清单

| # | 项 | 命令/来源 | 结果 | 分级 |
|---|----|----------|------|------|
| E1 | flake 根因定性 | 读 DevMode.cs（Enabled private set 唯一写手 Initialize）+ PluginFrontendFileMiddleware.cs L129（WebSrcEnabled 三元决定 no-store/immutable/no-cache）+ DevModeTests.Dispose（只清 env 不复位静态） | 机理闭环：残留 Enabled=true → 并行类 WebSrcEnabled=true → 缓存头断言翻转 | Inferred→Verified（E2 复现路径吻合） |
| E2 | 修复后过滤集（task-17 直接口径） | `dotnet test --filter 'FullyQualifiedName~PluginFrontendFileMiddlewareTests\|FullyQualifiedName~DevModeTests'` | ✅ **22/22 通过，0 失败**（233ms） | Verified（.temp/log-t17-filter.txt） |
| E3 | qa 收尾残余 ForgeSetting ×1 对表 | `dotnet test --filter 'FullyQualifiedName~ConfigUnifierTests\|FullyQualifiedName~ForgeSetting'` | ✅ **5/5 通过，0 失败**（103ms）——本 worktree（含 merge main 3a7ab62）已绿 | Verified（.temp/log-t17-forgesetting.txt） |
| E4 | 定向对表集（lead 批准口径全集，13:49） | `dotnet test --filter 'DbAdmin\|XCodeTestFixtureIronRuleTests\|DevModeTests\|PluginFrontendFileMiddlewareTests\|ConfigUnifierTests\|ForgeSetting\|ProblemClassRegistryTests'` | **201 总 = 198 通过 + 3 失败**（14s）；198 通过覆盖 DbAdmin 168 + IronRule 守卫 + task-17 三件 + ForgeSetting 系 | Verified（.temp/log-t17-directed.txt） |
| E5 | E4 的 3 红归属 | `dotnet test --filter ProblemClassRegistryTests -v normal` 单类复跑 | 3 红全为 **PC-024（smart-terminal 死会话 404，st-dev 域）工件登记腐烂**：① PC-024-frontend-poll-error-handling.md 缺必填键 signature ② cases/README.md 索引缺 case 行 ③ case front-matter id 为空与文件名不一致——**非本任务引入（本任务未触碰任何 PC 工件）；main 84875d3 引入工件后即带** | Verified（.temp/log-t17-pcreg.txt） |
| E6 | 中档全量本机环境不可达 | 两次全量（13:15/13:30）均卡死 ScriptRunner 簇（ScriptRunnerTests.CancelAsync 等，40+ 分钟零输出、testhost 单核烧） | 定性：CPU 争用下取消类 async 测试挂（主工作区 20+ dotnet 进程 + 51888 实例争用）；孤儿 testhost 占锁期间疑诱发 XCode 自愈风暴。**qa #3 同代码 15 分钟正常收口为对照证据**。孤儿已全部清理；正式全量由 qa 环境代跑（lead 已派） | Verified（进程采样）+ Inferred（机理） |

## 结论
- task-17 三件修复（复位协议/防御复位/守卫）在定向对表集内 **0 红**（E2/E4）。
- qa 收尾残余 ×2 的归属闭环：PluginFrontendFileMiddleware ×2 = 本任务直接修复对象（E2 绿）；ForgeSetting ×1 = E3 绿。
- 中档全量对表（「PC-014 族不新增红 + db-admin 基线照旧」）以 **qa 环境代跑结果为准**（lead 裁定），本机定向集作为 task-17 验收依据（lead 批准）。