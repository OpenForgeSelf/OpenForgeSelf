using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Security;
using Microsoft.Extensions.Configuration;

namespace ForgeSelf.Api.Services.Terminal;

/// <summary>
/// 第二个宿主进程把打开请求交给已经在听的实例，然后退出。
/// 只解密已有令牌，解密失败不重新生成，避免把正在运行的实例的密钥换掉。
/// </summary>
public static class TerminalInstanceForwarder
{
    public static bool TryForward(ParsedTerminalOpen open)
    {
        if (!TryReadCredential(out var port, out var token))
            return false;

        using var handler = new HttpClientHandler { UseProxy = false };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
        var payload = JsonSerializer.Serialize(new
        {
            query = open.Query,
            password = open.Password,
            secretStripped = open.SecretStripped
        });

        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"http://127.0.0.1:{port}/api/smart-terminal/inbound")
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var res = http.Send(req);
                if ((int)res.StatusCode is 200 or 202)
                    return true;
            }
            catch
            {
                // 实例刚占住互斥量、端口尚未开始听
            }

            Thread.Sleep(300);
        }

        return false;
    }

    internal static bool TryReadCredential(out int port, out string token)
    {
        port = 0;
        token = "";
        try
        {
            port = ForgeSetting.Current.PortNumber;
            if (port <= 0) return false;

            var cipher = ForgeSetting.Current.ApiToken;
            if (string.IsNullOrEmpty(cipher)) return false;

            var config = new ConfigurationBuilder()
                .SetBasePath(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                             ?? AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();
            var encryption = new AesSecretEncryptionService(config, new MachineKeyProvider());
            if (!encryption.TryDecrypt(cipher, out var plain) || string.IsNullOrEmpty(plain))
                return false;
            token = plain;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
