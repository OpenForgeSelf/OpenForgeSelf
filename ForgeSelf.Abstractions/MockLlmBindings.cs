namespace ForgeSelf.Abstractions;

/// <summary>
/// Mock 规则里的 <c>{{cwd}}</c> 从这里取当前项目根。
/// 智能体插件在启动时把工作区根接上。管道只读，不自己猜目录。
/// </summary>
public static class MockLlmBindings
{
    /// <summary>当前项目根绝对路径。未设置或返回空时，不替换 <c>{{cwd}}</c>。</summary>
    public static Func<string?>? ProjectRoot { get; set; }
}
