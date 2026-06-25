using System.Collections.Concurrent;

namespace Katasec.OaiServer.Tests.Infrastructure;

internal sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, Session> _store = new();

    public Task<Session?> GetAsync(string sessionId, CancellationToken ct)
        => Task.FromResult(_store.TryGetValue(sessionId, out var s) ? s : null);

    public Task SaveAsync(Session session, CancellationToken ct)
    {
        _store[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string sessionId, CancellationToken ct)
    {
        _store.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }
}
