using System.Text.Json;

namespace Katasec.OaiServer;

// Stores sessions as JSON files under ~/.forge/sessions/<session-id>.json
public sealed class LocalFileSessionStore : ISessionStore
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".forge", "sessions");

    public async Task<Session?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        var path = FilePath(sessionId);
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize(json, OaiJsonContext.Default.Session);
    }

    public async Task SaveAsync(Session session, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Root);
        var json = JsonSerializer.Serialize(session, OaiJsonContext.Default.Session);
        await File.WriteAllTextAsync(FilePath(session.Id), json, ct);
    }

    public Task DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        var path = FilePath(sessionId);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private static string FilePath(string sessionId)
        => Path.Combine(Root, $"{sessionId}.json");
}
