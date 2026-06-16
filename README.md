# Katasec.OaiServer

AOT-safe .NET library that exposes any `IChatClient` as an OpenAI-compatible `/v1/chat/completions` endpoint.

Used by [forge](https://github.com/katasec/mission-control-language) to serve MCL missions as agents via `forge serve`.

## Usage

```csharp
// Wire up your IChatClient, then:
var app = OaiServer.Build(chatClient, agentId: "my-agent-v1", port: 8080);
await app.RunAsync();
```

Sessions are persisted to `~/.forge/sessions/` by default. Pass a custom `ISessionStore` to swap the provider.

## Session handling

Clients pass `X-Session-Id` in the request header to continue a conversation. If omitted, a new session is created and the ID is returned in the response header.

## Publishing

Releases are cut via GitHub Actions (`workflow_dispatch`) — enter the version in the Actions UI.
