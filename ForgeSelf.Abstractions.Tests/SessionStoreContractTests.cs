using ForgeSelf.Abstractions;
using Xunit;

namespace ForgeSelf.Abstractions.Tests;

public class SessionStoreContractTests
{
    private sealed class FakeSessionStore : ISessionStore
    {
        private readonly List<SessionEvent> _events = new();
        private long _nextId = 1;

        public long Append(string sessionId, SessionEvent evt)
        {
            var recorded = new SessionEvent
            {
                Id = _nextId++,
                SessionId = sessionId,
                Type = evt.Type,
                Payload = evt.Payload,
                Timestamp = evt.Timestamp,
            };
            _events.Add(recorded);
            return recorded.Id;
        }

        public IReadOnlyList<SessionEvent> Replay(string sessionId)
            => _events.Where(e => e.SessionId == sessionId).ToList();

        public IReadOnlyList<Message> DeriveMessages(string sessionId)
            => _events
                .Where(e => e.SessionId == sessionId)
                .Select(e => new Message { Role = e.Type, Content = e.Payload })
                .ToList();
    }

    [Fact]
    public void Append_ThenReplay_ReturnsRecordedEvent()
    {
        var store = new FakeSessionStore();

        var id = store.Append("s1", new SessionEvent { Type = "user", Payload = "hi" });

        var events = store.Replay("s1");

        Assert.Single(events);
        Assert.Equal(id, events[0].Id);
        Assert.Equal("s1", events[0].SessionId);
        Assert.Equal("user", events[0].Type);
        Assert.Equal("hi", events[0].Payload);
    }

    [Fact]
    public void Replay_OnlyReturnsEventsForGivenSession()
    {
        var store = new FakeSessionStore();
        store.Append("s1", new SessionEvent { Type = "user", Payload = "a" });
        store.Append("s2", new SessionEvent { Type = "user", Payload = "b" });

        Assert.Single(store.Replay("s1"));
        Assert.Single(store.Replay("s2"));
    }

    [Fact]
    public void DeriveMessages_ProjectsEventsIntoModelVisibleHistory()
    {
        var store = new FakeSessionStore();
        store.Append("s1", new SessionEvent { Type = "user", Payload = "hi" });
        store.Append("s1", new SessionEvent { Type = "assistant", Payload = "hello" });

        var messages = store.DeriveMessages("s1");

        Assert.Equal(2, messages.Count);
        Assert.Equal("user", messages[0].Role);
        Assert.Equal("hi", messages[0].Content);
        Assert.Equal("assistant", messages[1].Role);
        Assert.Equal("hello", messages[1].Content);
    }
}
