using System.Reflection;
using Xunit.Sdk;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// 每个测试方法开跑前清空数据库，保证测试与测试之间零串扰。
///
/// 【为什么需要它】
/// 此前只在 XCodeTestFixture 的**构造函数**里清一次（IClassFixture 是"每测试类一份实例"），
/// 结果两类串扰都盖不住：
///   · 类与类之间：上一个类留下的数据撞下一个类的业务唯一约束
///     （AgentRegistry「同 vendor 只允许登记一次」→ 抛「厂商标识 xxx 已被 xxx 占用」）；
///   · 同一类内部：前一个用例留的数据被后一个用例看到。
/// 改成"每测试清一次"两病同治，且不依赖测试类的执行顺序。
///
/// 【为什么用程序集级 BeforeAfterTestAttribute】
/// xunit 的 assembly-level attribute 会自动作用到**全部**测试方法，
/// 因此不需要改动任何一个已有测试类（避免逐个类加清理代码导致遗漏）。
/// </summary>
public sealed class ClearXCodeDataAttribute : BeforeAfterTestAttribute
{
    /// <summary>测试方法执行前：清空各连接名的业务表数据。</summary>
    public override void Before(MethodInfo methodUnderTest)
    {
        // 首次进来时库与表可能还没建（比如测试本身没用 XCodeTestFixture），先兜底建表
        XCodeTestFixture.EnsureTablesCreated();
        XCodeTestFixture.ClearAllData();
    }
}
