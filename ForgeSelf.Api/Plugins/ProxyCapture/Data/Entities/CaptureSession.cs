using System;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.ProxyCapture.Data.Entities;

/// <summary>抓包会话记录：一条被监听到的请求（含完整请求/响应信息）。</summary>
[Serializable]
[DataObject]
[Description("抓包会话记录")]
[BindTable("CaptureSession", Description = "抓包会话记录", ConnName = "ProxyCapture", DbType = DatabaseType.None)]
public partial class CaptureSession : Entity<CaptureSession>
{
    #region 属性
    private Int64 _Id;
    [DisplayName("编号")][Description("编号")][DataObjectField(true, true, false, 0)][BindColumn("Id", "编号", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _ListenerId;
    [DisplayName("监听器ID")][Description("监听器ID")][DataObjectField(false, false, false, 0)][BindColumn("ListenerId", "监听器ID", "")]
    public Int32 ListenerId { get => _ListenerId; set { if (OnPropertyChanging("ListenerId", value)) { _ListenerId = value; OnPropertyChanged("ListenerId"); } } }

    private DateTime _Timestamp;
    [DisplayName("时间戳")][Description("时间戳")][DataObjectField(false, false, true, 0)][BindColumn("Timestamp", "时间戳", "")]
    public DateTime Timestamp { get => _Timestamp; set { if (OnPropertyChanging("Timestamp", value)) { _Timestamp = value; OnPropertyChanged("Timestamp"); } } }

    private String _Protocol;
    [DisplayName("协议")][Description("协议")][DataObjectField(false, false, false, 16)][BindColumn("Protocol", "协议", "")]
    public String Protocol { get => _Protocol; set { if (OnPropertyChanging("Protocol", value)) { _Protocol = value; OnPropertyChanged("Protocol"); } } }

    private String _ClientIp;
    [DisplayName("客户端IP")][Description("客户端IP")][DataObjectField(false, false, false, 64)][BindColumn("ClientIp", "客户端IP", "")]
    public String ClientIp { get => _ClientIp; set { if (OnPropertyChanging("ClientIp", value)) { _ClientIp = value; OnPropertyChanged("ClientIp"); } } }

    private String _LocalEndpoint;
    [DisplayName("本地端点")][Description("本地端点")][DataObjectField(false, false, false, 64)][BindColumn("LocalEndpoint", "本地端点", "")]
    public String LocalEndpoint { get => _LocalEndpoint; set { if (OnPropertyChanging("LocalEndpoint", value)) { _LocalEndpoint = value; OnPropertyChanged("LocalEndpoint"); } } }

    private String _Target;
    [DisplayName("目标")][Description("目标")][DataObjectField(false, false, true, 256)][BindColumn("Target", "目标", "")]
    public String Target { get => _Target; set { if (OnPropertyChanging("Target", value)) { _Target = value; OnPropertyChanged("Target"); } } }

    private String _Method;
    [DisplayName("方法")][Description("方法")][DataObjectField(false, false, true, 16)][BindColumn("Method", "方法", "")]
    public String Method { get => _Method; set { if (OnPropertyChanging("Method", value)) { _Method = value; OnPropertyChanged("Method"); } } }

    private String _Url;
    [DisplayName("URL")][Description("请求URL/路径")][DataObjectField(false, false, true, 0)][BindColumn("Url", "请求URL/路径", "")]
    public String Url { get => _Url; set { if (OnPropertyChanging("Url", value)) { _Url = value; OnPropertyChanged("Url"); } } }

    private String _HttpVersion;
    [DisplayName("HTTP版本")][Description("HTTP版本")][DataObjectField(false, false, true, 16)][BindColumn("HttpVersion", "HTTP版本", "")]
    public String HttpVersion { get => _HttpVersion; set { if (OnPropertyChanging("HttpVersion", value)) { _HttpVersion = value; OnPropertyChanged("HttpVersion"); } } }

    private String _RequestHeaders;
    [DisplayName("请求头")][Description("请求头(JSON)")][DataObjectField(false, false, true, 0)][BindColumn("RequestHeaders", "请求头(JSON)", "")]
    public String RequestHeaders { get => _RequestHeaders; set { if (OnPropertyChanging("RequestHeaders", value)) { _RequestHeaders = value; OnPropertyChanged("RequestHeaders"); } } }

    private String _RequestBody;
    [DisplayName("请求体")][Description("请求体")][DataObjectField(false, false, true, 0)][BindColumn("RequestBody", "请求体", "")]
    public String RequestBody { get => _RequestBody; set { if (OnPropertyChanging("RequestBody", value)) { _RequestBody = value; OnPropertyChanged("RequestBody"); } } }

    private Int32 _StatusCode;
    [DisplayName("状态码")][Description("状态码")][DataObjectField(false, false, false, 0)][BindColumn("StatusCode", "状态码", "")]
    public Int32 StatusCode { get => _StatusCode; set { if (OnPropertyChanging("StatusCode", value)) { _StatusCode = value; OnPropertyChanged("StatusCode"); } } }

    private String _ResponseHeaders;
    [DisplayName("响应头")][Description("响应头(JSON)")][DataObjectField(false, false, true, 0)][BindColumn("ResponseHeaders", "响应头(JSON)", "")]
    public String ResponseHeaders { get => _ResponseHeaders; set { if (OnPropertyChanging("ResponseHeaders", value)) { _ResponseHeaders = value; OnPropertyChanged("ResponseHeaders"); } } }

    private String _ResponseBody;
    [DisplayName("响应体")][Description("响应体")][DataObjectField(false, false, true, 0)][BindColumn("ResponseBody", "响应体", "")]
    public String ResponseBody { get => _ResponseBody; set { if (OnPropertyChanging("ResponseBody", value)) { _ResponseBody = value; OnPropertyChanged("ResponseBody"); } } }

    private Int64 _RequestBytes;
    [DisplayName("请求字节数")][Description("请求字节数")][DataObjectField(false, false, false, 0)][BindColumn("RequestBytes", "请求字节数", "")]
    public Int64 RequestBytes { get => _RequestBytes; set { if (OnPropertyChanging("RequestBytes", value)) { _RequestBytes = value; OnPropertyChanged("RequestBytes"); } } }

    private Int64 _ResponseBytes;
    [DisplayName("响应字节数")][Description("响应字节数")][DataObjectField(false, false, false, 0)][BindColumn("ResponseBytes", "响应字节数", "")]
    public Int64 ResponseBytes { get => _ResponseBytes; set { if (OnPropertyChanging("ResponseBytes", value)) { _ResponseBytes = value; OnPropertyChanged("ResponseBytes"); } } }

    private Int64 _DurationMs;
    [DisplayName("耗时(ms)")][Description("耗时(ms)")][DataObjectField(false, false, false, 0)][BindColumn("DurationMs", "耗时(ms)", "")]
    public Int64 DurationMs { get => _DurationMs; set { if (OnPropertyChanging("DurationMs", value)) { _DurationMs = value; OnPropertyChanged("DurationMs"); } } }

    private Boolean _Forwarded;
    [DisplayName("已转发")][Description("是否转发")][DataObjectField(false, false, false, 0)][BindColumn("Forwarded", "是否转发", "")]
    public Boolean Forwarded { get => _Forwarded; set { if (OnPropertyChanging("Forwarded", value)) { _Forwarded = value; OnPropertyChanged("Forwarded"); } } }

    private String _RawPreview = "";
    [DisplayName("原始预览")][Description("原始字节预览(hex)")][DataObjectField(false, false, true, 0)][BindColumn("RawPreview", "原始字节预览(hex)", "")]
    public String RawPreview { get => _RawPreview; set { if (OnPropertyChanging("RawPreview", value)) { _RawPreview = value; OnPropertyChanged("RawPreview"); } } }

    private String _ErrorMessage = "";
    [DisplayName("错误信息")][Description("错误信息")][DataObjectField(false, false, true, 0)][BindColumn("ErrorMessage", "错误信息", "")]
    public String ErrorMessage { get => _ErrorMessage; set { if (OnPropertyChanging("ErrorMessage", value)) { _ErrorMessage = value; OnPropertyChanged("ErrorMessage"); } } }
    #endregion

    #region 获取/设置 字段值
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id, "ListenerId" => _ListenerId, "Timestamp" => _Timestamp, "Protocol" => _Protocol,
            "ClientIp" => _ClientIp, "LocalEndpoint" => _LocalEndpoint, "Target" => _Target, "Method" => _Method,
            "Url" => _Url, "HttpVersion" => _HttpVersion, "RequestHeaders" => _RequestHeaders, "RequestBody" => _RequestBody,
            "StatusCode" => _StatusCode, "ResponseHeaders" => _ResponseHeaders, "ResponseBody" => _ResponseBody,
            "RequestBytes" => _RequestBytes, "ResponseBytes" => _ResponseBytes, "DurationMs" => _DurationMs,
            "Forwarded" => _Forwarded, "RawPreview" => _RawPreview, "ErrorMessage" => _ErrorMessage,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "ListenerId": _ListenerId = value.ToInt(); break;
                case "Timestamp": _Timestamp = value.ToDateTime(); break;
                case "Protocol": _Protocol = Convert.ToString(value); break;
                case "ClientIp": _ClientIp = Convert.ToString(value); break;
                case "LocalEndpoint": _LocalEndpoint = Convert.ToString(value); break;
                case "Target": _Target = Convert.ToString(value); break;
                case "Method": _Method = Convert.ToString(value); break;
                case "Url": _Url = Convert.ToString(value); break;
                case "HttpVersion": _HttpVersion = Convert.ToString(value); break;
                case "RequestHeaders": _RequestHeaders = Convert.ToString(value); break;
                case "RequestBody": _RequestBody = Convert.ToString(value); break;
                case "StatusCode": _StatusCode = value.ToInt(); break;
                case "ResponseHeaders": _ResponseHeaders = Convert.ToString(value); break;
                case "ResponseBody": _ResponseBody = Convert.ToString(value); break;
                case "RequestBytes": _RequestBytes = value.ToLong(); break;
                case "ResponseBytes": _ResponseBytes = value.ToLong(); break;
                case "DurationMs": _DurationMs = value.ToLong(); break;
                case "Forwarded": _Forwarded = value.ToBoolean(); break;
                case "RawPreview": _RawPreview = Convert.ToString(value); break;
                case "ErrorMessage": _ErrorMessage = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 字段名
    public partial class _
    {
        public static readonly Field Id = FindByName("Id");
        public static readonly Field ListenerId = FindByName("ListenerId");
        public static readonly Field Timestamp = FindByName("Timestamp");
        public static readonly Field Protocol = FindByName("Protocol");
        public static readonly Field ClientIp = FindByName("ClientIp");
        public static readonly Field LocalEndpoint = FindByName("LocalEndpoint");
        public static readonly Field Target = FindByName("Target");
        public static readonly Field Method = FindByName("Method");
        public static readonly Field Url = FindByName("Url");
        public static readonly Field HttpVersion = FindByName("HttpVersion");
        public static readonly Field RequestHeaders = FindByName("RequestHeaders");
        public static readonly Field RequestBody = FindByName("RequestBody");
        public static readonly Field StatusCode = FindByName("StatusCode");
        public static readonly Field ResponseHeaders = FindByName("ResponseHeaders");
        public static readonly Field ResponseBody = FindByName("ResponseBody");
        public static readonly Field RequestBytes = FindByName("RequestBytes");
        public static readonly Field ResponseBytes = FindByName("ResponseBytes");
        public static readonly Field DurationMs = FindByName("DurationMs");
        public static readonly Field Forwarded = FindByName("Forwarded");
        public static readonly Field RawPreview = FindByName("RawPreview");
        public static readonly Field ErrorMessage = FindByName("ErrorMessage");
        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    public partial class __
    {
        public const String Id = "Id";
        public const String ListenerId = "ListenerId";
        public const String Timestamp = "Timestamp";
        public const String Protocol = "Protocol";
        public const String ClientIp = "ClientIp";
        public const String LocalEndpoint = "LocalEndpoint";
        public const String Target = "Target";
        public const String Method = "Method";
        public const String Url = "Url";
        public const String HttpVersion = "HttpVersion";
        public const String RequestHeaders = "RequestHeaders";
        public const String RequestBody = "RequestBody";
        public const String StatusCode = "StatusCode";
        public const String ResponseHeaders = "ResponseHeaders";
        public const String ResponseBody = "ResponseBody";
        public const String RequestBytes = "RequestBytes";
        public const String ResponseBytes = "ResponseBytes";
        public const String DurationMs = "DurationMs";
        public const String Forwarded = "Forwarded";
        public const String RawPreview = "RawPreview";
        public const String ErrorMessage = "ErrorMessage";
    }
    #endregion
}
