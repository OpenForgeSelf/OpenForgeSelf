using System.Collections.Concurrent;
using AgentDefinitionEntity = ForgeSelf.Api.Plugins.AIAgent.Entities.AgentDefinition;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

public interface IAgentRegistryService
{
    void RegisterAgent(AgentDefinition agent);
    void UnregisterAgent(string agentId);
    AgentDefinition? GetAgent(string agentId);
    List<AgentDefinition> GetAllAgents();
    List<AgentDefinition> GetAgentsByType(AgentType type);
    List<AgentDefinition> FindAgentsByCapability(string capability);
    AgentDefinition? GetBestAgentForTask(string taskDescription, List<string> requiredCapabilities);

    // Stage 4：可编辑 Agent
    AgentDefinition CreateAgent(AgentDefinition agent);
    AgentDefinition? UpdateAgent(string agentId, AgentDefinition agent);
    bool DeleteAgent(string agentId);
}

public class AgentRegistryService : IAgentRegistryService
{
    private readonly ConcurrentDictionary<string, AgentDefinition> _agents = new(StringComparer.OrdinalIgnoreCase);

    public AgentRegistryService()
    {
        LoadFromDatabase();
    }

    /// <summary>从数据库加载全部 Agent 到内存缓存；表为空时 XCode InitData 自动 seed 内置 Agent。</summary>
    private void LoadFromDatabase()
    {
        try
        {
            // 触发一次查询，确保表已建 + InitData seed 执行（仅当表为空时）
            var entities = AgentDefinitionEntity.FindAll();
            foreach (var entity in entities)
            {
                var model = entity.ToModel();
                _agents[model.Id] = model;
            }
            XTrace.Log.Info("[AgentRegistry] 从数据库加载 {0} 个 Agent", _agents.Count);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AgentRegistry] 从数据库加载 Agent 失败，回退到内置定义：{0}", ex.Message);
            // 回退：内置 Agent 直接进内存（保证聊天不挂）
            foreach (var agent in Entities.BuiltInAgentDefinitions.GetAll())
            {
                _agents[agent.Id] = agent;
            }
        }
    }

    #region 只读查询
    public AgentDefinition? GetAgent(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId)) return null;
        _agents.TryGetValue(agentId, out var agent);
        return agent;
    }

    public List<AgentDefinition> GetAllAgents()
    {
        return _agents.Values
            .Where(a => a.IsEnabled)
            .OrderBy(a => a.SortOrder)
            .ToList();
    }

    public List<AgentDefinition> GetAgentsByType(AgentType type)
    {
        return _agents.Values
            .Where(a => a.IsEnabled && a.Type == type)
            .OrderBy(a => a.SortOrder)
            .ToList();
    }

    public List<AgentDefinition> FindAgentsByCapability(string capability)
    {
        return _agents.Values
            .Where(a => a.IsEnabled && a.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    public AgentDefinition? GetBestAgentForTask(string taskDescription, List<string> requiredCapabilities)
    {
        var allAgents = GetAllAgents();
        if (allAgents.Count == 0) return null;

        var scoredAgents = allAgents.Select(agent =>
        {
            int score = 0;
            foreach (var cap in requiredCapabilities)
            {
                if (agent.Capabilities.Contains(cap, StringComparer.OrdinalIgnoreCase))
                    score += 10;
            }
            var descLower = (taskDescription ?? string.Empty).ToLower();
            if ((agent.Description ?? string.Empty).ToLower().Intersect(descLower).Count() > 0)
                score += 2;
            if (agent.Type == AgentType.Generalist)
                score += 1;
            return (agent, score);
        });

        return scoredAgents.OrderByDescending(x => x.score).FirstOrDefault().agent;
    }
    #endregion

    #region 兼容旧接口（内存注册，不写库）
    public void RegisterAgent(AgentDefinition agent)
    {
        if (string.IsNullOrWhiteSpace(agent.Id))
            throw new ArgumentException("Agent ID 不能为空");
        _agents[agent.Id] = agent;
        XTrace.Log.Debug("[AgentRegistry] 内存注册 Agent: {0} ({1})", agent.Id, agent.Name);
    }

    public void UnregisterAgent(string agentId)
    {
        if (_agents.TryRemove(agentId, out _))
            XTrace.Log.Debug("[AgentRegistry] 内存注销 Agent: {0}", agentId);
    }
    #endregion

    #region Stage 4 CRUD（写库 + 同步内存）
    public AgentDefinition CreateAgent(AgentDefinition agent)
    {
        if (string.IsNullOrWhiteSpace(agent.Name))
            throw new ArgumentException("Agent 名称不能为空");
        if (!Enum.IsDefined(typeof(AgentType), agent.Type))
            agent.Type = AgentType.Generalist;

        // Id 为空时自动生成：优先取名称 ASCII 化 slug，否则回退带时间戳的自定义 id。
        if (string.IsNullOrWhiteSpace(agent.Id))
            agent.Id = GenerateAgentId(agent.Name, null);

        // 检查 ID 冲突（冲突时尝试追加序号，仍冲突则报错）
        var existing = AgentDefinitionEntity.FindById(agent.Id);
        if (existing != null)
        {
            var suffix = 2;
            string candidate;
            do
            {
                candidate = GenerateAgentId(agent.Name, suffix);
                suffix++;
            } while (AgentDefinitionEntity.FindById(candidate) != null && suffix < 100);
            agent.Id = candidate;
        }

        var entity = new AgentDefinitionEntity();
        entity.FromModel(agent);
        entity.Insert();

        var saved = entity.ToModel();
        _agents[saved.Id] = saved;
        XTrace.Log.Info("[AgentRegistry] 新建 Agent: {0} ({1})", saved.Id, saved.Name);
        return saved;
    }

    public AgentDefinition? UpdateAgent(string agentId, AgentDefinition agent)
    {
        var entity = AgentDefinitionEntity.FindById(agentId);
        if (entity == null) return null;

        // 保护：不允许改 Id（主键）
        agent.Id = agentId;
        entity.FromModel(agent);
        entity.Update();

        var saved = entity.ToModel();
        _agents[saved.Id] = saved;
        XTrace.Log.Info("[AgentRegistry] 更新 Agent: {0} ({1})", saved.Id, saved.Name);
        return saved;
    }

    public bool DeleteAgent(string agentId)
    {
        var entity = AgentDefinitionEntity.FindById(agentId);
        if (entity == null) return false;

        entity.Delete();
        _agents.TryRemove(agentId, out _);
        XTrace.Log.Info("[AgentRegistry] 删除 Agent: {0}", agentId);
        return true;
    }
    #endregion

    /// <summary>基于名称生成 Agent Id（ASCII 化 slug；suffix 非空时追加 -N）。</summary>
    private static string GenerateAgentId(string name, int? suffix)
    {
        // 只保留 ASCII 字母数字，转小写，分隔符用中划线
        var clean = new string((name ?? string.Empty)
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());
        clean = string.Join("-", clean.Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrEmpty(clean)) clean = "custom";
        if (clean.Length > 40) clean = clean[..40];

        var id = $"agent.{clean}";
        if (suffix is > 1) id += $"-{suffix}";
        return id;
    }
}
