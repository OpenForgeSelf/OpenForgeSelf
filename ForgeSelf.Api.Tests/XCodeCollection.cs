using Xunit;

// 全局禁用测试并行：XCode 的 DAL.ConnStrs 与实体 ConnName 均为进程级全局单例，
// 框架不支持"同实体 + 并行 + 各自独立库"。任何碰 XCode 的测试类只要与别的集合并行运行，
// 就会互相覆盖连接串导致状态串扰（参见 I-6 / D-10）。逐类加 [Collection("XCode")] 只能保证
// 集合内串行，无法阻止跨集合并行，且新增类容易漏加而再次串扰（脆弱）。
// 因此从根因消除：整个测试程序集串行执行，保证全量稳定通过、且不随新增测试类退化。
//
// ⚠️ 关键坑（2026-08-29 实测）：仅有本 attribute 不够——xunit.runner.json 里的
// "parallelizeTestCollections" / "maxParallelThreads" 会覆盖本设置。此前 json 里写的是
// true / -1，导致这里的 DisableTestParallelization 形同虚设，全量跑仍偶发随机红
// （ProxyCaptureE2ETests、ScriptRunnerTests.CancelAsync 等每轮换一批）。
// 现已把 xunit.runner.json 改为 false / 1，两处必须保持一致，改一处不改另一处会静默失效。
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ForgeSelf.Api.Tests;

/// <summary>
/// 串行约束集合：把显式使用 XCodeTestFixture 的测试类归入同一集合，强制集合内串行执行。
/// 仅作串行约束，不声明 ICollectionFixture，从而保留各测试类独立的 XCodeTestFixture 实例（独立数据库目录）。
/// 目的：消除全量并行时 DAL.AddConnStr 同名连接串被互相覆盖导致的状态串扰（参见 I-6 / D-10）。
/// XCode 的 DAL.ConnStrs 与实体 ConnName 均为进程级全局单例，框架不支持"同实体 + 并行 + 各自独立库"，
/// 因此唯一干净解是串行化 + 每类独立库目录。
/// </summary>
[CollectionDefinition("XCode")]
public class XCodeCollection
{
}

/// <summary>
/// 共享进程级全局状态的测试集合：这些类不碰 XCode 连接串，但会改写其它进程级单例
/// （<c>CaptureEngine.Instance</c> 静态单例 + 共享 ProxyCapture.db、NewLife <c>Config&lt;T&gt;.Provider.FileName</c>）。
/// </summary>
/// <remarks>
/// 与 <see cref="XCodeCollection"/> 分开的原因：两者冲突的资源不同（连接串 vs 其它单例），
/// 分开后即便将来有人重新开启并行，也能保证「同类冲突必定串行、无关类仍可并行」。
/// 收录此集合的判据：<b>测试会改写某个进程级全局单例</b>，而非「测试慢」或「测试偶发」。
/// 已知成员：<c>ProxyCaptureE2ETests</c>、<c>ProxyCapturePluginTests</c>（CaptureEngine 单例 +
/// 真实 TCP 端口）、<c>ConfigUnifierTests</c>、<c>ForgeConfigTests</c>（改全局配置文件名）。
/// </remarks>
[CollectionDefinition("SharedGlobalState")]
public class SharedGlobalStateCollection
{
}
