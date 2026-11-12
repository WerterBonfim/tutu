# Tutu — console do emulador Pub/Sub

UI local para listar/criar tópicos e subscriptions, publicar e puxar mensagens numa fila própria `tutu-*` (não compete com outras apps no mesmo emulador).

```bash
podman compose up --build
```

Abra http://localhost:8080. Emulador em `localhost:8085`, projeto `pedidos-local`. API direta em `localhost:5080`.

## Backend

Minimal API .NET 10 (`Microsoft.NET.Sdk.Web`). Única dependência: `Google.Cloud.PubSub.V1` 3.39.0 (gRPC no emulador). Sem banco, autenticação, controllers, MediatR ou middleware de exceção. Testes em xUnit.

| Peça | Papel |
|---|---|
| `Program.cs` | Rotas HTTP, JSON camelCase, SSE `/events` |
| `PubSubConsole` | `PublisherServiceApiClient` e `SubscriberServiceApiClient`; regra `tutu-*` |
| `ConsoleActivity` | Contadores, quarentena e logs em memória (`RingLoggerProvider`) |
| `CatalogHub` | `BackgroundService`: poll a cada 2 s e broadcast por `Channel` |

Singletons no container. Estado some quando o processo morre.

### Erros

Não há `UseExceptionHandler`. `Handle` nas rotas que mudam o emulador captura só `ConsoleException` e responde `{ error, message }` com o status dela. O resto sobe.

`PubSubConsole.Wrap` loga e traduz `RpcException`:

| gRPC | HTTP | `error` |
|---|---|---|
| `NotFound` | 404 | nome da operação (`PublishFailed`, …) |
| `AlreadyExists` | 409 | idem |
| `InvalidArgument` | 400 | idem |
| `Unavailable`, `DeadlineExceeded` | 503 | mensagem fixa: emulador inacessível |
| outro | 502 | `Exception.Message` |

Fora do gRPC: id inválido e payload que não é JSON → 400; pull/ack/nack fora de `tutu-*` → 403. Replay de mensagem que não está na quarentena → 404.

`/api/health` não passa por `Handle`: emulador fora vira `online: false`. O poll do `CatalogHub` engole a falha, loga `CatalogPollFailed` e emite o mesmo health offline. Porta ocupada no `Run` vira log `PortInUse` e exit code 1.
