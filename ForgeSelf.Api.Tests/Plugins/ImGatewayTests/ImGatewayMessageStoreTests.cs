using ForgeSelf.Api.Plugins.ImGateway.Entities;
using ForgeSelf.Api.Plugins.ImGateway.Services;
using FluentAssertions;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.ImGatewayTests;

/// <summary>
/// ImGatewayMessageStore（PILOT-078 FR-1）单测：
/// - RecordInbound / RecordReply / RecordError 字段落库正确（方向/状态/内容/会话/平台消息ID/时间）；
/// - ErrorText 截断 1000；null 入参安全；
/// - 内部异常被吞不抛（网关可用性优先）：实体校验失败（空 ChannelKey 触发 Valid 抛 ArgumentNullException）
///   与数据库层失败（表缺失触发 no such table）都不外抛，且不产生半截数据。
/// 库管理：XCodeTestFixture（PC-014 唯一入口，测试源码禁手搓连接串）+ [Collection("XCode")] 全进程串行。
/// </summary>
[Collection("XCode")]
public class ImGatewayMessageStoreTests : IClassFixture<XCodeTestFixture>
{
    private readonly XCodeTestFixture _fixture;
    private readonly ImGatewayMessageStore _store = new();

    public ImGatewayMessageStoreTests(XCodeTestFixture fixture)
    {
        _fixture = fixture;

        // 实体缓存清零：防进程早前测试类的残留行被实体缓存读回（Sems/SessionManagement 同款手法）
        ImGatewayMessage.Meta.Cache.Clear("test reset");
        ImGatewayMessage.Meta.Cache.Expire = 0;
        ImGatewaySession.Meta.Cache.Clear("test reset");
        ImGatewaySession.Meta.Cache.Expire = 0;

        Purge();
    }

    /// <summary>清空本类涉及的 ImGateway 三表（带条件查询走库，不依赖实体缓存）。</summary>
    private static void Purge()
    {
        foreach (var row in ImGatewayMessage.FindAll(ImGatewayMessage._.Id > 0)) row.Delete();
        foreach (var row in ImGatewaySession.FindAll(ImGatewaySession._.Id > 0)) row.Delete();
        foreach (var row in ImGatewayProcessedMsg.FindAll(ImGatewayProcessedMsg._.Id > 0)) row.Delete();
    }

    // ---- 字段落库正确性（FR-1 三个落库入口）----

    [Fact]
    public void RecordInbound_字段按FR1落库_方向in状态ok()
    {
        Purge();
        var key = $"wecom:conv-store-in:user-{Guid.NewGuid():N}";

        _store.RecordInbound(key, "wecom", "你好，请帮我查一下", "msg-in-1001", "sess-in-abc");

        var row = ImGatewayMessage.FindAll(ImGatewayMessage._.ChannelKey == key).Single();
        row.Direction.Should().Be(ImGatewayMessage.DirectionIn, "入站记录 direction=in");
        row.Status.Should().Be(ImGatewayMessage.StatusOk, "入站记录 status=ok");
        row.Content.Should().Be("你好，请帮我查一下");
        row.PlatformMsgId.Should().Be("msg-in-1001");
        row.SessionId.Should().Be("sess-in-abc");
        row.ChannelKey.Should().Be(key);
        row.ChannelType.Should().Be("wecom");
        row.ErrorText.Should().BeNull("成功入站不带错误摘要");
        row.CreatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(10), "CreatedAt 取落库时刻");
    }

    [Fact]
    public void RecordReply_字段按FR1落库_方向out状态ok()
    {
        Purge();
        var key = $"wecom:conv-store-out:user-{Guid.NewGuid():N}";

        _store.RecordReply(key, "wecom", "这是AI的回复", "sess-out-abc");

        var row = ImGatewayMessage.FindAll(ImGatewayMessage._.ChannelKey == key).Single();
        row.Direction.Should().Be(ImGatewayMessage.DirectionOut);
        row.Status.Should().Be(ImGatewayMessage.StatusOk);
        row.Content.Should().Be("这是AI的回复");
        row.SessionId.Should().Be("sess-out-abc");
        row.PlatformMsgId.Should().BeNull("回复记录无平台消息ID");
        row.ChannelType.Should().Be("wecom");
        row.CreatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void RecordError_方向out状态error_内容空_ErrorText为摘要()
    {
        Purge();
        var key = $"wecom:conv-store-err:user-{Guid.NewGuid():N}";

        _store.RecordError(key, "wecom", "sess-err-abc", "Service request failed. Status: 404 (Not Found)");

        var row = ImGatewayMessage.FindAll(ImGatewayMessage._.ChannelKey == key).Single();
        row.Direction.Should().Be(ImGatewayMessage.DirectionOut);
        row.Status.Should().Be(ImGatewayMessage.StatusError);
        row.Content.Should().BeEmpty("失败回复不携带正文");
        row.ErrorText.Should().Be("Service request failed. Status: 404 (Not Found)");
        row.SessionId.Should().Be("sess-err-abc");
    }

    [Fact]
    public void RecordError_超1000字符截断_且null安全()
    {
        Purge();
        var keyLong = $"wecom:conv-store-trunc:user-{Guid.NewGuid():N}";
        var longError = new string('x', 1500) + "-TAIL-MUST-BE-CUT";

        _store.RecordError(keyLong, "wecom", "sess-t", longError);
        var rowLong = ImGatewayMessage.FindAll(ImGatewayMessage._.ChannelKey == keyLong).Single();
        rowLong.ErrorText.Should().HaveLength(1000, "ErrorText 列上限 1000，超出截断");
        rowLong.ErrorText.Should().Be(longError[..1000], "截断保留前 1000 字符");
        rowLong.ErrorText.Should().NotContain("TAIL-MUST-BE-CUT");

        var keyNull = $"wecom:conv-store-null:user-{Guid.NewGuid():N}";
        Action actNull = () => _store.RecordError(keyNull, "wecom", "sess-n", null);
        actNull.Should().NotThrow("null 错误文本安全（落空串）");
        var rowNull = ImGatewayMessage.FindAll(ImGatewayMessage._.ChannelKey == keyNull).Single();
        rowNull.ErrorText.Should().BeEmpty();
        rowNull.Status.Should().Be(ImGatewayMessage.StatusError);
    }

    // ---- 内部异常被吞不抛（业务规则 1：落库为尽力而为）----

    [Fact]
    public void 实体校验失败_空ChannelKey_被吞不抛且零残留()
    {
        Purge();

        // ImGatewayMessage.Valid() 对空 ChannelKey 抛 ArgumentNullException——被 Record 的
        // try/catch 吞掉（网关主链可用性优先），且库里不产生半截数据。
        var before = ImGatewayMessage.FindCount(ImGatewayMessage._.Id > 0);
        Action act = () =>
        {
            _store.RecordInbound(string.Empty, "wecom", "x", "m1", "s1");
            _store.RecordReply(string.Empty, "wecom", "y", "s");
            _store.RecordError(string.Empty, "wecom", "s", "e");
        };
        act.Should().NotThrow("落库为尽力而为：任何内部异常只记 XTrace，绝不外抛");

        ImGatewayMessage.FindCount(ImGatewayMessage._.Id > 0).Should().Be(before, "校验失败不得产生半截数据");
    }

    [Fact]
    public void 数据库层失败_表缺失_被吞不抛_主链可用性优先()
    {
        Purge();
        var key = $"wecom:conv-store-dbdrop:user-{Guid.NewGuid():N}";

        // 制造 DB 层失败：删表（不碰连接串——PC-014；Migration 的 CheckTables 在夹具首绑已跑过，
        // 不会自动重建）。Save 应抛 SQLite「no such table」，被 Record 吞掉。
        var dal = DAL.Create("ImGateway");
        if (dal.Tables.Any(t => string.Equals(t.Name, "ImGatewayMessage", StringComparison.OrdinalIgnoreCase)))
        {
            dal.Execute("DROP TABLE ImGatewayMessage");
        }

        try
        {
            Action act = () =>
            {
                _store.RecordInbound(key, "wecom", "x", "m1", "s1");
                _store.RecordReply(key, "wecom", "y", "s1");
                _store.RecordError(key, "wecom", "s1", "e1");
            };
            act.Should().NotThrow("表缺失属数据库层失败，同样必须被吞");
        }
        finally
        {
            // 恢复：SetTables 幂等重建缺失表（XCodeTestFixture 首绑同款入口）
            XCodeTestFixture.EnsureTablesCreated();
        }

        ImGatewayMessage.FindAll(ImGatewayMessage._.ChannelKey == key).Should().BeEmpty("落库全程失败，不得有半截数据");
        // 恢复后表可用（阳性对照：能正常写入一条并读回）
        _store.RecordInbound(key, "wecom", "恢复后可正常落库", "m2", "s2");
        ImGatewayMessage.FindAll(ImGatewayMessage._.ChannelKey == key).Should().HaveCount(1);
    }
}
