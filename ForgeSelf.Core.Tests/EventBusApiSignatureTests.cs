using System.Reflection;
using Xunit;

namespace ForgeSelf.Core.Tests;

/// <summary>
/// B3（040）门禁：<see cref="IEventBus"/> 公开签名集合必须与设计基线<b>完全一致</b>。
/// 冒泡只允许改内部派发顺序，不允许动公开契约；本测试把「签名零变化」固化成断言。
/// </summary>
public class EventBusApiSignatureTests
{
    /// <summary>
    /// 基线签名集合（B3 改动前的 <see cref="IEventBus"/> 形态）。
    /// 格式：<c>方法名`泛型元数|返回类型名|参数类型名（逗号分隔）</c>；顺序无关。
    /// </summary>
    private static readonly string[] Baseline =
    {
        "EmitAsync`1|Task|String,TEvent",
        "WaterfallAsync`2|Task`1|String,TEvent,Func`1",
        "ParallelAsync`1|Task|String,TEvent",
        "SerialAsync`2|Task`1|String,TEvent",
        "On`1|IDisposable|String,Func`2",
        "OnSerial`2|IDisposable|String,Func`2",
        "OnWaterfall`2|IDisposable|String,Func`3",
    };

    [Fact]
    public void IEventBus_ApiSignature_Unchanged()
    {
        var actual = typeof(IEventBus)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(Describe)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(Baseline.OrderBy(s => s, StringComparer.Ordinal).ToArray(), actual);
    }

    [Fact]
    public void EventBus_ImplementsIEventBus_WithoutExtraPublicSurface()
    {
        // 具体实现不得新增 IEventBus 之外的公开方法（冒泡的 SetParent 必须是 internal）
        var iface = typeof(IEventBus).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name).ToHashSet(StringComparer.Ordinal);

        var extra = typeof(EventBus)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !iface.Contains(m.Name) && m.Name != "Dispose" && !m.IsSpecialName)
            .Select(m => m.Name)
            .ToArray();

        Assert.Empty(extra);
        Assert.True(typeof(IEventBus).IsAssignableFrom(typeof(EventBus)));
    }

    [Fact]
    public void EventBus_SetParent_IsNotPublic()
    {
        // 挂载父总线的入口必须是 internal（宿主经 InternalsVisibleTo 调用），不得进公开契约
        var setParent = typeof(EventBus).GetMethod("SetParent",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(setParent);
        Assert.False(setParent!.IsPublic);
    }

    private static string Describe(MethodInfo m)
    {
        var arity = m.IsGenericMethodDefinition ? m.GetGenericArguments().Length : 0;
        var parameters = string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name));
        return $"{m.Name}`{arity}|{m.ReturnType.Name}|{parameters}";
    }
}
