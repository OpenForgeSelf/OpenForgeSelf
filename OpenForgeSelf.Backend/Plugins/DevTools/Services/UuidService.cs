using System.Net.NetworkInformation;
using System.Security.Cryptography;
using OpenForgeSelf.Backend.Plugins.DevTools.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.DevTools.Services;

public class UuidService : IUuidService
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static long _lastTimestamp = -1;
    private static long _sequence = 0;
    private static readonly object _snowflakeLock = new();

    public Task<UuidGenerateResult> GenerateUuidAsync(string version, int count, bool uppercase, bool withHyphens)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 生成UUID，版本: {0}, 数量: {1}", version, count);

            if (count <= 0 || count > 100)
                throw new ArgumentException("数量必须在 1-100 之间");

            var ids = new List<string>();

            for (int i = 0; i < count; i++)
            {
                Guid guid;

                switch (version.ToLowerInvariant())
                {
                    case "v1":
                        guid = GenerateUuidV1();
                        break;
                    case "v4":
                        guid = Guid.NewGuid();
                        break;
                    default:
                        throw new ArgumentException($"不支持的UUID版本: {version}，支持 v1, v4");
                }

                var formatted = FormatUuid(guid, uppercase, withHyphens);
                ids.Add(formatted);
            }

            return Task.FromResult(new UuidGenerateResult
            {
                Ids = ids,
                Version = version,
                Count = count
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] UUID生成失败: {0}", ex.Message);
            throw new ArgumentException("UUID生成失败: " + ex.Message, nameof(version), ex);
        }
    }

    public Task<SnowflakeGenerateResult> GenerateSnowflakeIdAsync(long workerId, long datacenterId, int count)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 生成雪花ID，数量: {0}", count);

            if (count <= 0 || count > 100)
                throw new ArgumentException("数量必须在 1-100 之间");
            if (workerId < 0 || workerId > 31)
                throw new ArgumentException("工作ID必须在 0-31 之间");
            if (datacenterId < 0 || datacenterId > 31)
                throw new ArgumentException("数据中心ID必须在 0-31 之间");

            var ids = new List<SnowflakeIdInfo>();

            for (int i = 0; i < count; i++)
            {
                var idInfo = GenerateSnowflakeId(workerId, datacenterId);
                ids.Add(idInfo);
            }

            return Task.FromResult(new SnowflakeGenerateResult
            {
                Ids = ids,
                Count = count
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 雪花ID生成失败: {0}", ex.Message);
            throw new ArgumentException("雪花ID生成失败: " + ex.Message, nameof(workerId), ex);
        }
    }

    public Task<UuidConvertResult> UuidToGuidAsync(string uuid, bool uppercase, bool withHyphens)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 转换UUID格式");

            if (string.IsNullOrWhiteSpace(uuid))
                throw new ArgumentException("UUID不能为空");

            var guid = Guid.Parse(uuid);
            var formatted = FormatUuid(guid, uppercase, withHyphens);

            return Task.FromResult(new UuidConvertResult
            {
                Result = formatted
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] UUID转换失败: {0}", ex.Message);
            throw new ArgumentException("UUID转换失败: " + ex.Message, nameof(uuid), ex);
        }
    }

    private static Guid GenerateUuidV1()
    {
        var timestamp = DateTime.UtcNow - new DateTime(1582, 10, 15, 0, 0, 0, DateTimeKind.Utc);
        var timestampTicks = timestamp.Ticks;

        var timestampBytes = BitConverter.GetBytes(timestampTicks);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(timestampBytes);

        var clockSequence = new byte[2];
        RandomNumberGenerator.Fill(clockSequence);

        clockSequence[0] = (byte)((clockSequence[0] & 0x3F) | 0x80);

        var nodeBytes = GetNodeBytes();

        var guidBytes = new byte[16];

        Array.Copy(timestampBytes, 4, guidBytes, 0, 4);
        Array.Copy(timestampBytes, 2, guidBytes, 4, 2);
        Array.Copy(timestampBytes, 0, guidBytes, 6, 2);

        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x10);

        Array.Copy(clockSequence, 0, guidBytes, 8, 2);
        Array.Copy(nodeBytes, 0, guidBytes, 10, 6);

        return new Guid(guidBytes);
    }

    private static byte[] GetNodeBytes()
    {
        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var nic in networkInterfaces)
            {
                if (nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    var mac = nic.GetPhysicalAddress().GetAddressBytes();
                    if (mac.Length == 6)
                        return mac;
                }
            }
        }
        catch
        {
        }

        var randomNode = new byte[6];
        RandomNumberGenerator.Fill(randomNode);
        randomNode[0] |= 0x01;
        return randomNode;
    }

    private static SnowflakeIdInfo GenerateSnowflakeId(long workerId, long datacenterId)
    {
        const long twepoch = 1288834974657L;
        const int workerIdBits = 5;
        const int datacenterIdBits = 5;
        const int sequenceBits = 12;

        const long sequenceMask = -1L ^ (-1L << sequenceBits);

        const int workerIdShift = sequenceBits;
        const int datacenterIdShift = sequenceBits + workerIdBits;
        const int timestampLeftShift = sequenceBits + workerIdBits + datacenterIdBits;

        lock (_snowflakeLock)
        {
            var timestamp = GetCurrentTimestamp();

            if (timestamp < _lastTimestamp)
            {
                throw new InvalidOperationException("时钟回拨，无法生成ID");
            }

            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & sequenceMask;
                if (_sequence == 0)
                {
                    timestamp = WaitNextMillis(_lastTimestamp);
                }
            }
            else
            {
                _sequence = 0;
            }

            _lastTimestamp = timestamp;

            long id = ((timestamp - twepoch) << timestampLeftShift)
                      | (datacenterId << datacenterIdShift)
                      | (workerId << workerIdShift)
                      | _sequence;

            var idTimestamp = UnixEpoch.AddMilliseconds(timestamp);

            return new SnowflakeIdInfo
            {
                Id = id,
                Timestamp = idTimestamp,
                WorkerId = workerId,
                DatacenterId = datacenterId,
                Sequence = _sequence
            };
        }
    }

    private static long GetCurrentTimestamp()
    {
        return (long)(DateTime.UtcNow - UnixEpoch).TotalMilliseconds;
    }

    private static long WaitNextMillis(long lastTimestamp)
    {
        var timestamp = GetCurrentTimestamp();
        while (timestamp <= lastTimestamp)
        {
            timestamp = GetCurrentTimestamp();
        }
        return timestamp;
    }

    private static string FormatUuid(Guid guid, bool uppercase, bool withHyphens)
    {
        var format = withHyphens ? "D" : "N";
        var result = guid.ToString(format);
        return uppercase ? result.ToUpperInvariant() : result.ToLowerInvariant();
    }
}
