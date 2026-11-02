using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Api.Plugins.ImGateway.Controllers;
using ForgeSelf.Api.Plugins.ImGateway.Core;
using ForgeSelf.Api.Plugins.ImGateway.Entities;
using ForgeSelf.Api.Plugins.ImGateway.Services;
using ForgeSelf.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.ImGatewayTests;

/// <summary>
/// ImGatewayController 查询端点（PILOT-078 FR-2/FR-3/FR-4）单测：
/// - GET conversations：排序（有消息按最后消息时间倒序、无消息按 UpdatedAt 倒序排后）、displayName=ChannelKey 末段、
///   messageCount、preview 截 60 字符、零消息 lastMessage=null、camelCase 契约；
/// - GET messages：按 Id 升序、limit 钳制 1..200、beforeId 游标（不含）+ hasMore、越界不报错、channelKey 缺省 400；
/// - FR-4 鉴权：类级 [Authorize("ApiKeyPolicy")]（反射断言，对齐 McpAdminAuthTests 先例；活体 401/200 由 e2e AC-7 覆盖）。
/// 控制器直接实例化 + 真实 XCode 存储（XCodeTestFixture，PC-014 唯一入口），零 mock 数据层。
/// </summary>
[Collection("XCode")]
public class ImGatewayControllerQueryTests : IClassFixture<XCodeTestFixture>
{
    private readonly XCodeTestFixture _fixture;
    private readonly ImGatewayController _controller;

    public ImGatewayControllerQueryTests(XCodeTestFixture fixture)
    {
        _fixture = fixture;

        ImGatewayMessage.Meta.Cache.Clear("test reset");
        ImGatewayMessage.Meta.Cache.Expire = 0;
        ImGatewaySession.Meta.Cache.Clear("test reset");
        ImGatewaySession.Meta.Cache.Expire = 0;

        Purge();

        // 组装真实依赖图（conversations/messages 只读端点不触碰加密/长连接/扫码）
        var channels = Array.Empty<IImChannel>();
        var ctx = new Mock<IContext>();
        var config = new DbConfigStore(() => null);
        var router = new ImGatewayRouter(ctx.Object, config, new DbSessionStore(), new ImGatewayMessageStore(), channels);
        var connMgr = new ImGatewayConnectionManager(channels, router);
        var scanAuth = new WeComScanAuthService(config, connMgr);
        _controller = new ImGatewayController(channels, router, config, connMgr, scanAuth);
    }

    private static void Purge()
    {
        foreach (var row in ImGatewayMessage.FindAll(ImGatewayMessage._.Id > 0)) row.Delete();
        foreach (var row in ImGatewaySession.FindAll(ImGatewaySession._.Id > 0)) row.Delete();
        foreach (var row in ImGatewayProcessedMsg.FindAll(ImGatewayProcessedMsg._.Id > 0)) row.Delete();
    }

    private static void SeedSession(string channelKey, string sessionId, DateTime updatedAt)
        => new ImGatewaySession { ChannelKey = channelKey, SessionId = sessionId, UpdatedAt = updatedAt }.Insert();

    private static ImGatewayMessage SeedMsg(string channelKey, string channelType, string direction,
        string content, string status, DateTime createdAt, string? errorText = null)
    {
        var m = new ImGatewayMessage
        {
            ChannelKey = channelKey,
            ChannelType = channelType,
            Direction = direction,
            Content = content,
            Status = status,
            ErrorText = errorText,
            CreatedAt = createdAt,
        };
        m.Insert();
        return m;
    }

    /// <summary>把 IActionResult 解成 (状态码, JSON 体 JsonElement)（仿 SemsControllerTests）。</summary>
    private static (int Status, JsonElement Body) Decode(IActionResult result)
    {
        var (status, value) = result switch
        {
            // 本控制器直接返回 JsonResult（不继承 ObjectResult，是平行类），须显式接住
            JsonResult json => (200, json.Value),
            OkObjectResult ok => (ok.StatusCode ?? 200, ok.Value),
            ObjectResult obj => (obj.StatusCode ?? 500, obj.Value),
            _ => throw new InvalidOperationException($"未预期的结果类型：{result.GetType().Name}")
        };
        var body = JsonSerializer.SerializeToElement(value ?? new { }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return (status, body);
    }

    // ---- FR-2 conversations ----

    [Fact]
    public void Conversations_排序显示名计数与camelCase()
    {
        Purge();
        var t0 = DateTime.Now.AddMinutes(-60);

        // A：2 条消息，最新 T-5 → 有消息组第 1
        var keyA = "wecom:conv-a:zhang3";
        SeedSession(keyA, "sess-a", t0);
        SeedMsg(keyA, "wecom", ImGatewayMessage.DirectionIn, "A 的第一条消息", ImGatewayMessage.StatusOk, t0.AddMinutes(-20));
        SeedMsg(keyA, "wecom", ImGatewayMessage.DirectionOut, "A 的回复", ImGatewayMessage.StatusOk, t0.AddMinutes(-5));

        // B：1 条消息，最新 T-30 → 有消息组第 2
        var keyB = "wecom:conv-b:li4";
        SeedSession(keyB, "sess-b", t0);
        SeedMsg(keyB, "wecom", ImGatewayMessage.DirectionIn, "B 的消息", ImGatewayMessage.StatusOk, t0.AddMinutes(-30));

        // C：零消息，UpdatedAt=T-1 → 无消息组（整体排后）
        var keyC = "wecom:conv-c:wang5";
        SeedSession(keyC, "sess-c", t0.AddMinutes(-1));

        var (status, body) = Decode(_controller.Conversations());
        status.Should().Be(200);
        body.ValueKind.Should().Be(JsonValueKind.Array);
        body.GetArrayLength().Should().Be(3);

        // camelCase 契约硬断言（FR-2）：小写键在、PascalCase 键不得出现
        var first = body[0];
        first.TryGetProperty("channelKey", out _).Should().BeTrue("conversations 必须 camelCase");
        first.TryGetProperty("ChannelKey", out _).Should().BeFalse("不得出现 PascalCase 键");

        var keys = body.EnumerateArray().Select(i => i.GetProperty("channelKey").GetString()).ToList();
        keys.Should().ContainInOrder(new[] { keyA, keyB, keyC }, "有消息按最后消息时间倒序，无消息按 UpdatedAt 排后");

        var names = body.EnumerateArray().Select(i => i.GetProperty("displayName").GetString()).ToList();
        names.Should().ContainInOrder(new[] { "zhang3", "li4", "wang5" }, "displayName = ChannelKey 末段");

        body[0].GetProperty("channelType").GetString().Should().Be("wecom", "channelType = ChannelKey 首段");
        body[0].GetProperty("sessionId").GetString().Should().Be("sess-a");

        body[0].GetProperty("messageCount").GetInt32().Should().Be(2);
        body[1].GetProperty("messageCount").GetInt32().Should().Be(1);
        body[2].GetProperty("messageCount").GetInt32().Should().Be(0);

        var lastA = body[0].GetProperty("lastMessage");
        lastA.GetProperty("direction").GetString().Should().Be("out", "lastMessage = 最新一条（out 回复）");
        lastA.GetProperty("status").GetString().Should().Be("ok");
        lastA.GetProperty("preview").GetString().Should().Be("A 的回复");
        lastA.TryGetProperty("createdAt", out _).Should().BeTrue();

        body[2].GetProperty("lastMessage").ValueKind.Should().Be(JsonValueKind.Null, "零消息形态 lastMessage=null（历史库升级）");
        body[2].GetProperty("lastActiveAt").GetDateTime().Should()
            .BeCloseTo(t0.AddMinutes(-1), TimeSpan.FromSeconds(5), "无消息时 lastActiveAt=会话 UpdatedAt");
        body[0].GetProperty("lastActiveAt").GetDateTime().Should()
            .BeCloseTo(t0.AddMinutes(-5), TimeSpan.FromSeconds(5), "有消息时 lastActiveAt=最后消息时间");
    }

    [Fact]
    public void Conversations_preview截60字符()
    {
        Purge();
        var key = $"wecom:conv-p:previewer-{Guid.NewGuid():N}";
        SeedSession(key, "sess-p", DateTime.Now);
        var longText = new string('长', 100);
        SeedMsg(key, "wecom", ImGatewayMessage.DirectionIn, longText, ImGatewayMessage.StatusOk, DateTime.Now.AddSeconds(-1));

        var (status, body) = Decode(_controller.Conversations());
        status.Should().Be(200);
        body.GetArrayLength().Should().Be(1);
        body[0].GetProperty("lastMessage").GetProperty("preview").GetString().Should()
            .Be(longText[..60], "preview 截 60 字符");
    }

    // ---- FR-3 messages ----

    [Fact]
    public void Messages_按Id升序输出_字段保真camelCase()
    {
        Purge();
        var key = $"wecom:conv-m:user-m-{Guid.NewGuid():N}";
        for (var i = 1; i <= 5; i++)
        {
            SeedMsg(key, "wecom",
                i % 2 == 1 ? ImGatewayMessage.DirectionIn : ImGatewayMessage.DirectionOut,
                $"消息{i}",
                i == 4 ? ImGatewayMessage.StatusError : ImGatewayMessage.StatusOk,
                DateTime.Now.AddMinutes(-i),
                i == 4 ? "上游 404: model not found" : null);
        }

        var (status, body) = Decode(_controller.Messages(key));
        status.Should().Be(200);

        // 包裹形状 camelCase：{ messages, hasMore }
        body.TryGetProperty("messages", out _).Should().BeTrue();
        body.TryGetProperty("hasMore", out _).Should().BeTrue();
        body.GetProperty("hasMore").GetBoolean().Should().BeFalse("总数 < limit");

        var arr = body.GetProperty("messages");
        arr.GetArrayLength().Should().Be(5);
        var ids = arr.EnumerateArray().Select(m => m.GetProperty("id").GetInt64()).ToList();
        ids.Should().BeInAscendingOrder("FR-3：按 Id 升序输出（便于直接渲染）");

        var m4 = arr.EnumerateArray().Single(m => m.GetProperty("content").GetString() == "消息4");
        m4.GetProperty("direction").GetString().Should().Be("out");
        m4.GetProperty("status").GetString().Should().Be("error");
        m4.GetProperty("errorText").GetString().Should().Be("上游 404: model not found");
        m4.GetProperty("channelKey").GetString().Should().Be(key);
        m4.GetProperty("channelType").GetString().Should().Be("wecom");
        m4.TryGetProperty("ChannelKey", out _).Should().BeFalse("messages 条目必须 camelCase");

        var m1 = arr.EnumerateArray().First(m => m.GetProperty("content").GetString() == "消息1");
        m1.GetProperty("direction").GetString().Should().Be("in");
        m1.GetProperty("status").GetString().Should().Be("ok");
        m1.GetProperty("errorText").ValueKind.Should().Be(JsonValueKind.Null, "成功消息无错误摘要");
        m1.GetProperty("createdAt").GetDateTime().Should().BeOnOrBefore(DateTime.Now.AddMinutes(1));
    }

    [Fact]
    public void Messages_beforeId游标_不含该条_升序_hasMore正确()
    {
        Purge();
        var key = $"wecom:conv-cursor:user-c-{Guid.NewGuid():N}";
        var ids = new List<long>();
        for (var i = 1; i <= 5; i++)
        {
            var m = SeedMsg(key, "wecom", ImGatewayMessage.DirectionIn, $"m{i}", ImGatewayMessage.StatusOk, DateTime.Now.AddSeconds(-i));
            ids.Add(m.Id);
        }
        ids.Sort();

        // beforeId = 第 4 条（不含）：返回前 3 条升序；窗口内无更多更早 → hasMore=false
        var (_, body1) = Decode(_controller.Messages(key, 50, ids[3]));
        var arr1 = body1.GetProperty("messages");
        arr1.GetArrayLength().Should().Be(3);
        arr1.EnumerateArray().Select(m => m.GetProperty("id").GetInt64())
            .Should().ContainInOrder(ids[0], ids[1], ids[2]);
        body1.GetProperty("hasMore").GetBoolean().Should().BeFalse();

        // beforeId = 第 5 条 + limit=2：最新窗口 = 第 3、4 条（升序），还有更早 → hasMore=true
        var (_, body2) = Decode(_controller.Messages(key, 2, ids[4]));
        var arr2 = body2.GetProperty("messages");
        arr2.GetArrayLength().Should().Be(2);
        arr2.EnumerateArray().Select(m => m.GetProperty("id").GetInt64())
            .Should().ContainInOrder(ids[2], ids[3]);
        body2.GetProperty("hasMore").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Messages_limit钳制_下界1上界200()
    {
        Purge();
        var key = $"wecom:conv-clamp:user-x-{Guid.NewGuid():N}";
        for (var i = 1; i <= 205; i++)
        {
            SeedMsg(key, "wecom", ImGatewayMessage.DirectionIn, $"m{i}", ImGatewayMessage.StatusOk, DateTime.Now.AddSeconds(-i));
        }

        // 下边界：limit=0 → 钳到 1
        var (_, bodyLow) = Decode(_controller.Messages(key, 0));
        bodyLow.GetProperty("messages").GetArrayLength().Should().Be(1, "limit<1 钳制为 1");
        bodyLow.GetProperty("hasMore").GetBoolean().Should().BeTrue();

        // 上边界：limit=500 → 钳到 200，且 205 条时仍有更早 → hasMore=true
        var (_, bodyHigh) = Decode(_controller.Messages(key, 500));
        bodyHigh.GetProperty("messages").GetArrayLength().Should().Be(200, "limit>200 钳制为 200");
        bodyHigh.GetProperty("hasMore").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Messages_beforeId越界或不存在_不报错_hasMorefalse()
    {
        Purge();
        var key = $"wecom:conv-bound:user-b-{Guid.NewGuid():N}";
        for (var i = 1; i <= 3; i++)
        {
            SeedMsg(key, "wecom", ImGatewayMessage.DirectionIn, $"m{i}", ImGatewayMessage.StatusOk, DateTime.Now.AddSeconds(-i));
        }

        // beforeId=1（小于该键最小 Id）：空数组 + hasMore=false，不报错
        var (status1, body1) = Decode(_controller.Messages(key, 50, 1));
        status1.Should().Be(200);
        body1.GetProperty("messages").GetArrayLength().Should().Be(0);
        body1.GetProperty("hasMore").GetBoolean().Should().BeFalse();

        // beforeId 超大：全量返回 + hasMore=false
        var (_, body2) = Decode(_controller.Messages(key, 50, long.MaxValue));
        body2.GetProperty("messages").GetArrayLength().Should().Be(3);
        body2.GetProperty("hasMore").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void Messages_channelKey缺失_400()
    {
        var (statusEmpty, _) = Decode(_controller.Messages(string.Empty));
        statusEmpty.Should().Be(400, "channelKey 缺省必须 400（FR-3）");

        var (statusBlank, _) = Decode(_controller.Messages("   "));
        statusBlank.Should().Be(400, "channelKey 纯空白等价缺失");
    }

    // ---- FR-4 管理面鉴权（反射式，对齐 McpAdminAuthTests 先例）----

    [Fact]
    public void ImGatewayController_类级必须带ApiKeyPolicy鉴权()
    {
        var attr = typeof(ImGatewayController).GetCustomAttribute<AuthorizeAttribute>();
        attr.Should().NotBeNull("宿主无全局鉴权件，IM 网关管理面必须类级显式声明（FR-4 / 铁律 17）");
        attr!.Policy.Should().Be("ApiKeyPolicy", "裸 [Authorize] 不算：宿主默认策略不是 ApiKeyPolicy");

        var anonymous = typeof(ImGatewayController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null)
            .ToList();
        anonymous.Should().BeEmpty("本控制器不得存在匿名端点（管理面全端点需鉴权；活体 401 由 e2e AC-7 覆盖）");
    }
}
