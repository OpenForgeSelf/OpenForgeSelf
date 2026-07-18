using System.Text.Json.Serialization;

namespace OpenForgeSelf.Backend.Plugins.AIAgent.Models;

public enum AgentType
{
    Coordinator = 0,
    Researcher = 1,
    Writer = 2,
    Programmer = 3,
    Analyst = 4,
    Critic = 5,
    Generalist = 99
}

public enum AgentStatus
{
    Idle = 0,
    Thinking = 1,
    Working = 2,
    Waiting = 3,
    Completed = 4,
    Failed = 5
}

public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum AgentTaskStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

public class AgentPersonality
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;

    public double Creativity { get; set; } = 0.5;
    public double Analytical { get; set; } = 0.5;
    public double Empathy { get; set; } = 0.5;
    public double Confidence { get; set; } = 0.5;
    public double Formality { get; set; } = 0.5;

    public string ToneStyle { get; set; } = "professional";
    public string CommunicationStyle { get; set; } = "balanced";

    public List<string> Interests { get; set; } = new();
    public List<string> Strengths { get; set; } = new();
    public List<string> Limitations { get; set; } = new();

    public string SystemPromptAddon { get; set; } = string.Empty;
}

public class AgentDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AgentType Type { get; set; } = AgentType.Generalist;
    public string Avatar { get; set; } = string.Empty;

    public AgentPersonality Personality { get; set; } = new();

    public List<string> Capabilities { get; set; } = new();
    public List<string> Tools { get; set; } = new();

    public string SystemPrompt { get; set; } = string.Empty;
    public int MaxIterations { get; set; } = 10;

    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class AgentInstance
{
    public string InstanceId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public AgentType AgentType { get; set; }
    public AgentStatus Status { get; set; } = AgentStatus.Idle;

    public string SessionId { get; set; } = string.Empty;
    public string? CurrentTaskId { get; set; }

    public List<AIChatMessage> Context { get; set; } = new();
    public Dictionary<string, object> State { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastActiveAt { get; set; } = DateTime.Now;
}

public class AgentTask
{
    public string TaskId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ParentTaskId { get; set; }

    public AgentType AssignedAgentType { get; set; } = AgentType.Generalist;
    public string? AssignedAgentId { get; set; }
    public string? AssignedAgentInstanceId { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public AgentTaskStatus Status { get; set; } = AgentTaskStatus.Pending;

    public string Input { get; set; } = string.Empty;
    public string? Output { get; set; }
    public string? ErrorMessage { get; set; }

    public List<string> SubTaskIds { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class AgentMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string FromAgentId { get; set; } = string.Empty;
    public string ToAgentId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;

    public string MessageType { get; set; } = "text";
    public string Content { get; set; } = string.Empty;

    public Dictionary<string, object>? Payload { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class AgentConversation
{
    public string ConversationId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> ParticipantAgentIds { get; set; } = new();

    public AgentTask RelatedTask { get; set; } = null!;
    public List<AgentMessage> Messages { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class AgentExecutionResult
{
    public bool Success { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;

    public string? Output { get; set; }
    public string? ErrorMessage { get; set; }

    public int Iterations { get; set; }
    public List<string> ToolsUsed { get; set; } = new();

    public TimeSpan Duration { get; set; }
}

public class CoordinatorPlan
{
    public string PlanId { get; set; } = string.Empty;
    public string OriginalRequest { get; set; } = string.Empty;

    public string Strategy { get; set; } = string.Empty;
    public List<AgentTask> Tasks { get; set; } = new();

    public string CoordinatorReasoning { get; set; } = string.Empty;
    public List<string> RequiredCapabilities { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
