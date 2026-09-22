using AIChatMessageEntity = ForgeSelf.Api.Plugins.AIAgent.Entities.AIChatMessage;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins;

/// <summary>
/// T2 会话管理后端单测：会话聚合（GET /sessions 数据源）+ 删除（DELETE /session/{id} 数据源）。
///
/// 隔离模式照搬 AgentHub：<see cref="CollectionAttribute"/>("XCode") + <see cref="IClassFixture{T}"/>，
/// 与所有 XCode 测试同集合串行，避免全局连接串（"AIAgent" 等）被并行类覆盖导致串库。
/// 每个用例先 <see cref="XCodeTestFixture.ClearAllData"/> 清空本类专属临时库，再插入、断言。
///
/// 关键守卫（T3 回归）：后端聚合/删除一律以「存储的全 id」精确匹配，绝不做前缀剥离——
/// 前端切换/删除回传全 id，后端必须原样命中，否则会复现「切回历史会话查 0 条」的历史 bug。
/// </summary>
[Collection("XCode")]
public class SessionManagementTests : IClassFixture<XCodeTestFixture>
{
    private readonly XCodeTestFixture _fixture;
    private readonly PluginMessageService _svc = new();

    public SessionManagementTests(XCodeTestFixture fixture)
    {
        _fixture = fixture;
        XCodeTestFixture.ClearAllData();
        // 显式清空本插件消息表：XCode 集合内多测试类共享 "AIAgent" 临时库时，
        // AddConnStr 不会覆盖已初始化的连接，ClearAllData 可能清错目录导致串库残留。
        // 直删「当前生效连接」的 AIChatMessage，确保每条用例起点干净（与夹具清理互补）。
        foreach (var m in AIChatMessageEntity.FindAll().ToArray()) m.Delete();
        // 会话元数据（归档软标记）同库同清理：残留归档行会让「未归档」断言假失败。
        foreach (var s in AIChatSession.FindAll().ToArray()) s.Delete();
    }

    /// <summary>插入一条聊天消息（直插实体，绕过业务服务，专注测聚合/删除逻辑）。</summary>
    private static void Insert(string sessionId, string role, string content, int minutesAgo)
    {
        var now = DateTime.Now.AddMinutes(-minutesAgo);
        new AIChatMessageEntity
        {
            SessionId = sessionId,
            Role = role,
            Content = content,
            ToolCallsJson = string.Empty,
            CreateTime = now,
            UpdateTime = now,
        }.Insert();
    }

    [Fact]
    public async Task GetSessionsAsync_多会话聚合_按最后消息时间倒序()
    {
        // 注意：AIChatMessage 静态构造函数注册了 TimeInterceptor，插入时会把 CreateTime 重写为
        // 真实 Now（与显式 minutesAgo 偏移无关）。故这里用「真实时间间隔」模拟生产语义：
        // sessB 比 sessA 更晚收到消息 → 最后消息时间更新 → 聚合排序应排最前。
        Insert("sessA", "user", "第一条用户消息 A", 0);
        Insert("sessA", "assistant", "回复 A", 0);
        await Task.Delay(1500);
        Insert("sessB", "user", "你好 B", 0);

        var sessions = await _svc.GetSessionsAsync();

        Assert.Equal(2, sessions.Count);
        // 倒序：sessB（最后消息更晚）应排第一；sessA 第二
        Assert.Equal("sessB", sessions[0].SessionId);
        Assert.Equal("sessA", sessions[1].SessionId);
        Assert.Equal(2, sessions.First(s => s.SessionId == "sessA").MessageCount);
        Assert.Equal(1, sessions.First(s => s.SessionId == "sessB").MessageCount);
    }

    [Fact]
    public async Task GetSessionsAsync_标题取首条用户消息前20字_无用户消息回退新会话()
    {
        Insert("sessX", "assistant", "只有助手消息", 5);
        Insert("sessY", "user", "这是一条很长的用户消息用于测试标题截断逻辑是否生效", 3);

        var sessions = await _svc.GetSessionsAsync();
        var x = sessions.First(s => s.SessionId == "sessX");
        var y = sessions.First(s => s.SessionId == "sessY");

        Assert.Equal("新会话", x.Title);
        // 前 20 字 + 省略号，总长 ≤ 21
        Assert.StartsWith("这是一条很长的用户消息用于测试标题截断", y.Title);
        Assert.EndsWith("…", y.Title);
        Assert.True(y.Title.Length <= 21, $"标题过长：{y.Title}");
    }

    [Fact]
    public async Task GetSessionsAsync_返回存储的全id_不做前缀剥离()
    {
        // 真实形态：时间戳 base36 - 随机 6 位后缀（与前端 newSessionId 一致）
        const string fullId = "mu7p581h-6oscch";
        Insert(fullId, "user", "hi", 5);

        var sessions = await _svc.GetSessionsAsync();

        // 聚合以 g.Key（存储的全 id）为 SessionId，原样返回 → 前端全 id 切换才能命中
        Assert.Contains(sessions, s => s.SessionId == fullId);
    }

    [Fact]
    public async Task DeleteSessionAsync_删除后该会话清空_不影响其它会话()
    {
        Insert("sessDel", "user", "待删消息", 10);
        Insert("sessKeep", "user", "保留消息", 9);

        await _svc.DeleteSessionAsync("sessDel");

        var remaining = AIChatMessageEntity.FindAll();
        Assert.DoesNotContain(remaining, m => m.SessionId == "sessDel");
        Assert.Contains(remaining, m => m.SessionId == "sessKeep");
        Assert.Single(remaining, m => m.SessionId == "sessKeep");
    }

    [Fact]
    public async Task DeleteSessionAsync_按全id精确匹配_同前缀不同后缀不误删()
    {
        // T3 回归守卫：删除必须按完整 id 精确匹配，不能按前缀（否则会误删 mu7p581h-* 全部）
        const string fullId = "mu7p581h-6oscch";
        const string sibling = "mu7p581h-abcdef";
        Insert(fullId, "user", "del", 5);
        Insert(sibling, "user", "keep", 4);

        await _svc.DeleteSessionAsync(fullId);

        var remaining = AIChatMessageEntity.FindAll();
        Assert.DoesNotContain(remaining, m => m.SessionId == fullId);
        Assert.Contains(remaining, m => m.SessionId == sibling);
    }

    [Fact]
    public async Task DeleteSessionAsync_空会话不报错()
    {
        // 删除不存在的会话应静默成功，不抛异常（前端删除按钮的幂等保护）
        await _svc.DeleteSessionAsync("不存在的会话");
    }

    /* ------------------------------------------------------------------ */
    /* 归档（软标记，T4 会话管理改造）：agent 页默认不展示已归档会话          */
    /* ------------------------------------------------------------------ */

    [Fact]
    public async Task ArchiveSessionAsync_归档后默认列表不含_取消归档后回归()
    {
        Insert("sessArch", "user", "待归档会话", 5);

        Assert.True(await _svc.ArchiveSessionAsync("sessArch", true));

        // 默认 = 仅未归档（agent 页）：归档后不再出现
        var active = await _svc.GetSessionsAsync();
        Assert.DoesNotContain(active, s => s.SessionId == "sessArch");

        // 仅已归档视图能看到，且带 archived 标记
        var archived = await _svc.GetSessionsAsync(SessionArchivedFilter.Archived);
        var row = archived.FirstOrDefault(s => s.SessionId == "sessArch");
        Assert.NotNull(row);
        Assert.True(row!.Archived);

        // 取消归档 → 回归默认列表
        Assert.True(await _svc.ArchiveSessionAsync("sessArch", false));
        var active2 = await _svc.GetSessionsAsync();
        var back = active2.FirstOrDefault(s => s.SessionId == "sessArch");
        Assert.NotNull(back);
        Assert.False(back!.Archived);
    }

    [Fact]
    public async Task GetSessionsAsync_归档筛选_active_archived_all()
    {
        Insert("sessKeep", "user", "未归档", 5);
        Insert("sessHidden", "user", "已归档", 4);
        await _svc.ArchiveSessionAsync("sessHidden", true);

        var active = await _svc.GetSessionsAsync(SessionArchivedFilter.Active);
        var archived = await _svc.GetSessionsAsync(SessionArchivedFilter.Archived);
        var all = await _svc.GetSessionsAsync(SessionArchivedFilter.All);

        Assert.Equal(new[] { "sessKeep" }, active.Select(s => s.SessionId).ToArray());
        Assert.Equal(new[] { "sessHidden" }, archived.Select(s => s.SessionId).ToArray());
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task ArchiveSessionAsync_软标记_不删除任何消息()
    {
        // 归档 ≠ 删除：消息必须完整保留（与 DeleteSessionAsync 的硬删相对）
        Insert("sessSoft", "user", "归档后消息仍在", 5);
        Insert("sessSoft", "assistant", "回复也仍在", 4);

        await _svc.ArchiveSessionAsync("sessSoft", true);

        var rows = AIChatMessageEntity.FindAll().Where(m => m.SessionId == "sessSoft").ToArray();
        Assert.Equal(2, rows.Length);
    }

    [Fact]
    public async Task ArchiveSessionAsync_幂等_重复设置同值返回false()
    {
        Insert("sessIdem", "user", "幂等用例", 5);

        Assert.True(await _svc.ArchiveSessionAsync("sessIdem", true));
        Assert.False(await _svc.ArchiveSessionAsync("sessIdem", true));
        Assert.True(await _svc.ArchiveSessionAsync("sessIdem", false));
        Assert.False(await _svc.ArchiveSessionAsync("sessIdem", false));
    }

    [Fact]
    public async Task ArchiveSessionAsync_全id精确匹配_同前缀不同后缀不误伤()
    {
        // 与删除同一守卫：归档按完整 id 命中，不能前缀匹配
        const string fullId = "mu7p581h-6oscch";
        const string sibling = "mu7p581h-abcdef";
        Insert(fullId, "user", "归档我", 5);
        Insert(sibling, "user", "别动我", 4);

        await _svc.ArchiveSessionAsync(fullId, true);

        var active = await _svc.GetSessionsAsync();
        Assert.DoesNotContain(active, s => s.SessionId == fullId);
        Assert.Contains(active, s => s.SessionId == sibling);
    }

    [Fact]
    public async Task ArchiveSessionAsync_空id_返回false不落库()
    {
        Assert.False(await _svc.ArchiveSessionAsync("", true));
        Assert.Empty(AIChatSession.FindAll());
    }
}
