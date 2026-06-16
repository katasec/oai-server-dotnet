namespace Katasec.OaiServer;

public interface ISessionStore
{
    Task<Session?> GetAsync(string sessionId, CancellationToken ct = default);
    Task SaveAsync(Session session, CancellationToken ct = default);
    Task DeleteAsync(string sessionId, CancellationToken ct = default);
}
