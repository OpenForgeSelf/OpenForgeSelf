using ForgeSelf.Api.Plugins.DevTools.Models;

namespace ForgeSelf.Api.Plugins.DevTools.Services;

public interface IUuidService
{
    Task<UuidGenerateResult> GenerateUuidAsync(string version, int count, bool uppercase, bool withHyphens);
    Task<SnowflakeGenerateResult> GenerateSnowflakeIdAsync(long workerId, long datacenterId, int count);
    Task<UuidConvertResult> UuidToGuidAsync(string uuid, bool uppercase, bool withHyphens);
}
