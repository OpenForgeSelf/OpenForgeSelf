using DbConnectionStringBuilder = System.Data.Common.DbConnectionStringBuilder;
using System.Diagnostics;
using ForgeSelf.Api.Plugins.DbAdmin.Entities;
using ForgeSelf.Api.Plugins.DbAdmin.Models;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.DbAdmin.Services;

/// <summary>解析后的连接</summary>
public record ResolvedConnection(String Id, String ConnName, Boolean IsSystem, Boolean Readonly, String Provider);

/// <summary>
/// 连接管理：系统只读连接（宿主 + PluginDbs，取自进程内 <see cref="DAL.ConnStrs"/>）与用户连接（<c>DbAdminU_{Id}</c>）。
/// 唯一性 / 存在性一律直查 DB（铁律11）。删除只删配置，绝不删库文件。
/// </summary>
public sealed class DbConnectionService
{
    public const String UserConnPrefix = "DbAdminU_";
    public const String SystemIdPrefix = "sys:";
    private const String ProbeConnName = UserConnPrefix + "_probe";

    private readonly ISecretProtector _protector;
    private readonly IDriverProbe _driverProbe;
    private readonly DbAuditWriter _audit;
    private readonly Func<IEnumerable<String>> _systemConnNames;
    private readonly Dictionary<String, String> _registered = new(StringComparer.Ordinal);
    private readonly Lock _regLock = new();
    private readonly Lock _probeLock = new();

    public DbConnectionService(ISecretProtector protector, IDriverProbe driverProbe, DbAuditWriter audit, Func<IEnumerable<String>>? systemConnNames = null)
    {
        _protector = protector;
        _driverProbe = driverProbe;
        _audit = audit;
        _systemConnNames = systemConnNames ?? DefaultSystemConnNames;
    }

    /// <summary>默认系统连接：进程内已注册且非 DbAdminU_ 前缀的全部连接名</summary>
    public static IEnumerable<String> DefaultSystemConnNames() =>
        DAL.ConnStrs!.Keys.Where(k => !k.StartsWith(UserConnPrefix, StringComparison.OrdinalIgnoreCase)).OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

    #region provider
    /// <summary>provider 列表。预检关闭时 <c>DriverAvailable</c> 恒 true（不再据此拦截/标「需驱动」）。</summary>
    public IReadOnlyList<ProviderInfo> ListProviders() =>
        DriverCatalog.Providers.Select(p => new ProviderInfo(
            p.Id, p.DisplayName,
            !DriverCatalog.ProbeEnabled || _driverProbe.IsAvailable(p),
            p.Tested, p.DriverPackage, p.DefaultPort)).ToList();
    #endregion

    #region 列表 / 查询
    public IReadOnlyList<ConnectionView> List()
    {
        var list = new List<ConnectionView>();
        foreach (var name in _systemConnNames())
        {
            list.Add(SystemView(name));
        }
        foreach (var e in DbConnection.FindAll(null, DbConnection._.Id.Asc(), null, 0, 0))
            list.Add(ToView(e));
        return list;
    }

    public ConnectionView Get(String id)
    {
        if (id.StartsWith(SystemIdPrefix, StringComparison.Ordinal))
        {
            var name = id[SystemIdPrefix.Length..];
            if (!_systemConnNames().Contains(name, StringComparer.Ordinal)) throw NotFound(id);
            return SystemView(name);
        }
        return ToView(LoadUser(id));
    }

    private ConnectionView SystemView(String name)
    {
        String provider;
        try { provider = DAL.Create(name).DbType.ToString(); } catch { provider = "unknown"; }
        return new ConnectionView
        {
            Id = SystemIdPrefix + name,
            Name = name,
            Provider = provider.ToLowerInvariant(),
            Kind = "system",
            Mode = "system",
            Readonly = true,
            Tested = provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase),
        };
    }

    public ConnectionView ToView(DbConnection e) => new()
    {
        Id = e.Id.ToString(),
        Name = e.Name,
        Provider = e.Provider,
        Kind = "user",
        Mode = e.Mode,
        Host = e.Host,
        Port = e.Port == 0 ? null : e.Port,
        Database = e.Database,
        UserName = e.UserName,
        Password = String.IsNullOrEmpty(e.PasswordEnc) ? null : SqlRedactor.Mask,
        ConnStr = String.IsNullOrEmpty(e.ConnStrEnc) ? null : MaskConnStr(SafeUnprotect(e.ConnStrEnc)),
        Readonly = e.Readonly,
        Tested = DriverCatalog.Find(e.Provider)?.Tested ?? false,
        CreateTime = e.CreateTime,
        UpdateTime = e.UpdateTime,
    };

    private String? SafeUnprotect(String? c)
    {
        try { return _protector.Unprotect(c); } catch { return null; }
    }

    /// <summary>连接串掩码：口令类键值替换为 ****（解不开时整串掩码）</summary>
    public static String MaskConnStr(String? connStr) => connStr == null ? SqlRedactor.Mask : SqlRedactor.RedactText(connStr);

    private static DbConnection? FindUser(Int32 id) => DbConnection.Find(DbConnection._.Id == id);

    private static DbConnection LoadUser(String id)
    {
        if (!Int32.TryParse(id, out var n)) throw NotFound(id);
        return FindUser(n) ?? throw NotFound(id);
    }

    private static DbAdminException NotFound(String id) => new(DbErrorCodes.NotFound, $"连接不存在：{id}");
    #endregion

    #region CRUD
    public ConnectionView Create(ConnectionInput input)
    {
        var e = new DbConnection();
        Apply(e, input, isCreate: true);
        e.CreateTime = e.UpdateTime = DateTime.Now;
        e.Insert();
        _audit.Write("conn_create", e.Id.ToString(), null, "ok", $"name={e.Name} provider={e.Provider} mode={e.Mode}");
        return ToView(e);
    }

    public ConnectionView Update(String id, ConnectionInput input)
    {
        if (id.StartsWith(SystemIdPrefix, StringComparison.Ordinal))
            throw new DbAdminException(DbErrorCodes.ReadonlyConnection, "系统连接不可编辑");
        var e = LoadUser(id);
        Apply(e, input, isCreate: false);
        e.UpdateTime = DateTime.Now;
        e.Update();
        Unregister(e.Id);
        _audit.Write("conn_update", e.Id.ToString(), null, "ok", $"name={e.Name} provider={e.Provider} mode={e.Mode}");
        return ToView(e);
    }

    /// <summary>仅删除配置记录；绝不触碰目标库文件</summary>
    public void Delete(String id)
    {
        if (id.StartsWith(SystemIdPrefix, StringComparison.Ordinal))
            throw new DbAdminException(DbErrorCodes.ReadonlyConnection, "系统连接不可删除");
        var e = LoadUser(id);
        e.Delete();
        Unregister(e.Id);
        _audit.Write("conn_delete", id, null, "ok", $"name={e.Name}（仅删配置，未删库文件）");
    }

    private void Apply(DbConnection e, ConnectionInput input, Boolean isCreate)
    {
        var name = input.Name?.Trim();
        if (isCreate || name != null)
        {
            if (String.IsNullOrEmpty(name)) throw Invalid("连接名不能为空");
            if (name.Length > 100) throw Invalid("连接名过长（≤100）");
            // 唯一性直查 DB
            var exp = DbConnection._.Name == name;
            if (!isCreate) exp &= DbConnection._.Id != e.Id;
            if (DbConnection.FindCount(exp, null, null, 0, 0) > 0) throw Invalid($"连接名已存在：{name}");
            e.Name = name;
        }

        if (isCreate || input.Provider != null)
        {
            var def = DriverCatalog.Find(input.Provider) ?? throw Invalid($"不支持的 provider：{input.Provider}");
            e.Provider = def.Id;
        }

        var mode = input.Mode?.Trim().ToLowerInvariant() ?? (isCreate ? "form" : e.Mode);
        if (mode != "form" && mode != "connstr") throw Invalid("mode 只能是 form 或 connstr");
        e.Mode = mode;

        if (input.Host != null) e.Host = input.Host.Trim();
        if (input.Port != null) e.Port = input.Port.Value;
        if (input.Database != null) e.Database = input.Database.Trim();
        if (input.UserName != null) e.UserName = input.UserName.Trim();
        if (input.Readonly != null) e.Readonly = input.Readonly.Value;

        // 密码 / 连接串：空或 **** = 不改
        if (!IsUnchanged(input.Password)) e.PasswordEnc = _protector.Protect(input.Password)!;
        if (!IsUnchanged(input.ConnStr)) e.ConnStrEnc = _protector.Protect(input.ConnStr!.Trim())!;

        if (mode == "form")
        {
            GuardRequiredDatabase(e);
            if (e.Provider != "sqlite" && String.IsNullOrWhiteSpace(e.Host)) throw Invalid("需填写主机（Host）");
            e.ConnStrEnc = null!;
        }
        else
        {
            if (String.IsNullOrEmpty(e.ConnStrEnc)) throw Invalid("connstr 模式需填写连接串");
        }

        GuardNotSystemFile(e);
    }

    private static Boolean IsUnchanged(String? v) => String.IsNullOrEmpty(v) || v == SqlRedactor.Mask;

    private static DbAdminException Invalid(String msg) => new(DbErrorCodes.InvalidRequest, msg);

    /// <summary>Database 必填面：sqlite（库文件路径）与 oracle（服务名）必填；mysql/sqlserver/postgresql 可留空（连上后可枚举/切换数据库）</summary>
    private static void GuardRequiredDatabase(DbConnection e)
    {
        if (String.IsNullOrWhiteSpace(e.Database))
        {
            if (e.Provider == "sqlite") throw Invalid("SQLite 需填写数据库文件路径（Database）");
            if (e.Provider == "oracle") throw Invalid("Oracle 需填写服务名（Database，EZConnect 形如 //host:port/service 中的 service）");
        }
    }

    /// <summary>防绕过：用户 SQLite 连接不得指向任何系统连接的库文件（否则可借用户连接写宿主库）</summary>
    private void GuardNotSystemFile(DbConnection e)
    {
        if (e.Provider != "sqlite") return;
        var file = GetSqliteFile(BuildConnStr(e));
        if (file == null) return;
        foreach (var name in _systemConnNames())
        {
            if (!DAL.ConnStrs!.TryGetValue(name, out var cs)) continue;
            var sys = GetSqliteFile(cs);
            if (sys != null && String.Equals(sys, file, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new DbAdminException(DbErrorCodes.ReadonlyConnection, $"该文件属于系统连接 {name}，只能以系统只读连接访问");
        }
    }

    /// <summary>从连接串取 SQLite 文件完整路径（无法解析返回 null）</summary>
    public static String? GetSqliteFile(String? connStr)
    {
        if (String.IsNullOrWhiteSpace(connStr)) return null;
        try
        {
            var b = new DbConnectionStringBuilder { ConnectionString = connStr };
            foreach (var key in new[] { "Data Source", "DataSource", "Filename", "File" })
            {
                if (b.TryGetValue(key, out var v) && v is String s && !String.IsNullOrWhiteSpace(s) && s != ":memory:")
                    return Path.GetFullPath(s);
            }
        }
        catch { }
        return null;
    }
    #endregion

    #region 连接串 / 注册
    /// <summary>构造明文连接串（仅内存使用，绝不落日志 / 审计）。database=null 时行为与旧版一致。</summary>
    public String BuildConnStr(DbConnection e) => BuildConnStr(e, null);

    /// <summary>构造明文连接串。database 非空 = 切到目标库（仅 mysql/sqlserver/postgresql 支持，调用方 Resolve 已校验）。</summary>
    public String BuildConnStr(DbConnection e, String? database)
    {
        if (e.Mode == "connstr") return RewriteConnStrDatabase(_protector.Unprotect(e.ConnStrEnc) ?? "", e.Provider, database);
        var pwd = SafeUnprotect(e.PasswordEnc) ?? "";
        var b = new DbConnectionStringBuilder();
        var db = String.IsNullOrWhiteSpace(database) ? e.Database : database.Trim();
        switch (e.Provider)
        {
            case "sqlite":
                b["Data Source"] = e.Database;
                break;
            case "mysql":
                b["Server"] = e.Host; b["Port"] = e.Port > 0 ? e.Port : 3306;
                b["Uid"] = e.UserName; b["Pwd"] = pwd;
                // 库名可留空：不选库直接连上，SHOW DATABASES 枚举后再切
                if (!String.IsNullOrWhiteSpace(db)) b["Database"] = db;
                break;
            case "postgresql":
                b["Server"] = e.Host; b["Port"] = e.Port > 0 ? e.Port : 5432;
                b["Database"] = String.IsNullOrWhiteSpace(db) ? "postgres" : db; // 空库回落维护库 postgres
                b["User Id"] = e.UserName; b["Password"] = pwd;
                break;
            case "sqlserver":
                b["Data Source"] = e.Port > 0 ? $"{e.Host},{e.Port}" : e.Host;
                // 库名可留空：登录默认库，sys.databases 枚举后再切
                if (!String.IsNullOrWhiteSpace(db)) b["Initial Catalog"] = db;
                b["User ID"] = e.UserName; b["Password"] = pwd;
                b["TrustServerCertificate"] = "true";
                break;
            case "oracle":
                // ODP.NET EZConnect：//host:port/service（port 缺省 1521）；TNS 别名/完整描述符走 connstr 模式
                b["Data Source"] = $"//{e.Host}:{(e.Port > 0 ? e.Port : 1521)}/{e.Database}";
                b["User Id"] = e.UserName; b["Password"] = pwd;
                break;
            default:
                throw Invalid($"不支持的 provider：{e.Provider}");
        }
        return b.ConnectionString;
    }

    /// <summary>connstr 模式指定数据库：改写/补写库关键字（mysql/postgresql=Database，sqlserver=Initial Catalog）</summary>
    private static String RewriteConnStrDatabase(String connStr, String? provider, String? database)
    {
        if (String.IsNullOrWhiteSpace(database)) return connStr;
        var key = provider switch
        {
            "mysql" or "postgresql" => "Database",
            "sqlserver" => "Initial Catalog",
            _ => null,
        };
        if (key == null) throw Invalid("该 provider 不支持指定数据库");
        try
        {
            var b = new DbConnectionStringBuilder { ConnectionString = connStr };
            b[key] = database.Trim();
            return b.ConnectionString;
        }
        catch (Exception ex)
        {
            throw Invalid($"连接串无法解析，无法切换数据库：{SqlRedactor.RedactText(ex.Message)}");
        }
    }

    /// <summary>多库派生连接名：DbAdminU_{id}__D{净化库名}_{8位稳定哈希}。哈希保证不同库（含仅大小写差异）不共用 ConnName。</summary>
    internal static String DeriveConnName(Int32 id, String database)
    {
        var raw = database.Trim();
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var ch in raw) sb.Append(char.IsAsciiLetterOrDigit(ch) || ch == '_' ? ch : '_');
        var name = sb.ToString();
        if (name.Length > 40) name = name[..40];
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)), 0, 4).ToLowerInvariant();
        return $"{UserConnPrefix}{id}__D{name}_{hash}";
    }

    /// <summary>
    /// 解析连接标识 → 已注册的 ConnName（用户连接首次 AddConnStr）。
    /// database 非空时按库派生独立 ConnName（DbAdminU_{id}__D{净化库名}_{哈希}），实现一连接多库；
    /// 系统连接 / SQLite（仅 main）/ Oracle（不支持切换）会抛 invalid_request。
    /// 驱动预检开启且缺失时抛 driver_missing；关闭时（当前默认）交给 XCode 在连接期自行获取驱动。
    /// </summary>
    public ResolvedConnection Resolve(String? id, String? database = null)
    {
        if (String.IsNullOrWhiteSpace(id)) throw new DbAdminException(DbErrorCodes.InvalidRequest, "connectionId 不能为空");
        if (id.StartsWith(SystemIdPrefix, StringComparison.Ordinal))
        {
            if (!String.IsNullOrWhiteSpace(database)) throw Invalid("系统连接不支持指定数据库（sys:* 恒为单库只读）");
            var name = id[SystemIdPrefix.Length..];
            if (!_systemConnNames().Contains(name, StringComparer.Ordinal)) throw NotFound(id);
            return new ResolvedConnection(id, name, true, true, SystemView(name).Provider);
        }
        var e = LoadUser(id);
        var def = DriverCatalog.Find(e.Provider) ?? throw Invalid($"不支持的 provider：{e.Provider}");
        if (DriverCatalog.ProbeEnabled && !_driverProbe.IsAvailable(def))
            throw new DbAdminException(DbErrorCodes.DriverMissing, DriverCatalog.DriverMissingMessage(def));
        var targetDb = NormalizeTargetDatabase(def, database);
        var connStr = BuildConnStr(e, targetDb);
        EnsureSqliteFileExists(def, connStr);
        var connName = targetDb == null ? UserConnPrefix + e.Id : DeriveConnName(e.Id, targetDb);
        Register(connName, connStr, def);
        return new ResolvedConnection(id, connName, false, e.Readonly, def.Id);
    }

    /// <summary>校验「切换数据库」参数：null/空 = 连接默认库；sqlite 仅接受 main（归一为默认）；oracle 不支持切换</summary>
    private static String? NormalizeTargetDatabase(ProviderDef def, String? database)
    {
        var db = database?.Trim();
        if (String.IsNullOrEmpty(db)) return null;
        return def.Id switch
        {
            "sqlite" when db.Equals("main", StringComparison.OrdinalIgnoreCase) => null,
            "sqlite" => throw Invalid("SQLite 连接不支持切换数据库（一个库文件即一个库，仅 main）"),
            "oracle" => throw Invalid("Oracle 连接不支持切换数据库（枚举到的是 schema，查询请显式写 schema.table）"),
            _ => db,
        };
    }

    private static void EnsureSqliteFileExists(ProviderDef def, String connStr)
    {
        if (def.Id != "sqlite") return;
        var f = GetSqliteFile(connStr);
        // 不让 XCode 打开时自动建出新库文件：文件必须已存在
        if (f != null && !File.Exists(f)) throw new DbAdminException(DbErrorCodes.SqlError, $"SQLite 文件不存在：{f}（DbAdmin 不会自动创建库文件）");
    }

    private void Register(String connName, String connStr, ProviderDef def)
    {
        lock (_regLock)
        {
            if (_registered.TryGetValue(connName, out var old) && old == connStr && DAL.ConnStrs!.ContainsKey(connName)) return;
            DAL.AddConnStr(connName, connStr, null, def.XCodeProvider);
            _registered[connName] = connStr;
        }
    }

    private void Unregister(Int32 id)
    {
        lock (_regLock) _registered.Remove(UserConnPrefix + id);
    }

    /// <summary>
    /// 明文密钥清单（仅内存，供 Agent 工具回包做最后一道兜底擦除）：用户连接的密码、connstr 模式原始连接串、
    /// 由表单拼出的连接串、连接串内 password/pwd 值，以及系统连接的连接串。长度 &lt; 4 的值不收（避免误伤普通数据）。
    /// 绝不可序列化输出或写日志。
    /// </summary>
    public IReadOnlyCollection<String> CollectSecretsForScrub()
    {
        var set = new HashSet<String>(StringComparer.Ordinal);
        void Add(String? v) { if (!String.IsNullOrEmpty(v) && v.Length >= 4) set.Add(v); }
        void AddConnStr(String? cs)
        {
            if (String.IsNullOrEmpty(cs)) return;
            Add(cs);
            try
            {
                var b = new DbConnectionStringBuilder { ConnectionString = cs };
                foreach (var key in new[] { "Password", "Pwd", "PassWord" })
                    if (b.TryGetValue(key, out var v) && v is String s) Add(s);
            }
            catch { }
        }
        try
        {
            foreach (var e in DbConnection.FindAll(null, null, null, 0, 0))
            {
                Add(SafeUnprotect(e.PasswordEnc));
                AddConnStr(SafeUnprotect(e.ConnStrEnc));
                try { AddConnStr(BuildConnStr(e)); } catch { }
            }
        }
        catch { }
        try
        {
            foreach (var name in _systemConnNames())
                if (DAL.ConnStrs!.TryGetValue(name, out var cs)) AddConnStr(cs);
        }
        catch { }
        return set;
    }
    #endregion

    #region 测连
    /// <summary>
    /// 测连（未保存表单亦可）。返回 ok / error(sql_error)；驱动预检开启时才可能返回 driver_missing。
    /// 预检关闭时直接交给 XCode 连接，失败回显底层错误。不抛业务异常。
    /// </summary>
    public ConnectionTestResult Test(ConnectionInput input)
    {
        var sw = Stopwatch.StartNew();
        String? auditId = input.Id?.ToString();
        try
        {
            DbConnection e;
            if (input.Id != null)
            {
                var saved = FindUser(input.Id.Value) ?? throw NotFound(input.Id.Value.ToString());
                e = Clone(saved);
                ApplyForTest(e, input);
            }
            else
            {
                e = new DbConnection { Mode = "form" };
                ApplyForTest(e, input);
            }
            var def = DriverCatalog.Find(e.Provider) ?? throw Invalid($"不支持的 provider：{e.Provider}");
            if (DriverCatalog.ProbeEnabled && !_driverProbe.IsAvailable(def))
            {
                var msg = DriverCatalog.DriverMissingMessage(def);
                _audit.Write("conn_test", auditId, null, DbErrorCodes.DriverMissing, msg);
                return new ConnectionTestResult(false, DbErrorCodes.DriverMissing, msg, null, sw.ElapsedMilliseconds);
            }
            GuardNotSystemFile(e);
            var connStr = BuildConnStr(e);
            EnsureSqliteFileExists(def, connStr);

            String version;
            lock (_probeLock)
            {
                DAL.AddConnStr(ProbeConnName, connStr, null, def.XCodeProvider);
                version = DAL.Create(ProbeConnName).Db.ServerVersion;
            }
            _audit.Write("conn_test", auditId, null, "ok", $"provider={def.Id}");
            return new ConnectionTestResult(true, null, "连接成功", version, sw.ElapsedMilliseconds);
        }
        catch (DbAdminException ex)
        {
            _audit.Write("conn_test", auditId, null, ex.Code, ex.Message);
            return new ConnectionTestResult(false, ex.Code, SqlRedactor.RedactText(ex.Message), null, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            var def = DriverCatalog.Find(input.Provider);
            // 预检关闭时不按异常文本猜「驱动缺失」，直接回显底层错误（用户输入2：驱动还是其他问题另说）
            var isDriver = DriverCatalog.ProbeEnabled && DriverCatalog.IsDriverException(ex);
            var code = isDriver ? DbErrorCodes.DriverMissing : DbErrorCodes.SqlError;
            var msg = isDriver && def != null ? DriverCatalog.DriverMissingMessage(def) : ErrorMessageOf(ex);
            _audit.Write("conn_test", auditId, null, code, msg);
            return new ConnectionTestResult(false, code, msg, null, sw.ElapsedMilliseconds);
        }
    }

    private void ApplyForTest(DbConnection e, ConnectionInput input)
    {
        if (input.Provider != null) e.Provider = DriverCatalog.Find(input.Provider)?.Id ?? throw Invalid($"不支持的 provider：{input.Provider}");
        if (e.Provider == null) throw Invalid("provider 不能为空");
        if (input.Mode != null) e.Mode = input.Mode.Trim().ToLowerInvariant();
        if (input.Host != null) e.Host = input.Host;
        if (input.Port != null) e.Port = input.Port.Value;
        if (input.Database != null) e.Database = input.Database;
        if (input.UserName != null) e.UserName = input.UserName;
        if (!IsUnchanged(input.Password)) e.PasswordEnc = _protector.Protect(input.Password)!;
        if (!IsUnchanged(input.ConnStr)) e.ConnStrEnc = _protector.Protect(input.ConnStr)!;
        if (e.Mode == "connstr" && String.IsNullOrEmpty(e.ConnStrEnc)) throw Invalid("connstr 模式需填写连接串");
        if (e.Mode == "form") GuardRequiredDatabase(e);
    }

    private static DbConnection Clone(DbConnection s) => new()
    {
        Id = s.Id, Name = s.Name, Provider = s.Provider, Mode = s.Mode, Host = s.Host, Port = s.Port,
        Database = s.Database, UserName = s.UserName, PasswordEnc = s.PasswordEnc, ConnStrEnc = s.ConnStrEnc, Readonly = s.Readonly,
    };
    #endregion

    #region Schema
    public IReadOnlyList<TableSummary> ListTables(String id, String? database = null)
    {
        var rc = Resolve(id, database);
        var tables = Wrap(rc, () => DAL.Create(rc.ConnName).Db.CreateMetaData().GetTables());
        return tables.OrderBy(t => t.TableName, StringComparer.OrdinalIgnoreCase)
            .Select(t => new TableSummary(t.TableName, t.IsView, t.Description, t.Columns.Count)).ToList();
    }

    public TableDetail DescribeTable(String id, String table, String? database = null)
    {
        var rc = Resolve(id, database);
        var tables = Wrap(rc, () => DAL.Create(rc.ConnName).Db.CreateMetaData().GetTables());
        var t = tables.FirstOrDefault(x => String.Equals(x.TableName, table, StringComparison.OrdinalIgnoreCase))
            ?? throw new DbAdminException(DbErrorCodes.NotFound, $"表不存在：{table}");
        var cols = t.Columns.Select(c => new ColumnInfo(c.ColumnName, c.DataType?.Name, c.RawType, c.Nullable, c.PrimaryKey, c.Identity, c.Length, c.DefaultValue, c.Description)).ToList();
        var idx = t.Indexes.Select(i => new IndexInfo(i.Name, i.Columns ?? [], i.Unique, i.PrimaryKey)).ToList();
        return new TableDetail(t.TableName, t.IsView, t.Description, cols, t.PrimaryKeys.Select(c => c.ColumnName).ToList(), idx);
    }

    /// <summary>把底层异常映射为 sql_error（预检开启时才可能映射 driver_missing）</summary>
    internal static T Wrap<T>(ResolvedConnection rc, Func<T> fn)
    {
        try { return fn(); }
        catch (DbAdminException) { throw; }
        catch (Exception ex)
        {
            if (DriverCatalog.ProbeEnabled && DriverCatalog.IsDriverException(ex))
            {
                var def = DriverCatalog.Find(rc.Provider);
                throw new DbAdminException(DbErrorCodes.DriverMissing, def != null ? DriverCatalog.DriverMissingMessage(def) : "驱动缺失");
            }
            throw new DbAdminException(DbErrorCodes.SqlError, ErrorMessageOf(ex));
        }
    }

    /// <summary>底层异常 → 可展示错误文本：消息为空时回退异常类型名，保证「连接失败必有提示」</summary>
    private static String ErrorMessageOf(Exception ex)
    {
        var msg = SqlRedactor.RedactText(ex.GetBaseException().Message);
        return String.IsNullOrWhiteSpace(msg) ? ex.GetBaseException().GetType().Name : msg;
    }
    #endregion

    #region 多数据库
    /// <summary>
    /// 枚举连接可见的数据库（mysql / sqlserver / postgresql）或 schema（Oracle，kind=schema，默认 schema=登录用户名）。
    /// SQLite 恒返回 [main]（kind=main）。系统连接不支持枚举（invalid_request）。
    /// </summary>
    public DatabaseList ListDatabases(String id)
    {
        if (id.StartsWith(SystemIdPrefix, StringComparison.Ordinal))
            throw Invalid("系统连接不支持枚举数据库（sys:* 恒为单库只读）");
        var e = LoadUser(id);
        var def = DriverCatalog.Find(e.Provider) ?? throw Invalid($"不支持的 provider：{e.Provider}");
        if (def.Id == "sqlite")
            return new DatabaseList(id, "sqlite", e.Database, [new DatabaseInfo("main", "main", true, false)]);
        var rc = Resolve(id);
        var defaultDb = def.Id == "oracle" ? e.UserName?.Trim() : (String.IsNullOrWhiteSpace(e.Database) ? null : e.Database!.Trim());
        (String sql, String col, String kind) = def.Id switch
        {
            "mysql" => ("SHOW DATABASES", "Database", "database"),
            "sqlserver" => ("SELECT name FROM sys.databases WHERE state = 0 AND HAS_DBACCESS(name) = 1 ORDER BY name", "name", "database"),
            "postgresql" => ("SELECT datname FROM pg_database WHERE datallowconn AND NOT datistemplate ORDER BY datname", "datname", "database"),
            "oracle" => ("SELECT username FROM all_users ORDER BY username", "username", "schema"),
            _ => throw Invalid($"不支持的 provider：{def.Id}"),
        };
        var dt = Wrap(rc, () => DAL.Create(rc.ConnName).Query(sql, (Dictionary<String, Object?>?)null));
        var cols = (dt.Columns ?? []).ToList();
        var idx = cols.FindIndex(c => c.Equals(col, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) idx = 0;
        var items = (dt.Rows ?? [])
            .Select(r => r.ElementAtOrDefault(idx)?.ToString() ?? "")
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(n => new DatabaseInfo(n, kind,
                defaultDb != null && String.Equals(n, defaultDb, def.Id == "sqlserver" ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
                IsSystemDatabase(def.Id, n)))
            .ToList();
        return new DatabaseList(id, def.Id, defaultDb, items);
    }

    /// <summary>系统库标记（仅提示，不禁止访问）</summary>
    private static Boolean IsSystemDatabase(String provider, String n) => provider switch
    {
        "mysql" => n.Equals("information_schema", StringComparison.OrdinalIgnoreCase) || n.Equals("mysql", StringComparison.OrdinalIgnoreCase)
            || n.Equals("performance_schema", StringComparison.OrdinalIgnoreCase) || n.Equals("sys", StringComparison.OrdinalIgnoreCase),
        "sqlserver" => n.Equals("master", StringComparison.OrdinalIgnoreCase) || n.Equals("tempdb", StringComparison.OrdinalIgnoreCase)
            || n.Equals("model", StringComparison.OrdinalIgnoreCase) || n.Equals("msdb", StringComparison.OrdinalIgnoreCase),
        "postgresql" => n.Equals("postgres", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };
    #endregion
}
