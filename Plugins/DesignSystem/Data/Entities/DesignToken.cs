using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.DesignSystem.Entities;

/// <summary>{name}。</summary>
[Serializable]
[DataObject]
[Description("{name}。")]
[BindIndex("IU_DesignToken_ProjectId_ThemeId_Path", true, "ProjectId,ThemeId,Path")]
[BindIndex("IX_DesignToken_ProjectId_Tier", false, "ProjectId,Tier")]
[BindIndex("IX_DesignToken_ProjectId_Group", false, "ProjectId,Group")]
[BindIndex("IX_DesignToken_ProjectId_AliasPath", false, "ProjectId,AliasPath")]
[BindIndex("IX_DesignToken_OklchH", false, "OklchH")]
[BindTable("DesignToken", Description = "设计令牌", ConnName = "DesignSystem", DbType = DatabaseType.None)]
public partial class DesignToken
{
    #region 属性
    private Int64 _Id;
    /// <summary>令牌ID</summary>
    [DisplayName("令牌ID")]
    [Description("令牌ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "令牌ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ProjectId;
    /// <summary>项目ID</summary>
    [DisplayName("项目ID")]
    [Description("项目ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目ID", "")]
    public Int64 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private Int64 _ThemeId;
    /// <summary>主题ID，0=跨主题共享层（通常为 primitive 基线）</summary>
    [DisplayName("主题ID")]
    [Description("主题ID，0=跨主题共享层（通常为 primitive 基线）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ThemeId", "主题ID，0=跨主题共享层（通常为 primitive 基线）", "", DefaultValue = "0")]
    public Int64 ThemeId { get => _ThemeId; set { if (OnPropertyChanging("ThemeId", value)) { _ThemeId = value; OnPropertyChanged("ThemeId"); } } }

    private String _Tier;
    /// <summary>层级：primitive|semantic|component</summary>
    [DisplayName("层级")]
    [Description("层级：primitive|semantic|component")]
    [DataObjectField(false, false, false, 20)]
    [BindColumn("Tier", "层级：primitive|semantic|component", "", DefaultValue = "primitive")]
    public String Tier { get => _Tier; set { if (OnPropertyChanging("Tier", value)) { _Tier = value; OnPropertyChanged("Tier"); } } }

    private String _Path;
    /// <summary>点分小写kebab路径，如 color.brand.500</summary>
    [DisplayName("点分小写kebab路径")]
    [Description("点分小写kebab路径，如 color.brand.500")]
    [DataObjectField(false, false, false, 200)]
    [BindColumn("Path", "点分小写kebab路径，如 color.brand.500", "")]
    public String Path { get => _Path; set { if (OnPropertyChanging("Path", value)) { _Path = value; OnPropertyChanged("Path"); } } }

    private String _Name;
    /// <summary>显示名</summary>
    [DisplayName("显示名")]
    [Description("显示名")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Name", "显示名", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Type;
    /// <summary>DTCG $type：color|dimension|fontFamily|fontWeight|duration|cubicBezier|number|string|shadow|border|gradient|typography|transition|strokeStyle</summary>
    [DisplayName("DTCGtype")]
    [Description("DTCG $type：color|dimension|fontFamily|fontWeight|duration|cubicBezier|number|string|shadow|border|gradient|typography|transition|strokeStyle")]
    [DataObjectField(false, false, true, 30)]
    [BindColumn("Type", "DTCG $type：color|dimension|fontFamily|fontWeight|duration|cubicBezier|number|string|shadow|border|gradient|typography|transition|strokeStyle", "", DefaultValue = "color")]
    public String Type { get => _Type; set { if (OnPropertyChanging("Type", value)) { _Type = value; OnPropertyChanged("Type"); } } }

    private String _Value;
    /// <summary>解析后的最终值（hex/oklch()/px 等字符串形态）</summary>
    [DisplayName("解析后的最终值（hex_oklch()_px等字符串形态）")]
    [Description("解析后的最终值（hex/oklch()/px 等字符串形态）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Value", "解析后的最终值（hex/oklch()/px 等字符串形态）", "")]
    public String Value { get => _Value; set { if (OnPropertyChanging("Value", value)) { _Value = value; OnPropertyChanged("Value"); } } }

    private String _ValueJson;
    /// <summary>结构化/复合值（JSON）。复合令牌以此为真源</summary>
    [DisplayName("结构化_复合值（JSON）")]
    [Description("结构化/复合值（JSON）。复合令牌以此为真源")]
    [DataObjectField(false, false, true, 4000)]
    [BindColumn("ValueJson", "结构化/复合值（JSON）。复合令牌以此为真源", "")]
    public String ValueJson { get => _ValueJson; set { if (OnPropertyChanging("ValueJson", value)) { _ValueJson = value; OnPropertyChanged("ValueJson"); } } }

    private String _AliasPath;
    /// <summary>别名指向的令牌路径，禁止环与逆向引用</summary>
    [DisplayName("别名指向的令牌路径")]
    [Description("别名指向的令牌路径，禁止环与逆向引用")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("AliasPath", "别名指向的令牌路径，禁止环与逆向引用", "")]
    public String AliasPath { get => _AliasPath; set { if (OnPropertyChanging("AliasPath", value)) { _AliasPath = value; OnPropertyChanged("AliasPath"); } } }

    private String _Group;
    /// <summary>分组（导出层级用），如 brand/spacing/shadow</summary>
    [DisplayName("分组（导出层级用）")]
    [Description("分组（导出层级用），如 brand/spacing/shadow")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Group", "分组（导出层级用），如 brand/spacing/shadow", "")]
    public String Group { get => _Group; set { if (OnPropertyChanging("Group", value)) { _Group = value; OnPropertyChanged("Group"); } } }

    private String _Description;
    /// <summary>说明与用途</summary>
    [DisplayName("说明与用途")]
    [Description("说明与用途")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("Description", "说明与用途", "")]
    public String Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private String _ColorHex;
    /// <summary>解析后 sRGB hex，供对比度数学与外部 lint</summary>
    [DisplayName("解析后sRGBhex")]
    [Description("解析后 sRGB hex，供对比度数学与外部 lint")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("ColorHex", "解析后 sRGB hex，供对比度数学与外部 lint", "")]
    public String ColorHex { get => _ColorHex; set { if (OnPropertyChanging("ColorHex", value)) { _ColorHex = value; OnPropertyChanged("ColorHex"); } } }

    private Double _OklchL;
    /// <summary>OKLCH 明度 L（拆列以便 SQL 插值/检索）</summary>
    [DisplayName("OKLCH明度L（拆列以便SQL插值_检索）")]
    [Description("OKLCH 明度 L（拆列以便 SQL 插值/检索）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OklchL", "OKLCH 明度 L（拆列以便 SQL 插值/检索）", "")]
    public Double OklchL { get => _OklchL; set { if (OnPropertyChanging("OklchL", value)) { _OklchL = value; OnPropertyChanged("OklchL"); } } }

    private Double _OklchC;
    /// <summary>OKLCH 彩度 C</summary>
    [DisplayName("OKLCH彩度C")]
    [Description("OKLCH 彩度 C")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OklchC", "OKLCH 彩度 C", "")]
    public Double OklchC { get => _OklchC; set { if (OnPropertyChanging("OklchC", value)) { _OklchC = value; OnPropertyChanged("OklchC"); } } }

    private Double _OklchH;
    /// <summary>OKLCH 色相 H</summary>
    [DisplayName("OKLCH色相H")]
    [Description("OKLCH 色相 H")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OklchH", "OKLCH 色相 H", "")]
    public Double OklchH { get => _OklchH; set { if (OnPropertyChanging("OklchH", value)) { _OklchH = value; OnPropertyChanged("OklchH"); } } }

    private Double _Alpha;
    /// <summary>透明度 0~1</summary>
    [DisplayName("透明度0~1")]
    [Description("透明度 0~1")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Alpha", "透明度 0~1", "", DefaultValue = "1")]
    public Double Alpha { get => _Alpha; set { if (OnPropertyChanging("Alpha", value)) { _Alpha = value; OnPropertyChanged("Alpha"); } } }

    private String _ColorSpace;
    /// <summary>色彩空间：oklch|srgb|display-p3</summary>
    [DisplayName("色彩空间")]
    [Description("色彩空间：oklch|srgb|display-p3")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("ColorSpace", "色彩空间：oklch|srgb|display-p3", "", DefaultValue = "oklch")]
    public String ColorSpace { get => _ColorSpace; set { if (OnPropertyChanging("ColorSpace", value)) { _ColorSpace = value; OnPropertyChanged("ColorSpace"); } } }

    private Boolean _IsPrimary;
    /// <summary>是否该族主色</summary>
    [DisplayName("是否该族主色")]
    [Description("是否该族主色")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsPrimary", "是否该族主色", "", DefaultValue = "0")]
    public Boolean IsPrimary { get => _IsPrimary; set { if (OnPropertyChanging("IsPrimary", value)) { _IsPrimary = value; OnPropertyChanged("IsPrimary"); } } }

    private String _ContrastOn;
    /// <summary>对比度计算所对的前景/背景配对路径</summary>
    [DisplayName("对比度计算所对的前景_背景配对路径")]
    [Description("对比度计算所对的前景/背景配对路径")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("ContrastOn", "对比度计算所对的前景/背景配对路径", "")]
    public String ContrastOn { get => _ContrastOn; set { if (OnPropertyChanging("ContrastOn", value)) { _ContrastOn = value; OnPropertyChanged("ContrastOn"); } } }

    private Double _ContrastRatio;
    /// <summary>WCAG 2.2 对比度比率，未计算为 -1</summary>
    [DisplayName("WCAG2")]
    [Description("WCAG 2.2 对比度比率，未计算为 -1")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ContrastRatio", "WCAG 2.2 对比度比率，未计算为 -1", "")]
    public Double ContrastRatio { get => _ContrastRatio; set { if (OnPropertyChanging("ContrastRatio", value)) { _ContrastRatio = value; OnPropertyChanged("ContrastRatio"); } } }

    private String _WcagLevel;
    /// <summary>达标等级：none|aa|aaa</summary>
    [DisplayName("达标等级")]
    [Description("达标等级：none|aa|aaa")]
    [DataObjectField(false, false, true, 10)]
    [BindColumn("WcagLevel", "达标等级：none|aa|aaa", "")]
    public String WcagLevel { get => _WcagLevel; set { if (OnPropertyChanging("WcagLevel", value)) { _WcagLevel = value; OnPropertyChanged("WcagLevel"); } } }

    private Double _ApcaLc;
    /// <summary>APCA Lc 参考读数（不作门禁，仅参考）</summary>
    [DisplayName("APCALc参考读数（不作门禁")]
    [Description("APCA Lc 参考读数（不作门禁，仅参考）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ApcaLc", "APCA Lc 参考读数（不作门禁，仅参考）", "")]
    public Double ApcaLc { get => _ApcaLc; set { if (OnPropertyChanging("ApcaLc", value)) { _ApcaLc = value; OnPropertyChanged("ApcaLc"); } } }

    private String _Generator;
    /// <summary>来源：oklch-ramp|manual|imported|derived</summary>
    [DisplayName("来源")]
    [Description("来源：oklch-ramp|manual|imported|derived")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Generator", "来源：oklch-ramp|manual|imported|derived", "", DefaultValue = "manual")]
    public String Generator { get => _Generator; set { if (OnPropertyChanging("Generator", value)) { _Generator = value; OnPropertyChanged("Generator"); } } }

    private String _GeneratorSeed;
    /// <summary>生成种子（同输入可复现）</summary>
    [DisplayName("生成种子（同输入可复现）")]
    [Description("生成种子（同输入可复现）")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("GeneratorSeed", "生成种子（同输入可复现）", "")]
    public String GeneratorSeed { get => _GeneratorSeed; set { if (OnPropertyChanging("GeneratorSeed", value)) { _GeneratorSeed = value; OnPropertyChanged("GeneratorSeed"); } } }

    private String _GeneratorVersion;
    /// <summary>生成器版本</summary>
    [DisplayName("生成器版本")]
    [Description("生成器版本")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("GeneratorVersion", "生成器版本", "")]
    public String GeneratorVersion { get => _GeneratorVersion; set { if (OnPropertyChanging("GeneratorVersion", value)) { _GeneratorVersion = value; OnPropertyChanged("GeneratorVersion"); } } }

    private String _Lifecycle;
    /// <summary>治理生命周期：proposed|adopted|deprecated|removed</summary>
    [DisplayName("治理生命周期")]
    [Description("治理生命周期：proposed|adopted|deprecated|removed")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Lifecycle", "治理生命周期：proposed|adopted|deprecated|removed", "", DefaultValue = "adopted")]
    public String Lifecycle { get => _Lifecycle; set { if (OnPropertyChanging("Lifecycle", value)) { _Lifecycle = value; OnPropertyChanged("Lifecycle"); } } }

    private String _ReplacedBy;
    /// <summary>废弃后替代令牌路径</summary>
    [DisplayName("废弃后替代令牌路径")]
    [Description("废弃后替代令牌路径")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("ReplacedBy", "废弃后替代令牌路径", "")]
    public String ReplacedBy { get => _ReplacedBy; set { if (OnPropertyChanging("ReplacedBy", value)) { _ReplacedBy = value; OnPropertyChanged("ReplacedBy"); } } }

    private Boolean _Deprecated;
    /// <summary>是否已废弃</summary>
    [DisplayName("是否已废弃")]
    [Description("是否已废弃")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Deprecated", "是否已废弃", "", DefaultValue = "0")]
    public Boolean Deprecated { get => _Deprecated; set { if (OnPropertyChanging("Deprecated", value)) { _Deprecated = value; OnPropertyChanged("Deprecated"); } } }

    private String _Tags;
    /// <summary>标签，逗号分隔，供检索</summary>
    [DisplayName("标签")]
    [Description("标签，逗号分隔，供检索")]
    [DataObjectField(false, false, true, 300)]
    [BindColumn("Tags", "标签，逗号分隔，供检索", "")]
    public String Tags { get => _Tags; set { if (OnPropertyChanging("Tags", value)) { _Tags = value; OnPropertyChanged("Tags"); } } }

    private Int32 _SortOrder;
    /// <summary>排序</summary>
    [DisplayName("排序")]
    [Description("排序")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SortOrder", "排序", "")]
    public Int32 SortOrder { get => _SortOrder; set { if (OnPropertyChanging("SortOrder", value)) { _SortOrder = value; OnPropertyChanged("SortOrder"); } } }

    private String _Extensions;
    /// <summary>DTCG $extensions 与厂商元数据（JSON）</summary>
    [DisplayName("DTCGextensions与厂商元数据（JSON）")]
    [Description("DTCG $extensions 与厂商元数据（JSON）")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Extensions", "DTCG $extensions 与厂商元数据（JSON）", "")]
    public String Extensions { get => _Extensions; set { if (OnPropertyChanging("Extensions", value)) { _Extensions = value; OnPropertyChanged("Extensions"); } } }

    private DateTime _CreatedAt;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreatedAt", "创建时间", "")]
    public DateTime CreatedAt { get => _CreatedAt; set { if (OnPropertyChanging("CreatedAt", value)) { _CreatedAt = value; OnPropertyChanged("CreatedAt"); } } }

    private DateTime _UpdatedAt;
    /// <summary>更新时间，兼作乐观并发依据</summary>
    [DisplayName("更新时间")]
    [Description("更新时间，兼作乐观并发依据")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdatedAt", "更新时间，兼作乐观并发依据", "")]
    public DateTime UpdatedAt { get => _UpdatedAt; set { if (OnPropertyChanging("UpdatedAt", value)) { _UpdatedAt = value; OnPropertyChanged("UpdatedAt"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "ProjectId" => _ProjectId,
            "ThemeId" => _ThemeId,
            "Tier" => _Tier,
            "Path" => _Path,
            "Name" => _Name,
            "Type" => _Type,
            "Value" => _Value,
            "ValueJson" => _ValueJson,
            "AliasPath" => _AliasPath,
            "Group" => _Group,
            "Description" => _Description,
            "ColorHex" => _ColorHex,
            "OklchL" => _OklchL,
            "OklchC" => _OklchC,
            "OklchH" => _OklchH,
            "Alpha" => _Alpha,
            "ColorSpace" => _ColorSpace,
            "IsPrimary" => _IsPrimary,
            "ContrastOn" => _ContrastOn,
            "ContrastRatio" => _ContrastRatio,
            "WcagLevel" => _WcagLevel,
            "ApcaLc" => _ApcaLc,
            "Generator" => _Generator,
            "GeneratorSeed" => _GeneratorSeed,
            "GeneratorVersion" => _GeneratorVersion,
            "Lifecycle" => _Lifecycle,
            "ReplacedBy" => _ReplacedBy,
            "Deprecated" => _Deprecated,
            "Tags" => _Tags,
            "SortOrder" => _SortOrder,
            "Extensions" => _Extensions,
            "CreatedAt" => _CreatedAt,
            "UpdatedAt" => _UpdatedAt,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "ProjectId": _ProjectId = value.ToLong(); break;
                case "ThemeId": _ThemeId = value.ToLong(); break;
                case "Tier": _Tier = Convert.ToString(value); break;
                case "Path": _Path = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Type": _Type = Convert.ToString(value); break;
                case "Value": _Value = Convert.ToString(value); break;
                case "ValueJson": _ValueJson = Convert.ToString(value); break;
                case "AliasPath": _AliasPath = Convert.ToString(value); break;
                case "Group": _Group = Convert.ToString(value); break;
                case "Description": _Description = Convert.ToString(value); break;
                case "ColorHex": _ColorHex = Convert.ToString(value); break;
                case "OklchL": _OklchL = value.ToDouble(); break;
                case "OklchC": _OklchC = value.ToDouble(); break;
                case "OklchH": _OklchH = value.ToDouble(); break;
                case "Alpha": _Alpha = value.ToDouble(); break;
                case "ColorSpace": _ColorSpace = Convert.ToString(value); break;
                case "IsPrimary": _IsPrimary = value.ToBoolean(); break;
                case "ContrastOn": _ContrastOn = Convert.ToString(value); break;
                case "ContrastRatio": _ContrastRatio = value.ToDouble(); break;
                case "WcagLevel": _WcagLevel = Convert.ToString(value); break;
                case "ApcaLc": _ApcaLc = value.ToDouble(); break;
                case "Generator": _Generator = Convert.ToString(value); break;
                case "GeneratorSeed": _GeneratorSeed = Convert.ToString(value); break;
                case "GeneratorVersion": _GeneratorVersion = Convert.ToString(value); break;
                case "Lifecycle": _Lifecycle = Convert.ToString(value); break;
                case "ReplacedBy": _ReplacedBy = Convert.ToString(value); break;
                case "Deprecated": _Deprecated = value.ToBoolean(); break;
                case "Tags": _Tags = Convert.ToString(value); break;
                case "SortOrder": _SortOrder = value.ToInt(); break;
                case "Extensions": _Extensions = Convert.ToString(value); break;
                case "CreatedAt": _CreatedAt = value.ToDateTime(); break;
                case "UpdatedAt": _UpdatedAt = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据令牌ID查找</summary>
    /// <param name="id">令牌ID</param>
    /// <returns>实体对象</returns>
    public static DesignToken FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据项目ID、主题ID、点分小写kebab路径查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="themeId">主题ID</param>
    /// <param name="path">点分小写kebab路径</param>
    /// <returns>实体对象</returns>
    public static DesignToken FindByProjectIdAndThemeIdAndPath(Int64 projectId, Int64 themeId, String path)
    {
        if (projectId < 0) return null;
        if (themeId < 0) return null;
        if (path.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.ProjectId == projectId && e.ThemeId == themeId && e.Path.EqualIgnoreCase(path));

        return Find(_.ProjectId == projectId & _.ThemeId == themeId & _.Path == path);
    }

    /// <summary>根据项目ID查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <returns>实体列表</returns>
    public static IList<DesignToken> FindAllByProjectId(Int64 projectId)
    {
        if (projectId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }

    /// <summary>根据项目ID、主题ID查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="themeId">主题ID</param>
    /// <returns>实体列表</returns>
    public static IList<DesignToken> FindAllByProjectIdAndThemeId(Int64 projectId, Int64 themeId)
    {
        if (projectId < 0) return [];
        if (themeId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId && e.ThemeId == themeId);

        return FindAll(_.ProjectId == projectId & _.ThemeId == themeId);
    }

    /// <summary>根据项目ID、层级查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="tier">层级</param>
    /// <returns>实体列表</returns>
    public static IList<DesignToken> FindAllByProjectIdAndTier(Int64 projectId, String tier)
    {
        if (projectId < 0) return [];
        if (tier.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId && e.Tier.EqualIgnoreCase(tier));

        return FindAll(_.ProjectId == projectId & _.Tier == tier);
    }

    /// <summary>根据项目ID、分组（导出层级用）查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="@group">分组（导出层级用）</param>
    /// <returns>实体列表</returns>
    public static IList<DesignToken> FindAllByProjectIdAndGroup(Int64 projectId, String @group)
    {
        if (projectId < 0) return [];
        if (@group.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId && e.Group.EqualIgnoreCase(@group));

        return FindAll(_.ProjectId == projectId & _.Group == @group);
    }

    /// <summary>根据项目ID、别名指向的令牌路径查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="aliasPath">别名指向的令牌路径</param>
    /// <returns>实体列表</returns>
    public static IList<DesignToken> FindAllByProjectIdAndAliasPath(Int64 projectId, String aliasPath)
    {
        if (projectId < 0) return [];
        if (aliasPath.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId && e.AliasPath.EqualIgnoreCase(aliasPath));

        return FindAll(_.ProjectId == projectId & _.AliasPath == aliasPath);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="themeId">主题ID，0=跨主题共享层（通常为 primitive 基线）</param>
    /// <param name="tier">层级：primitive|semantic|component</param>
    /// <param name="path">点分小写kebab路径，如 color.brand.500</param>
    /// <param name="aliasPath">别名指向的令牌路径，禁止环与逆向引用</param>
    /// <param name="@group">分组（导出层级用），如 brand/spacing/shadow</param>
    /// <param name="oklchH">OKLCH 色相 H</param>
    /// <param name="isPrimary">是否该族主色</param>
    /// <param name="deprecated">是否已废弃</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DesignToken> Search(Int64 projectId, Int64 themeId, String tier, String path, String aliasPath, String @group, Double oklchH, Boolean? isPrimary, Boolean? deprecated, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (themeId >= 0) exp &= _.ThemeId == themeId;
        if (!tier.IsNullOrEmpty()) exp &= _.Tier == tier;
        if (!path.IsNullOrEmpty()) exp &= _.Path == path;
        if (!aliasPath.IsNullOrEmpty()) exp &= _.AliasPath == aliasPath;
        if (!@group.IsNullOrEmpty()) exp &= _.Group == @group;
        if (isPrimary != null) exp &= _.IsPrimary == isPrimary;
        if (deprecated != null) exp &= _.Deprecated == deprecated;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得设计令牌字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>令牌ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目ID</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>主题ID，0=跨主题共享层（通常为 primitive 基线）</summary>
        public static readonly Field ThemeId = FindByName("ThemeId");

        /// <summary>层级：primitive|semantic|component</summary>
        public static readonly Field Tier = FindByName("Tier");

        /// <summary>点分小写kebab路径，如 color.brand.500</summary>
        public static readonly Field Path = FindByName("Path");

        /// <summary>显示名</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>DTCG $type：color|dimension|fontFamily|fontWeight|duration|cubicBezier|number|string|shadow|border|gradient|typography|transition|strokeStyle</summary>
        public static readonly Field Type = FindByName("Type");

        /// <summary>解析后的最终值（hex/oklch()/px 等字符串形态）</summary>
        public static readonly Field Value = FindByName("Value");

        /// <summary>结构化/复合值（JSON）。复合令牌以此为真源</summary>
        public static readonly Field ValueJson = FindByName("ValueJson");

        /// <summary>别名指向的令牌路径，禁止环与逆向引用</summary>
        public static readonly Field AliasPath = FindByName("AliasPath");

        /// <summary>分组（导出层级用），如 brand/spacing/shadow</summary>
        public static readonly Field Group = FindByName("Group");

        /// <summary>说明与用途</summary>
        public static readonly Field Description = FindByName("Description");

        /// <summary>解析后 sRGB hex，供对比度数学与外部 lint</summary>
        public static readonly Field ColorHex = FindByName("ColorHex");

        /// <summary>OKLCH 明度 L（拆列以便 SQL 插值/检索）</summary>
        public static readonly Field OklchL = FindByName("OklchL");

        /// <summary>OKLCH 彩度 C</summary>
        public static readonly Field OklchC = FindByName("OklchC");

        /// <summary>OKLCH 色相 H</summary>
        public static readonly Field OklchH = FindByName("OklchH");

        /// <summary>透明度 0~1</summary>
        public static readonly Field Alpha = FindByName("Alpha");

        /// <summary>色彩空间：oklch|srgb|display-p3</summary>
        public static readonly Field ColorSpace = FindByName("ColorSpace");

        /// <summary>是否该族主色</summary>
        public static readonly Field IsPrimary = FindByName("IsPrimary");

        /// <summary>对比度计算所对的前景/背景配对路径</summary>
        public static readonly Field ContrastOn = FindByName("ContrastOn");

        /// <summary>WCAG 2.2 对比度比率，未计算为 -1</summary>
        public static readonly Field ContrastRatio = FindByName("ContrastRatio");

        /// <summary>达标等级：none|aa|aaa</summary>
        public static readonly Field WcagLevel = FindByName("WcagLevel");

        /// <summary>APCA Lc 参考读数（不作门禁，仅参考）</summary>
        public static readonly Field ApcaLc = FindByName("ApcaLc");

        /// <summary>来源：oklch-ramp|manual|imported|derived</summary>
        public static readonly Field Generator = FindByName("Generator");

        /// <summary>生成种子（同输入可复现）</summary>
        public static readonly Field GeneratorSeed = FindByName("GeneratorSeed");

        /// <summary>生成器版本</summary>
        public static readonly Field GeneratorVersion = FindByName("GeneratorVersion");

        /// <summary>治理生命周期：proposed|adopted|deprecated|removed</summary>
        public static readonly Field Lifecycle = FindByName("Lifecycle");

        /// <summary>废弃后替代令牌路径</summary>
        public static readonly Field ReplacedBy = FindByName("ReplacedBy");

        /// <summary>是否已废弃</summary>
        public static readonly Field Deprecated = FindByName("Deprecated");

        /// <summary>标签，逗号分隔，供检索</summary>
        public static readonly Field Tags = FindByName("Tags");

        /// <summary>排序</summary>
        public static readonly Field SortOrder = FindByName("SortOrder");

        /// <summary>DTCG $extensions 与厂商元数据（JSON）</summary>
        public static readonly Field Extensions = FindByName("Extensions");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        /// <summary>更新时间，兼作乐观并发依据</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得设计令牌字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>令牌ID</summary>
        public const String Id = "Id";

        /// <summary>项目ID</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>主题ID，0=跨主题共享层（通常为 primitive 基线）</summary>
        public const String ThemeId = "ThemeId";

        /// <summary>层级：primitive|semantic|component</summary>
        public const String Tier = "Tier";

        /// <summary>点分小写kebab路径，如 color.brand.500</summary>
        public const String Path = "Path";

        /// <summary>显示名</summary>
        public const String Name = "Name";

        /// <summary>DTCG $type：color|dimension|fontFamily|fontWeight|duration|cubicBezier|number|string|shadow|border|gradient|typography|transition|strokeStyle</summary>
        public const String Type = "Type";

        /// <summary>解析后的最终值（hex/oklch()/px 等字符串形态）</summary>
        public const String Value = "Value";

        /// <summary>结构化/复合值（JSON）。复合令牌以此为真源</summary>
        public const String ValueJson = "ValueJson";

        /// <summary>别名指向的令牌路径，禁止环与逆向引用</summary>
        public const String AliasPath = "AliasPath";

        /// <summary>分组（导出层级用），如 brand/spacing/shadow</summary>
        public const String Group = "Group";

        /// <summary>说明与用途</summary>
        public const String Description = "Description";

        /// <summary>解析后 sRGB hex，供对比度数学与外部 lint</summary>
        public const String ColorHex = "ColorHex";

        /// <summary>OKLCH 明度 L（拆列以便 SQL 插值/检索）</summary>
        public const String OklchL = "OklchL";

        /// <summary>OKLCH 彩度 C</summary>
        public const String OklchC = "OklchC";

        /// <summary>OKLCH 色相 H</summary>
        public const String OklchH = "OklchH";

        /// <summary>透明度 0~1</summary>
        public const String Alpha = "Alpha";

        /// <summary>色彩空间：oklch|srgb|display-p3</summary>
        public const String ColorSpace = "ColorSpace";

        /// <summary>是否该族主色</summary>
        public const String IsPrimary = "IsPrimary";

        /// <summary>对比度计算所对的前景/背景配对路径</summary>
        public const String ContrastOn = "ContrastOn";

        /// <summary>WCAG 2.2 对比度比率，未计算为 -1</summary>
        public const String ContrastRatio = "ContrastRatio";

        /// <summary>达标等级：none|aa|aaa</summary>
        public const String WcagLevel = "WcagLevel";

        /// <summary>APCA Lc 参考读数（不作门禁，仅参考）</summary>
        public const String ApcaLc = "ApcaLc";

        /// <summary>来源：oklch-ramp|manual|imported|derived</summary>
        public const String Generator = "Generator";

        /// <summary>生成种子（同输入可复现）</summary>
        public const String GeneratorSeed = "GeneratorSeed";

        /// <summary>生成器版本</summary>
        public const String GeneratorVersion = "GeneratorVersion";

        /// <summary>治理生命周期：proposed|adopted|deprecated|removed</summary>
        public const String Lifecycle = "Lifecycle";

        /// <summary>废弃后替代令牌路径</summary>
        public const String ReplacedBy = "ReplacedBy";

        /// <summary>是否已废弃</summary>
        public const String Deprecated = "Deprecated";

        /// <summary>标签，逗号分隔，供检索</summary>
        public const String Tags = "Tags";

        /// <summary>排序</summary>
        public const String SortOrder = "SortOrder";

        /// <summary>DTCG $extensions 与厂商元数据（JSON）</summary>
        public const String Extensions = "Extensions";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";

        /// <summary>更新时间，兼作乐观并发依据</summary>
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}
