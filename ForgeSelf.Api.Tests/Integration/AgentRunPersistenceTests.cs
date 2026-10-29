using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// AgentRun / AgentStepRun 双表落库验证（tasks.md T008）：
/// 表经 EntityFactory.InitConnection 自动创建、可插入可查询、RunId+StepIndex 判重查询可用。
/// </summary>
[Collection("XCode")]
public class AgentRunPersistenceTests : IClassFixture<XCodeTestFixture>
{
    public AgentRunPersistenceTests(XCodeTestFixture fixture)
    {
        _ = fixture;
        // 2026-10-29（PILOT-051 同类修复）：不得在此自建临时目录并 AddConnStr 换库——
        //  connStr 变更会触发 DAL.Reset 换 Database（Migration 回 On）+ 旧线程会话残留，
        //  全量联跑时把后续类的写入引去别处/吞行。连接串由夹具进程级一次性注册。
        // 本类 ctor 主动补建本连接缺失表（全量首绑可能早于本程序集加载，夹具首绑建表会漏；
        // 此处实体类型已 JIT 加载，InitConnection 必能扫到；Migration=On 下缺表建表、
        // 同构表空操作，且不动连接串 → 无 Reset 无重建竞态）。
        EntityFactory.InitConnection("AIAgent");

        // XCode 实体级缓存为进程级全局单例，跨测试共享；清缓存保证隔离。
        AgentRun.Meta.Cache.Clear("test reset");
        AgentStepRun.Meta.Cache.Clear("test reset");
        AgentRun.Meta.Cache.Expire = 0;
        AgentStepRun.Meta.Cache.Expire = 0;
    }

    [Fact]
    public void InsertRunAndStep_RoundTrips()
    {
        var run = new AgentRun
        {
            AgentId = "agent.generalist",
            AgentName = "通用",
            SessionId = "sess-1",
            TaskInput = "任务",
            Status = (int)AgentRunStatus.Running,
            CurrentStepIndex = 0,
            StepCount = 2,
        };
        run.Insert();
        run.Id.Should().BeGreaterThan(0);

        var step = new AgentStepRun
        {
            RunId = run.Id,
            StepIndex = 0,
            StepId = "s1",
            Name = "步骤一",
            Status = (int)AgentStepStatus.Running,
        };
        step.Insert();
        step.Id.Should().BeGreaterThan(0);

        var loaded = AgentRun.FindById(run.Id);
        loaded.Should().NotBeNull();
        loaded!.AgentId.Should().Be("agent.generalist");
        loaded.Status.Should().Be((int)AgentRunStatus.Running);

        var steps = AgentStepRun.FindAllByRunIdAndStepIndex(run.Id, 0);
        steps.Should().ContainSingle().Which.StepId.Should().Be("s1");

        var stepsByRun = AgentStepRun.FindAllByRunId(run.Id);
        stepsByRun.Should().HaveCount(1);
    }

    [Fact]
    public void Run_FindAllByStatus_Filters()
    {
        var running = new AgentRun { AgentId = "agent.generalist", Status = (int)AgentRunStatus.Running };
        running.Insert();

        var completed = new AgentRun { AgentId = "agent.generalist", Status = (int)AgentRunStatus.Completed };
        completed.Insert();

        AgentRun.FindAllByStatus((int)AgentRunStatus.Running)
            .Should().Contain(r => r.Id == running.Id)
            .And.NotContain(r => r.Id == completed.Id);
    }
}
