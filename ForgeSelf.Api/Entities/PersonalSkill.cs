using System;
using System.ComponentModel;
using System.Runtime.Serialization;
using NewLife;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Entities;

/// <summary>宿主个人技能。正文是 SKILL.md；其余列服务现有 CRUD 字段。</summary>
[Serializable]
[DataObject]
[Description("个人技能。")]
[BindIndex("IU_PersonalSkill_SkillId", true, "SkillId")]
[BindTable("PersonalSkill", Description = "个人技能库", ConnName = "ForgeSelf", DbType = DatabaseType.None)]
public partial class PersonalSkill
{
    #region 属性
    private Int64 _Id;
    /// <summary>行主键</summary>
    [DisplayName("主键")]
    [Description("主键")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "主键", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _SkillId = string.Empty;
    /// <summary>对外技能 Id（skill_N），与 /api/skills 的 Id 相同</summary>
    [DisplayName("技能Id")]
    [Description("技能Id")]
    [DataObjectField(false, false, false, 64)]
    [BindColumn("SkillId", "技能Id", "", Master = true)]
    public String SkillId { get => _SkillId; set { if (OnPropertyChanging("SkillId", value)) { _SkillId = value; OnPropertyChanged("SkillId"); } } }

    private String _Name = string.Empty;
    /// <summary>名称（对应 SKILL.md frontmatter name）</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("Name", "名称", "")]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Description = string.Empty;
    /// <summary>描述（对应 SKILL.md frontmatter description）</summary>
    [DisplayName("描述")]
    [Description("描述")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Description", "描述", "")]
    public String Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private String _Category = string.Empty;
    /// <summary>分类（技能库元数据，不进入 SKILL.md）</summary>
    [DisplayName("分类")]
    [Description("分类")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Category", "分类", "")]
    public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private Boolean _IsEnabled = true;
    /// <summary>是否启用</summary>
    [DisplayName("是否启用")]
    [Description("是否启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsEnabled", "是否启用", "")]
    public Boolean IsEnabled { get => _IsEnabled; set { if (OnPropertyChanging("IsEnabled", value)) { _IsEnabled = value; OnPropertyChanged("IsEnabled"); } } }

    private String _SystemPrompt = string.Empty;
    /// <summary>系统提示，即 SKILL.md 正文</summary>
    [DisplayName("系统提示")]
    [Description("系统提示")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("SystemPrompt", "系统提示", "")]
    public String SystemPrompt { get => _SystemPrompt; set { if (OnPropertyChanging("SystemPrompt", value)) { _SystemPrompt = value; OnPropertyChanged("SystemPrompt"); } } }

    private String _ToolIdsJson = "[]";
    /// <summary>工具 Id 列表 JSON</summary>
    [DisplayName("工具列表")]
    [Description("工具列表")]
    [DataObjectField(false, false, false, -1)]
    [BindColumn("ToolIdsJson", "工具列表", "")]
    public String ToolIdsJson { get => _ToolIdsJson; set { if (OnPropertyChanging("ToolIdsJson", value)) { _ToolIdsJson = value; OnPropertyChanged("ToolIdsJson"); } } }

    private String _SettingsJson = "{}";
    /// <summary>设置 JSON</summary>
    [DisplayName("设置")]
    [Description("设置")]
    [DataObjectField(false, false, false, -1)]
    [BindColumn("SettingsJson", "设置", "")]
    public String SettingsJson { get => _SettingsJson; set { if (OnPropertyChanging("SettingsJson", value)) { _SettingsJson = value; OnPropertyChanged("SettingsJson"); } } }

    private String _SkillMarkdown = string.Empty;
    /// <summary>完整 SKILL.md 文本</summary>
    [DisplayName("SKILL.md")]
    [Description("SKILL.md")]
    [DataObjectField(false, false, false, -1)]
    [BindColumn("SkillMarkdown", "SKILL.md", "")]
    public String SkillMarkdown { get => _SkillMarkdown; set { if (OnPropertyChanging("SkillMarkdown", value)) { _SkillMarkdown = value; OnPropertyChanged("SkillMarkdown"); } } }

    private Int32 _UsageCount;
    /// <summary>使用次数</summary>
    [DisplayName("使用次数")]
    [Description("使用次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UsageCount", "使用次数", "")]
    public Int32 UsageCount { get => _UsageCount; set { if (OnPropertyChanging("UsageCount", value)) { _UsageCount = value; OnPropertyChanged("UsageCount"); } } }

    private DateTime _CreatedAt;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreatedAt", "创建时间", "")]
    public DateTime CreatedAt { get => _CreatedAt; set { if (OnPropertyChanging("CreatedAt", value)) { _CreatedAt = value; OnPropertyChanged("CreatedAt"); } } }

    private DateTime _UpdatedAt;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdatedAt", "更新时间", "")]
    public DateTime UpdatedAt { get => _UpdatedAt; set { if (OnPropertyChanging("UpdatedAt", value)) { _UpdatedAt = value; OnPropertyChanged("UpdatedAt"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <inheritdoc />
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "SkillId" => _SkillId,
            "Name" => _Name,
            "Description" => _Description,
            "Category" => _Category,
            "IsEnabled" => _IsEnabled,
            "SystemPrompt" => _SystemPrompt,
            "ToolIdsJson" => _ToolIdsJson,
            "SettingsJson" => _SettingsJson,
            "SkillMarkdown" => _SkillMarkdown,
            "UsageCount" => _UsageCount,
            "CreatedAt" => _CreatedAt,
            "UpdatedAt" => _UpdatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "SkillId": _SkillId = Convert.ToString(value) ?? string.Empty; break;
                case "Name": _Name = Convert.ToString(value) ?? string.Empty; break;
                case "Description": _Description = Convert.ToString(value) ?? string.Empty; break;
                case "Category": _Category = Convert.ToString(value) ?? string.Empty; break;
                case "IsEnabled": _IsEnabled = value.ToBoolean(); break;
                case "SystemPrompt": _SystemPrompt = Convert.ToString(value) ?? string.Empty; break;
                case "ToolIdsJson": _ToolIdsJson = Convert.ToString(value) ?? "[]"; break;
                case "SettingsJson": _SettingsJson = Convert.ToString(value) ?? "{}"; break;
                case "SkillMarkdown": _SkillMarkdown = Convert.ToString(value) ?? string.Empty; break;
                case "UsageCount": _UsageCount = value.ToInt(); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                case "UpdatedAt": _UpdatedAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 扩展查询
    /// <summary>按对外技能 Id 查找（不走实体缓存，写入后立即可读）。</summary>
    public static PersonalSkill? FindBySkillId(String skillId)
    {
        if (skillId.IsNullOrEmpty()) return null;
        if (Meta.Session.Count < MaxCacheCount)
            return Meta.Cache.Find(e => e.SkillId.EqualIgnoreCase(skillId));
        return Find(_.SkillId == skillId);
    }

    /// <summary>全部个人技能（不走实体缓存）。</summary>
    public static IList<PersonalSkill> FindAllSkills()
    {
        if (Meta.Session.Count < MaxCacheCount)
            return Meta.Cache.Entities;
        return FindAll();
    }
    #endregion

    #region 字段名
    /// <summary>取得个人技能字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>技能Id</summary>
        public static readonly Field SkillId = FindByName("SkillId");

        private static Field FindByName(String name) => Meta.Table.FindByName(name);
    }
    #endregion
}
