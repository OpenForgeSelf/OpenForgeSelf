using System.Collections.Generic;
using System.IO;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Sems.Services;
using ForgeSelf.Core;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.Sems;

/// <summary>
/// Launch 失败原因不依赖 XCode：目录从未存在时必须说明「不可达」，而不是折叠成一句 409。
/// </summary>
public class RunnerLaunchErrorTests
{
    private sealed class MissingRootRegistry : IProjectRegistry
    {
        public MissingRootRegistry()
        {
            Project = new ProjectInfo
            {
                Id = 7,
                Name = "gone",
                Root = Path.Combine(Path.GetTempPath(), "sems-never-existed-" + Guid.NewGuid().ToString("N")),
                Type = string.Empty
            };
            Commands.Add(new RunCommandInfo { Id = 3, ProjectId = 7, Name = "dev", Script = "echo hi" });
        }

        public ProjectInfo Project { get; }
        public List<RunCommandInfo> Commands { get; } = new();

        public bool Register(string root, out string? error) { error = null; return false; }
        public bool Register(string root, string? source, out string? error) { error = null; return false; }
        public List<ProjectInfo> GetAll() => new() { Project };
        public ProjectInfo? Get(int id) => id == Project.Id ? Project : null;
        public bool Update(int id, ProjectUpdate update) => false;
        public bool Remove(int id, out string? error) { error = null; return false; }
        public int AddCommand(int projectId, RunCommandInfo command) => 0;
        public bool UpdateCommand(int commandId, RunCommandUpdate update) => false;
        public bool DeleteCommand(int commandId) => false;
        public List<RunCommandInfo> GetCommands(int projectId) =>
            projectId == Project.Id ? Commands : new List<RunCommandInfo>();
    }

    [Fact]
    public void Launch_MissingDirectory_SetsLastLaunchError()
    {
        var registry = new MissingRootRegistry();
        Assert.False(Directory.Exists(registry.Project.Root));

        var ctx = new Mock<IContext>();
        ctx.Setup(c => c.Get<IProjectRegistry>()).Returns(registry);
        var runner = new RunnerService(ctx.Object);

        var session = runner.Launch(3);

        Assert.Null(session);
        Assert.Contains("不可达", runner.LastLaunchError);
    }
}
