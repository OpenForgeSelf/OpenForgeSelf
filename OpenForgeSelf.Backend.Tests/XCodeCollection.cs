using Xunit;

// 全局禁用测试并行：XCode 的 DAL.ConnStrs 与实体 ConnName 均为进程级全局单例，
// 框架不支持"同实体 + 并行 + 各自独立库"。任何碰 XCode 的测试类只要与别的集合并行运行，
// 就会互相覆盖连接串导致状态串扰（参见 I-6 / D-10）。逐类加 [Collection("XCode")] 只能保证
// 集合内串行，无法阻止跨集合并行，且新增类容易漏加而再次串扰（脆弱）。
// 因此从根因消除：整个测试程序集串行执行，保证全量稳定通过、且不随新增测试类退化。
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OpenForgeSelf.Backend.Tests;

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
