using ForgeSelf.Abstractions;
using ForgeSelf.Abstractions.Tests;
using ForgeSelf.Api.Services;

namespace ForgeSelf.Api.Tests.Services;

/// <summary>
/// B2（040）双实现契约之一：内存实现跑 Abstractions 侧同一套契约基类。
/// </summary>
/// <remarks>
/// 契约基类 <see cref="SessionStoreContractTestsBase"/> 位于 <c>ForgeSelf.Abstractions.Tests</c>；
/// B2 已为 Api.Tests 补上该 ProjectReference，使内存实现与持久化实现共用同一套断言，
/// 任一实现偏离 <see cref="ISessionStore"/> 契约即在此红。
/// 内存实现在此语境下的定位是<b>测试替身</b>（B2 起不再注册进生产 DI）。
/// </remarks>
public class InMemorySessionStoreContractTests : SessionStoreContractTestsBase
{
    /// <inheritdoc />
    protected override ISessionStore CreateStore() => new InMemorySessionStore();
}
