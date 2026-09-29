using Xunit;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// 进程级环境变量测试隔离集合（B9-4）：改写 <c>FORGESELF_DATA_ROOT</c>/<c>ASPNETCORE_ENVIRONMENT</c> 等
/// 进程级变量的测试类必须挂本集合——xUnit 默认跨类并行，环境变量互踩会导致密封失效型 flake
/// （实测：DataLocationOverrideTests 设置变量与 DataLocationServiceTests 密封变量并行竞争，6 条连红）。
/// DisableParallelization = true 时本集合内类串行，且与其他集合仍并行（只约束集合内部）。
/// </summary>
[CollectionDefinition("EnvVarIsolation", DisableParallelization = true)]
public sealed class EnvVarIsolationCollection
{
}
