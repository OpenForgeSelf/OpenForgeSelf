using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Log;
using OpenForgeSelf.Backend.Plugins.ProxyCapture.Data.Entities;
using System.Reflection;
using XCode;

namespace OpenForgeSelf.Backend.Plugins.ProxyCapture.Core;

/// <summary>
/// 抓包引擎（静态单例）：管理 TCP 监听器生命周期、嗅探协议并分发给对应处理器、
/// 将抓包记录持久化到插件独立 SQLite 库（XCode，连接名 ProxyCapture）。不使用 IHostedService，避免与宿主其他插件冲突。
/// </summary>
public class CaptureEngine
{
    /// <summary>插件独立数据目录（CA 证书与 ProxyCapture.db 所在）。默认程序目录 ProxyCaptureData，可由宿主按运行形态覆盖。</summary>
    public static string DataDirectory => _dataDirectory ?? Path.Combine(AppContext.BaseDirectory, "ProxyCaptureData");

    /// <summary>设置插件数据目录（须在 <see cref="Instance"/> 创建 / <see cref="StartAll"/> 前调用）。</summary>
    public static void SetDataDirectory(string dir) => _dataDirectory = dir;

    private static string? _dataDirectory;

    private static CaptureEngine? _instance;
    public static CaptureEngine Instance => _instance ??= new CaptureEngine();

    private readonly CertificateAuthority _ca;
    private readonly List<IProtocolHandler> _handlers = new();
    private readonly ConcurrentDictionary<int, ActiveListener> _active = new();
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    private CaptureEngine()
    {
        Directory.CreateDirectory(DataDirectory);
        _ca = new CertificateAuthority(DataDirectory);
        _ca.GetCaCertificate(); // 确保 CA 就绪（首次生成）

        // 处理器顺序：TLS 优先，其次 HTTP，RAW 兜底（CanHandle 恒真，必须最后）
        _handlers.Add(new HttpsMitmHandler(_ca));
        _handlers.Add(new HttpCaptureHandler());
        _handlers.Add(new RawTunnelHandler());
    }

    /// <summary>CA 公钥证书（.cer）路径，供前端下载安装。</summary>
    public string CaCertificatePem => _ca.CaCerPath;

    /// <summary>启动所有「已启用」的监听器（插件初始化时调用）。</summary>
    public void StartAll()
    {
        try
        {
            EnsureDatabase();
            foreach (var cfg in ListenerConfig.FindAll(ListenerConfig._.Enabled == true))
            {
                try
                {
                    StartListener(cfg);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("[ProxyCapture] 启动监听器[{0}]失败: {1}", cfg.Name, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ProxyCapture] 初始化数据库失败: {0}", ex.Message);
        }
    }

    /// <summary>停止全部监听器（插件卸载时调用）。</summary>
    public void StopAll()
    {
        foreach (var id in _active.Keys.ToList())
        {
            StopListener(id);
        }
    }

    public bool IsRunning(int id) => _active.ContainsKey(id);

    /// <summary>按配置启动一个监听器（若已存在则先停止）。</summary>
    public void StartListener(ListenerConfig cfg)
    {
        StopListener(cfg.Id);
        if (!cfg.Enabled) return;
        if (cfg.ListenPort <= 0 || cfg.ListenPort > 65535) return;

        // 保证库表就绪：StartListener 是独立入口（如创建监听器即启用），不能依赖 StartAll 先建表
        EnsureDatabase();

        if (!IPAddress.TryParse(cfg.ListenAddress, out var ip))
        {
            ip = IPAddress.Any; // 0.0.0.0 / 任意非法值 → 绑定全部
        }

        var listener = new TcpListener(ip, cfg.ListenPort);
        listener.Start();
        var cts = new CancellationTokenSource();
        _active[cfg.Id] = new ActiveListener { Listener = listener, Cts = cts, Config = cfg };
        XTrace.Log.Info("[ProxyCapture] 监听器已启动: {0} -> {1}:{2}", cfg.Name, cfg.ListenAddress, cfg.ListenPort);

        _ = Task.Run(async () => await AcceptLoopAsync(_active[cfg.Id], cts.Token));
    }

    /// <summary>幂等建库建表（StartAll 与 StartListener 共用）。</summary>
    private void EnsureDatabase()
    {
        try
        {
            CreateTable<ListenerConfig>();
            CreateTable<CaptureSession>();
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ProxyCapture] 初始化数据库失败: {0}", ex.Message);
        }
    }

    /// <summary>通过反射调用实体 <c>Meta.CreateTable()</c>（XCode 的 <c>CreateTable</c> 是运行时方法，
    /// 编译期不在 <c>Meta</c> 返回类型上直接可见；与 <see cref="OpenForgeSelf.Backend.Data.XCodeConfig"/> 同源做法）。</summary>
    private static void CreateTable<T>() where T : Entity<T>, new()
    {
        var metaProp = typeof(T).GetProperty("Meta", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (metaProp == null) return;
        var meta = metaProp.GetValue(null);
        var createTable = meta?.GetType().GetMethod("CreateTable", Type.EmptyTypes);
        createTable?.Invoke(meta, null);
    }

    /// <summary>停止指定监听器。</summary>
    public void StopListener(int id)
    {
        if (_active.TryRemove(id, out var active))
        {
            try { active.Cts.Cancel(); } catch { }
            try { active.Listener.Stop(); } catch { }
            XTrace.Log.Info("[ProxyCapture] 监听器已停止: {0}", id);
        }
    }

    private async Task AcceptLoopAsync(ActiveListener active, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await active.Listener.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                if (ct.IsCancellationRequested) break;
                continue;
            }

            if (client == null) continue;
            _ = Task.Run(async () => await HandleClientAsync(client, active.Config, ct));
        }
    }

    private async Task HandleClientAsync(TcpClient client, ListenerConfig cfg, CancellationToken ct)
    {
        try
        {
            client.ReceiveTimeout = 0;
            client.SendTimeout = 30000;
            var stream = client.GetStream();

            // 嗅探首字节：0x16=TLS；大写字母=疑似 HTTP 方法；其余交给 RAW 兜底
            var first = new byte[1];
            var n = await stream.ReadAsync(first, 0, 1, ct);
            if (n == 0)
            {
                client.Close();
                return;
            }

            var detection = new ProtocolDetection { FirstByte = first };
            if (first[0] == 0x16)
            {
                detection.IsTls = true;
            }
            else if (first[0] >= 0x41 && first[0] <= 0x5A)
            {
                // 大写字母 A-Z：HTTP 方法起始（GET/POST/...）
                detection.IsHttp = true;
            }

            // 还原含首字节的完整流，交给处理器
            var prefix = new PrefixStream(stream, first);
            var handler = _handlers.First(h => h.CanHandle(detection));
            await handler.HandleAsync(prefix, client, cfg, detection, ct);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[ProxyCapture] 连接处理异常: {0}", ex.Message);
            try { client.Close(); } catch { }
        }
    }

    /// <summary>供处理器创建一条抓包记录（填充监听器/客户端等元数据）。</summary>
    public CaptureSession NewRecord(ListenerConfig cfg, string protocol, TcpClient client)
    {
        var ep = client.Client.RemoteEndPoint as IPEndPoint;
        return new CaptureSession
        {
            ListenerId = cfg.Id,
            Timestamp = DateTime.UtcNow,
            Protocol = protocol,
            ClientIp = ep?.Address.ToString() ?? string.Empty,
            LocalEndpoint = $"{cfg.ListenAddress}:{cfg.ListenPort}",
            Forwarded = false
        };
    }

    /// <summary>持久化一条抓包记录（串行化写入，避免 SQLite 并发锁）。</summary>
    public void SaveRecord(CaptureSession record)
    {
        try
        {
            _dbLock.Wait();
            try
            {
                record.DurationMs = (long)(DateTime.UtcNow - record.Timestamp).TotalMilliseconds;
                record.Insert();
            }
            finally
            {
                _dbLock.Release();
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ProxyCapture] 保存抓包记录失败: {0}", ex.Message);
        }
    }
}
