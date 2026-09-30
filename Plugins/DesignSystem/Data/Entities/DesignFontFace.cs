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
[BindIndex("IU_DesignFontFace_ProjectId_Family_Weight_Style", true, "ProjectId,Family,Weight,Style")]
[BindIndex("IX_DesignFontFace_Role", false, "Role")]
[BindTable("DesignFontFace", Description = "字体资产", ConnName = "DesignSystem", DbType = DatabaseType.None)]
public partial class DesignFontFace
{
    #region 属性
    private Int64 _Id;
    /// <summary>字体ID</summary>
    [DisplayName("字体ID")]
    [Description("字体ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "字体ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ProjectId;
    /// <summary>项目ID；0=共享登记</summary>
    [DisplayName("项目ID")]
    [Description("项目ID；0=共享登记")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目ID；0=共享登记", "", DefaultValue = "0")]
    public Int64 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private String _Family;
    /// <summary>字族名</summary>
    [DisplayName("字族名")]
    [Description("字族名")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("Family", "字族名", "", Master = true)]
    public String Family { get => _Family; set { if (OnPropertyChanging("Family", value)) { _Family = value; OnPropertyChanged("Family"); } } }

    private Int32 _Weight;
    /// <summary>字重</summary>
    [DisplayName("字重")]
    [Description("字重")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Weight", "字重", "", DefaultValue = "400")]
    public Int32 Weight { get => _Weight; set { if (OnPropertyChanging("Weight", value)) { _Weight = value; OnPropertyChanged("Weight"); } } }

    private String _Style;
    /// <summary>样式：normal|italic</summary>
    [DisplayName("样式")]
    [Description("样式：normal|italic")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Style", "样式：normal|italic", "", DefaultValue = "normal")]
    public String Style { get => _Style; set { if (OnPropertyChanging("Style", value)) { _Style = value; OnPropertyChanged("Style"); } } }

    private String _FileName;
    /// <summary>字体文件名</summary>
    [DisplayName("字体文件名")]
    [Description("字体文件名")]
    [DataObjectField(false, false, true, 300)]
    [BindColumn("FileName", "字体文件名", "")]
    public String FileName { get => _FileName; set { if (OnPropertyChanging("FileName", value)) { _FileName = value; OnPropertyChanged("FileName"); } } }

    private String _FileRef;
    /// <summary>文件在插件数据目录内的相对路径</summary>
    [DisplayName("文件在插件数据目录内的相对路径")]
    [Description("文件在插件数据目录内的相对路径")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("FileRef", "文件在插件数据目录内的相对路径", "")]
    public String FileRef { get => _FileRef; set { if (OnPropertyChanging("FileRef", value)) { _FileRef = value; OnPropertyChanged("FileRef"); } } }

    private String _Display;
    /// <summary>font-display 策略</summary>
    [DisplayName("font-display策略")]
    [Description("font-display 策略")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Display", "font-display 策略", "", DefaultValue = "swap")]
    public String Display { get => _Display; set { if (OnPropertyChanging("Display", value)) { _Display = value; OnPropertyChanged("Display"); } } }

    private String _Role;
    /// <summary>角色：sans|serif|mono|display</summary>
    [DisplayName("角色")]
    [Description("角色：sans|serif|mono|display")]
    [DataObjectField(false, false, true, 30)]
    [BindColumn("Role", "角色：sans|serif|mono|display", "")]
    public String Role { get => _Role; set { if (OnPropertyChanging("Role", value)) { _Role = value; OnPropertyChanged("Role"); } } }

    private String _SourceUrl;
    /// <summary>来源地址（自托管则留空）</summary>
    [DisplayName("来源地址（自托管则留空）")]
    [Description("来源地址（自托管则留空）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("SourceUrl", "来源地址（自托管则留空）", "")]
    public String SourceUrl { get => _SourceUrl; set { if (OnPropertyChanging("SourceUrl", value)) { _SourceUrl = value; OnPropertyChanged("SourceUrl"); } } }

    private String _License;
    /// <summary>许可证（字体许可常为 SIL OFL，需登记）</summary>
    [DisplayName("许可证（字体许可常为SILOFL")]
    [Description("许可证（字体许可常为 SIL OFL，需登记）")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("License", "许可证（字体许可常为 SIL OFL，需登记）", "")]
    public String License { get => _License; set { if (OnPropertyChanging("License", value)) { _License = value; OnPropertyChanged("License"); } } }

    private String _MetricsJson;
    /// <summary>度量信息（ascender/descender/x-height 等）</summary>
    [DisplayName("度量信息（ascender_descender_x-height等）")]
    [Description("度量信息（ascender/descender/x-height 等）")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("MetricsJson", "度量信息（ascender/descender/x-height 等）", "")]
    public String MetricsJson { get => _MetricsJson; set { if (OnPropertyChanging("MetricsJson", value)) { _MetricsJson = value; OnPropertyChanged("MetricsJson"); } } }

    private String _Extensions;
    /// <summary>扩展元数据袋（JSON）</summary>
    [DisplayName("扩展元数据袋（JSON）")]
    [Description("扩展元数据袋（JSON）")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Extensions", "扩展元数据袋（JSON）", "")]
    public String Extensions { get => _Extensions; set { if (OnPropertyChanging("Extensions", value)) { _Extensions = value; OnPropertyChanged("Extensions"); } } }

    private DateTime _CreatedAt;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreatedAt", "创建时间", "")]
    public DateTime CreatedAt { get => _CreatedAt; set { if (OnPropertyChanging("CreatedAt", value)) { _CreatedAt = value; OnPropertyChanged("CreatedAt"); } } }

    private DateTime _UpdatedAt;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdatedAt", "更新时间", "")]
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
            "Family" => _Family,
            "Weight" => _Weight,
            "Style" => _Style,
            "FileName" => _FileName,
            "FileRef" => _FileRef,
            "Display" => _Display,
            "Role" => _Role,
            "SourceUrl" => _SourceUrl,
            "License" => _License,
            "MetricsJson" => _MetricsJson,
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
                case "Family": _Family = Convert.ToString(value); break;
                case "Weight": _Weight = value.ToInt(); break;
                case "Style": _Style = Convert.ToString(value); break;
                case "FileName": _FileName = Convert.ToString(value); break;
                case "FileRef": _FileRef = Convert.ToString(value); break;
                case "Display": _Display = Convert.ToString(value); break;
                case "Role": _Role = Convert.ToString(value); break;
                case "SourceUrl": _SourceUrl = Convert.ToString(value); break;
                case "License": _License = Convert.ToString(value); break;
                case "MetricsJson": _MetricsJson = Convert.ToString(value); break;
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
    /// <summary>根据字体ID查找</summary>
    /// <param name="id">字体ID</param>
    /// <returns>实体对象</returns>
    public static DesignFontFace FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据角色查找</summary>
    /// <param name="role">角色</param>
    /// <returns>实体列表</returns>
    public static IList<DesignFontFace> FindAllByRole(String role)
    {
        if (role.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Role.EqualIgnoreCase(role));

        return FindAll(_.Role == role);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="projectId">项目ID；0=共享登记</param>
    /// <param name="weight">字重</param>
    /// <param name="style">样式：normal|italic</param>
    /// <param name="role">角色：sans|serif|mono|display</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DesignFontFace> Search(Int64 projectId, Int32 weight, String style, String role, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (weight >= 0) exp &= _.Weight == weight;
        if (!style.IsNullOrEmpty()) exp &= _.Style == style;
        if (!role.IsNullOrEmpty()) exp &= _.Role == role;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得字体资产字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>字体ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目ID；0=共享登记</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>字族名</summary>
        public static readonly Field Family = FindByName("Family");

        /// <summary>字重</summary>
        public static readonly Field Weight = FindByName("Weight");

        /// <summary>样式：normal|italic</summary>
        public static readonly Field Style = FindByName("Style");

        /// <summary>字体文件名</summary>
        public static readonly Field FileName = FindByName("FileName");

        /// <summary>文件在插件数据目录内的相对路径</summary>
        public static readonly Field FileRef = FindByName("FileRef");

        /// <summary>font-display 策略</summary>
        public static readonly Field Display = FindByName("Display");

        /// <summary>角色：sans|serif|mono|display</summary>
        public static readonly Field Role = FindByName("Role");

        /// <summary>来源地址（自托管则留空）</summary>
        public static readonly Field SourceUrl = FindByName("SourceUrl");

        /// <summary>许可证（字体许可常为 SIL OFL，需登记）</summary>
        public static readonly Field License = FindByName("License");

        /// <summary>度量信息（ascender/descender/x-height 等）</summary>
        public static readonly Field MetricsJson = FindByName("MetricsJson");

        /// <summary>扩展元数据袋（JSON）</summary>
        public static readonly Field Extensions = FindByName("Extensions");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得字体资产字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>字体ID</summary>
        public const String Id = "Id";

        /// <summary>项目ID；0=共享登记</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>字族名</summary>
        public const String Family = "Family";

        /// <summary>字重</summary>
        public const String Weight = "Weight";

        /// <summary>样式：normal|italic</summary>
        public const String Style = "Style";

        /// <summary>字体文件名</summary>
        public const String FileName = "FileName";

        /// <summary>文件在插件数据目录内的相对路径</summary>
        public const String FileRef = "FileRef";

        /// <summary>font-display 策略</summary>
        public const String Display = "Display";

        /// <summary>角色：sans|serif|mono|display</summary>
        public const String Role = "Role";

        /// <summary>来源地址（自托管则留空）</summary>
        public const String SourceUrl = "SourceUrl";

        /// <summary>许可证（字体许可常为 SIL OFL，需登记）</summary>
        public const String License = "License";

        /// <summary>度量信息（ascender/descender/x-height 等）</summary>
        public const String MetricsJson = "MetricsJson";

        /// <summary>扩展元数据袋（JSON）</summary>
        public const String Extensions = "Extensions";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";

        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}
