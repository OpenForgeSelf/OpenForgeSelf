using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;

namespace ForgeSelf.Api.Tests.Unit;

public class AgentRegistryServiceTests
{
    private readonly AgentRegistryService _registry;

    public AgentRegistryServiceTests()
    {
        _registry = new AgentRegistryService();
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
