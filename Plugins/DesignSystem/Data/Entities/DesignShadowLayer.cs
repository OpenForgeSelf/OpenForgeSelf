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
[BindIndex("IU_DesignShadowLayer_TokenId_Layer", true, "TokenId,Layer")]
[BindIndex("IX_DesignShadowLayer_ProjectId", false, "ProjectId")]
[BindTable("DesignShadowLayer", Description = "阴影层", ConnName = "DesignSystem", DbType = DatabaseType.None)]
public partial class DesignShadowLayer
{
    #region 属性
    private Int64 _Id;
    /// <summary>层ID</summary>
    [DisplayName("层ID")]
    [Description("层ID")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "层ID", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ProjectId;
    /// <summary>项目ID</summary>
    [DisplayName("项目ID")]
    [Description("项目ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目ID", "")]
    public Int64 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private Int64 _TokenId;
    /// <summary>所属 shadow 令牌ID</summary>
    [DisplayName("所属shadow令牌ID")]
    [Description("所属 shadow 令牌ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TokenId", "所属 shadow 令牌ID", "")]
    public Int64 TokenId { get => _TokenId; set { if (OnPropertyChanging("TokenId", value)) { _TokenId = value; OnPropertyChanged("TokenId"); } } }

    private String _TokenPath;
    /// <summary>所属令牌路径（冗余便于查询）</summary>
    [DisplayName("所属令牌路径（冗余便于查询）")]
    [Description("所属令牌路径（冗余便于查询）")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("TokenPath", "所属令牌路径（冗余便于查询）", "")]
    public String TokenPath { get => _TokenPath; set { if (OnPropertyChanging("TokenPath", value)) { _TokenPath = value; OnPropertyChanged("TokenPath"); } } }

    private Int32 _Layer;
    /// <summary>层序号，自 0 起</summary>
    [DisplayName("层序号")]
    [Description("层序号，自 0 起")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Layer", "层序号，自 0 起", "")]
    public Int32 Layer { get => _Layer; set { if (OnPropertyChanging("Layer", value)) { _Layer = value; OnPropertyChanged("Layer"); } } }

    private String _Name;
    /// <summary>层显示名</summary>
    [DisplayName("层显示名")]
    [Description("层显示名")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Name", "层显示名", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Boolean _IsInset;
    /// <summary>是否内阴影（暗色主题用 inset 高光替代投影）</summary>
    [DisplayName("是否内阴影（暗色主题用inset高光替代投影）")]
    [Description("是否内阴影（暗色主题用 inset 高光替代投影）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsInset", "是否内阴影（暗色主题用 inset 高光替代投影）", "", DefaultValue = "0")]
    public Boolean IsInset { get => _IsInset; set { if (OnPropertyChanging("IsInset", value)) { _IsInset = value; OnPropertyChanged("IsInset"); } } }

    private Double _OffsetX;
    /// <summary>X 偏移 px</summary>
    [DisplayName("X偏移px")]
    [Description("X 偏移 px")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OffsetX", "X 偏移 px", "")]
    public Double OffsetX { get => _OffsetX; set { if (OnPropertyChanging("OffsetX", value)) { _OffsetX = value; OnPropertyChanged("OffsetX"); } } }

    private Double _OffsetY;
    /// <summary>Y 偏移 px</summary>
    [DisplayName("Y偏移px")]
    [Description("Y 偏移 px")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OffsetY", "Y 偏移 px", "")]
    public Double OffsetY { get => _OffsetY; set { if (OnPropertyChanging("OffsetY", value)) { _OffsetY = value; OnPropertyChanged("OffsetY"); } } }

    private Double _Blur;
    /// <summary>模糊 px</summary>
    [DisplayName("模糊px")]
    [Description("模糊 px")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Blur", "模糊 px", "")]
    public Double Blur { get => _Blur; set { if (OnPropertyChanging("Blur", value)) { _Blur = value; OnPropertyChanged("Blur"); } } }

    private Double _Spread;
    /// <summary>扩散 px</summary>
    [DisplayName("扩散px")]
    [Description("扩散 px")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Spread", "扩散 px", "")]
    public Double Spread { get => _Spread; set { if (OnPropertyChanging("Spread", value)) { _Spread = value; OnPropertyChanged("Spread"); } } }

    private String _ColorValue;
    /// <summary>层颜色值</summary>
    [DisplayName("层颜色值")]
    [Description("层颜色值")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ColorValue", "层颜色值", "")]
    public String ColorValue { get => _ColorValue; set { if (OnPropertyChanging("ColorValue", value)) { _ColorValue = value; OnPropertyChanged("ColorValue"); } } }

    private String _ColorAliasPath;
    /// <summary>层颜色指向的颜色令牌路径</summary>
    [DisplayName("层颜色指向的颜色令牌路径")]
    [Description("层颜色指向的颜色令牌路径")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("ColorAliasPath", "层颜色指向的颜色令牌路径", "")]
    public String ColorAliasPath { get => _ColorAliasPath; set { if (OnPropertyChanging("ColorAliasPath", value)) { _ColorAliasPath = value; OnPropertyChanged("ColorAliasPath"); } } }

    private Double _Alpha;
    /// <summary>层透明度</summary>
    [DisplayName("层透明度")]
    [Description("层透明度")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Alpha", "层透明度", "", DefaultValue = "1")]
    public Double Alpha { get => _Alpha; set { if (OnPropertyChanging("Alpha", value)) { _Alpha = value; OnPropertyChanged("Alpha"); } } }

    private String _Usage;
    /// <summary>用途说明</summary>
    [DisplayName("用途说明")]
    [Description("用途说明")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Usage", "用途说明", "")]
    public String Usage { get => _Usage; set { if (OnPropertyChanging("Usage", value)) { _Usage = value; OnPropertyChanged("Usage"); } } }

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
            "TokenId" => _TokenId,
            "TokenPath" => _TokenPath,
            "Layer" => _Layer,
            "Name" => _Name,
            "IsInset" => _IsInset,
            "OffsetX" => _OffsetX,
            "OffsetY" => _OffsetY,
            "Blur" => _Blur,
            "Spread" => _Spread,
            "ColorValue" => _ColorValue,
            "ColorAliasPath" => _ColorAliasPath,
            "Alpha" => _Alpha,
            "Usage" => _Usage,
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
                case "TokenId": _TokenId = value.ToLong(); break;
                case "TokenPath": _TokenPath = Convert.ToString(value); break;
                case "Layer": _Layer = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "IsInset": _IsInset = value.ToBoolean(); break;
                case "OffsetX": _OffsetX = value.ToDouble(); break;
                case "OffsetY": _OffsetY = value.ToDouble(); break;
                case "Blur": _Blur = value.ToDouble(); break;
                case "Spread": _Spread = value.ToDouble(); break;
                case "ColorValue": _ColorValue = Convert.ToString(value); break;
                case "ColorAliasPath": _ColorAliasPath = Convert.ToString(value); break;
                case "Alpha": _Alpha = value.ToDouble(); break;
                case "Usage": _Usage = Convert.ToString(value); break;
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
    /// <summary>根据层ID查找</summary>
    /// <param name="id">层ID</param>
    /// <returns>实体对象</returns>
    public static DesignShadowLayer FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据所属shadow令牌ID、层序号查找</summary>
    /// <param name="tokenId">所属shadow令牌ID</param>
    /// <param name="layer">层序号</param>
    /// <returns>实体对象</returns>
    public static DesignShadowLayer FindByTokenIdAndLayer(Int64 tokenId, Int32 layer)
    {
        if (tokenId < 0) return null;
        if (layer < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.TokenId == tokenId && e.Layer == layer);

        return Find(_.TokenId == tokenId & _.Layer == layer);
    }

    /// <summary>根据所属shadow令牌ID查找</summary>
    /// <param name="tokenId">所属shadow令牌ID</param>
    /// <returns>实体列表</returns>
    public static IList<DesignShadowLayer> FindAllByTokenId(Int64 tokenId)
    {
        if (tokenId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.TokenId == tokenId);

        return FindAll(_.TokenId == tokenId);
    }

    /// <summary>根据项目ID查找</summary>
    /// <param name="projectId">项目ID</param>
    /// <returns>实体列表</returns>
    public static IList<DesignShadowLayer> FindAllByProjectId(Int64 projectId)
    {
        if (projectId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="projectId">项目ID</param>
    /// <param name="tokenId">所属 shadow 令牌ID</param>
    /// <param name="layer">层序号，自 0 起</param>
    /// <param name="isInset">是否内阴影（暗色主题用 inset 高光替代投影）</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DesignShadowLayer> Search(Int64 projectId, Int64 tokenId, Int32 layer, Boolean? isInset, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (tokenId >= 0) exp &= _.TokenId == tokenId;
        if (layer >= 0) exp &= _.Layer == layer;
        if (isInset != null) exp &= _.IsInset == isInset;
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得阴影层字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>层ID</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>项目ID</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>所属 shadow 令牌ID</summary>
        public static readonly Field TokenId = FindByName("TokenId");

        /// <summary>所属令牌路径（冗余便于查询）</summary>
        public static readonly Field TokenPath = FindByName("TokenPath");

        /// <summary>层序号，自 0 起</summary>
        public static readonly Field Layer = FindByName("Layer");

        /// <summary>层显示名</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>是否内阴影（暗色主题用 inset 高光替代投影）</summary>
        public static readonly Field IsInset = FindByName("IsInset");

        /// <summary>X 偏移 px</summary>
        public static readonly Field OffsetX = FindByName("OffsetX");

        /// <summary>Y 偏移 px</summary>
        public static readonly Field OffsetY = FindByName("OffsetY");

        /// <summary>模糊 px</summary>
        public static readonly Field Blur = FindByName("Blur");

        /// <summary>扩散 px</summary>
        public static readonly Field Spread = FindByName("Spread");

        /// <summary>层颜色值</summary>
        public static readonly Field ColorValue = FindByName("ColorValue");

        /// <summary>层颜色指向的颜色令牌路径</summary>
        public static readonly Field ColorAliasPath = FindByName("ColorAliasPath");

        /// <summary>层透明度</summary>
        public static readonly Field Alpha = FindByName("Alpha");

        /// <summary>用途说明</summary>
        public static readonly Field Usage = FindByName("Usage");

        /// <summary>扩展元数据袋（JSON）</summary>
        public static readonly Field Extensions = FindByName("Extensions");

        /// <summary>创建时间</summary>
        public static readonly Field CreatedAt = FindByName("CreatedAt");

        /// <summary>更新时间</summary>
        public static readonly Field UpdatedAt = FindByName("UpdatedAt");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得阴影层字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>层ID</summary>
        public const String Id = "Id";

        /// <summary>项目ID</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>所属 shadow 令牌ID</summary>
        public const String TokenId = "TokenId";

        /// <summary>所属令牌路径（冗余便于查询）</summary>
        public const String TokenPath = "TokenPath";

        /// <summary>层序号，自 0 起</summary>
        public const String Layer = "Layer";

        /// <summary>层显示名</summary>
        public const String Name = "Name";

        /// <summary>是否内阴影（暗色主题用 inset 高光替代投影）</summary>
        public const String IsInset = "IsInset";

        /// <summary>X 偏移 px</summary>
        public const String OffsetX = "OffsetX";

        /// <summary>Y 偏移 px</summary>
        public const String OffsetY = "OffsetY";

        /// <summary>模糊 px</summary>
        public const String Blur = "Blur";

        /// <summary>扩散 px</summary>
        public const String Spread = "Spread";

        /// <summary>层颜色值</summary>
        public const String ColorValue = "ColorValue";

        /// <summary>层颜色指向的颜色令牌路径</summary>
        public const String ColorAliasPath = "ColorAliasPath";

        /// <summary>层透明度</summary>
        public const String Alpha = "Alpha";

        /// <summary>用途说明</summary>
        public const String Usage = "Usage";

        /// <summary>扩展元数据袋（JSON）</summary>
        public const String Extensions = "Extensions";

        /// <summary>创建时间</summary>
        public const String CreatedAt = "CreatedAt";

        /// <summary>更新时间</summary>
        public const String UpdatedAt = "UpdatedAt";
    }
    #endregion
}
