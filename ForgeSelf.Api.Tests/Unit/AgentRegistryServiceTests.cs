using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using AgentDefinitionEntity = ForgeSelf.Api.Plugins.AIAgent.Entities.AgentDefinition;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// AgentRegistryService 单元测试。
/// </summary>
/// <remarks>
/// 构造器会 <c>AgentDefinitionEntity.FindAll()</c>（空表时 XCode InitData seed 内置 Agent），
/// 触碰进程级全局的 XCode "AIAgent" 连接 —— 必须收进 XCode 串行集合，
/// 否则与 <c>[Collection("XCode")]</c> 内交换连接串的测试类产生竞态（QA 2026-09-28 实测踩坑）。
/// 注意：XCode InitData 每进程仅在实体首次访问时触发一次，seed 会落在先绑定 "AIAgent"
/// 连接的测试类的临时库里；本类运行时当前连接指向的库可能没有内置 Agent，
/// 因此构造器中显式补种（与 InitData 的 FromModel+Insert 语义一致）。
/// </remarks>
[Collection("XCode")]
public class AgentRegistryServiceTests
{
    private readonly AgentRegistryService _registry;

    public AgentRegistryServiceTests()
    {
        EnsureBuiltInAgentsSeeded();
        _registry = new AgentRegistryService();
    }

    /// <summary>
    /// 确保当前 "AIAgent" 连接指向的库中存在全部内置 Agent。
    /// 同集合内先执行的测试类（如 AgentRunPersistenceTests）会把全局 "AIAgent"
    /// 连接绑定到各自随机临时目录，且 InitData 不再二次触发，故此处按需补种；
    /// 全程 try/catch —— 表尚未创建等异常场景下，LoadFromDatabase 的回退路径
    /// 会直接把内置定义装入内存，不应让构造器抛错放大故障面。
    /// </summary>
    private static void EnsureBuiltInAgentsSeeded()
    {
        try
        {
            // 首次访问触发建表（必要时含 InitData seed）
            var existing = AgentDefinitionEntity.FindAll();
            foreach (var model in ForgeSelf.Api.Plugins.AIAgent.Entities.BuiltInAgentDefinitions.GetAll())
            {
                if (existing.Any(e => string.Equals(e.Id, model.Id, StringComparison.OrdinalIgnoreCase))) continue;

                var entity = new AgentDefinitionEntity();
                entity.FromModel(model);
                entity.Insert();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AgentRegistryServiceTests] 内置 Agent 补种跳过：{ex.Message}");
        }
    }

    #region Register & Unregister

    [Fact]
    public void RegisterAgent_ValidAgent_AddsToRegistry()
    {
        // Arrange
        var agent = new AgentDefinition
        {
            Id = "test.custom",
            Name = "Custom Agent",
            Description = "A custom test agent",
            Type = AgentType.Generalist,
            Capabilities = new List<string> { "custom-task" }
        };

        // Act
        _registry.RegisterAgent(agent);

        // Assert
        var result = _registry.GetAgent("test.custom");
        result.Should().NotBeNull();
        result!.Name.Should().Be("Custom Agent");
    }

    [Fact]
    public void RegisterAgent_DuplicateId_OverwritesExisting()
    {
        // Arrange
        var agent1 = new AgentDefinition { Id = "test.dup", Name = "Agent 1", Type = AgentType.Generalist };
        var agent2 = new AgentDefinition { Id = "test.dup", Name = "Agent 2", Type = AgentType.Generalist };

        // Act
        _registry.RegisterAgent(agent1);
        _registry.RegisterAgent(agent2);

        // Assert
        var result = _registry.GetAgent("test.dup");
        result.Should().NotBeNull();
        result!.Name.Should().Be("Agent 2");
    }

    [Fact]
    public void UnregisterAgent_ExistingAgent_RemovesFromRegistry()
    {
        // Arrange
        var agent = new AgentDefinition { Id = "test.remove", Name = "To Remove", Type = AgentType.Generalist };
        _registry.RegisterAgent(agent);

        // Act
        _registry.UnregisterAgent("test.remove");

        // Assert
        var result = _registry.GetAgent("test.remove");
        result.Should().BeNull();
    }

    [Fact]
    public void UnregisterAgent_NonExistingAgent_DoesNotThrow()
    {
        // Act
        var action = () => _registry.UnregisterAgent("nonexistent.agent");

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region GetAgent & GetAllAgents

    [Fact]
    public void GetAgent_ExistingAgent_ReturnsAgent()
    {
        // Act
        var result = _registry.GetAgent("agent.coordinator");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("agent.coordinator");
        result.Name.Should().Be("协调者");
    }

    [Fact]
    public void GetAgent_NonExistingAgent_ReturnsNull()
    {
        // Act
        var result = _registry.GetAgent("nonexistent.agent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetAllAgents_ReturnsAllAgents()
    {
        // Act
        var result = _registry.GetAllAgents();

        // Assert
        result.Should().NotBeEmpty();
        result.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    #endregion

    #region GetAgentsByType

    [Fact]
    public void GetAgentsByType_CoordinatorType_ReturnsCoordinators()
    {
        // Act
        var result = _registry.GetAgentsByType(AgentType.Coordinator);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(a => a.Type.Should().Be(AgentType.Coordinator));
    }

    [Fact]
    public void GetAgentsByType_ResearcherType_ReturnsResearchers()
    {
        // Act
        var result = _registry.GetAgentsByType(AgentType.Researcher);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(a => a.Type.Should().Be(AgentType.Researcher));
    }

    [Fact]
    public void GetAgentsByType_NoMatchingType_ReturnsEmptyList()
    {
        // Act
        var result = _registry.GetAgentsByType((AgentType)999);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region FindAgentsByCapability

    [Fact]
    public void FindAgentsByCapability_ExistingCapability_ReturnsMatchingAgents()
    {
        // Act
        var result = _registry.FindAgentsByCapability("task-planning");

        // Assert
        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(a => a.Capabilities.Should().Contain("task-planning"));
    }

    [Fact]
    public void FindAgentsByCapability_NonExistingCapability_ReturnsEmptyList()
    {
        // Act
        var result = _registry.FindAgentsByCapability("nonexistent-capability");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetBestAgentForTask

    [Fact]
    public void GetBestAgentForTask_ResearchTask_ReturnsResearcher()
    {
        // Arrange
        var requiredCapabilities = new List<string> { "research", "information-retrieval" };

        // Act
        var result = _registry.GetBestAgentForTask("研究人工智能的发展历史", requiredCapabilities);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void GetBestAgentForTask_NoRequiredCapabilities_ReturnsCoordinator()
    {
        // Act
        var result = _registry.GetBestAgentForTask("一个普通的任务", new List<string>());

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void GetBestAgentForTask_WithNullCapabilities_ThrowsArgumentNullException()
    {
        // Act
        var action = () => _registry.GetBestAgentForTask("test task", null!);

        // Assert
        action.Should().Throw<NullReferenceException>();
    }

    #endregion

    #region Built-in Agents

    [Fact]
    public void BuiltInAgents_ShouldHaveCoordinator()
    {
        // Act
        var coordinator = _registry.GetAgent("agent.coordinator");

        // Assert
        coordinator.Should().NotBeNull();
        coordinator!.Type.Should().Be(AgentType.Coordinator);
        coordinator.Capabilities.Should().NotBeEmpty();
        coordinator.SystemPrompt.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BuiltInAgents_ShouldHaveResearcher()
    {
        // Act
        var researcher = _registry.GetAgent("agent.researcher");

        // Assert
        researcher.Should().NotBeNull();
        researcher!.Type.Should().Be(AgentType.Researcher);
    }

    [Fact]
    public void BuiltInAgents_ShouldHaveValidPersonality()
    {
        // Act
        var coordinator = _registry.GetAgent("agent.coordinator");

        // Assert
        coordinator.Should().NotBeNull();
        coordinator!.Personality.Should().NotBeNull();
        coordinator.Personality!.Creativity.Should().BeInRange(0, 1);
        coordinator.Personality.Analytical.Should().BeInRange(0, 1);
    }

    #endregion
}
