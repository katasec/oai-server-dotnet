namespace Katasec.OaiServer;

public class Session
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public List<OaiMessage> History { get; init; } = [];
}
