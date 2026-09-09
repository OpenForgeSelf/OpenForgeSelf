using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Threading;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;

namespace ForgeSelf.Api.Plugins.AIAgent.Entities;

public partial class AgentDefinition : Entity<AgentDefinition>
{
    #region 对象操作
    private static Int32 MaxCacheCount = 1000;

    static AgentDefinition()
    {
        Meta.Interceptors.Add<TimeInterceptor>();
    }

    /// <summary>验证并修补数据。</summary>
    public override Boolean Valid(DataMethod method)
    {
        if (!HasDirty) return true;

        if (Id.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Id), "Agent ID 不能为空！");
        if (Name == null) throw new ArgumentNullException(nameof(Name), "名称不能为空！");

        if (!base.Valid(method)) return false;

        // 新建时补时间戳；更新时刷新 UpdatedAt
        var now = DateTime.Now;
        if (method == DataMethod.Insert)
        {
            if (!Dirtys[nameof(CreatedAt)]) CreatedAt = now;
            if (!Dirtys[nameof(UpdatedAt)]) UpdatedAt = now;
        }
        else if (method == DataMethod.Update)
        {
            UpdatedAt = now;
        }

        return true;
    }

    /// <summary>首次连接数据库时初始化内置 Agent（仅当表为空时 seed，不覆盖用户自定义）。</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void InitData()
    {
        if (Meta.Session.Count > 0) return;

        XTrace.Log.Info("[AgentDefinition] 表为空，初始化内置 Agent……");

        var builtIns = BuiltInAgentDefinitions.GetAll();
        foreach (var model in builtIns)
        {
            var entity = new AgentDefinition();
            entity.FromModel(model);
            entity.Insert();
        }

        XTrace.Log.Info("[AgentDefinition] 内置 Agent 初始化完成，共 {0} 个", builtIns.Count);
    }
    #endregion

    #region 模型映射（实体 ↔ Models.AgentDefinition）
    /// <summary>实体转为运行时模型（含从 ConfigJson 反序列化 Personality/Capabilities/Tools）。</summary>
    public Models.AgentDefinition ToModel()
    {
        var model = new Models.AgentDefinition
        {
            Id = Id ?? string.Empty,
            Name = Name ?? string.Empty,
            Description = Description ?? string.Empty,
            Type = (Models.AgentType)Type,
            Avatar = Avatar ?? string.Empty,
            SystemPrompt = SystemPrompt ?? string.Empty,
            MaxIterations = MaxIterations,
            IsEnabled = IsEnabled,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
        };

        if (!ConfigJson.IsNullOrEmpty())
        {
            try
            {
                var cfg = JsonSerializer.Deserialize<AgentConfigPayload>(ConfigJson);
                if (cfg != null)
                {
                    model.Personality = cfg.Personality ?? new Models.AgentPersonality();
                    model.Capabilities = cfg.Capabilities ?? new List<string>();
                    model.Tools = cfg.Tools ?? new List<string>();
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("[AgentDefinition] ConfigJson 反序列化失败（Id={0}）：{1}", Id, ex.Message);
            }
        }

        return model;
    }

    /// <summary>运行时模型写入实体（Personality/Capabilities/Tools 序列化为 ConfigJson）。</summary>
    public void FromModel(Models.AgentDefinition model)
    {
        Id = model.Id;
        Name = model.Name;
        Description = model.Description;
        Type = (Int32)model.Type;
        Avatar = model.Avatar;
        SystemPrompt = model.SystemPrompt;
        MaxIterations = model.MaxIterations;
        IsEnabled = model.IsEnabled;

        var cfg = new AgentConfigPayload
        {
            Personality = model.Personality,
            Capabilities = model.Capabilities,
            Tools = model.Tools,
        };
        ConfigJson = JsonSerializer.Serialize(cfg);
    }
    #endregion
}

/// <summary>AgentDefinition.ConfigJson 的反序列化结构。</summary>
internal class AgentConfigPayload
{
    public Models.AgentPersonality? Personality { get; set; }
    public List<string>? Capabilities { get; set; }
    public List<string>? Tools { get; set; }
}
