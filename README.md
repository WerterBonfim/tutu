# Tutu

Interface de monitoramento do Pub/Sub para testes de integração locais. O objetivo é exercitar filas na máquina, sem publicar no GCP.

O Tutu mostra o que entra na fila e como ela se comporta: tópicos, subscriptions, mensagens, confirmações, rejeições e quarentena. A observação usa subscriptions `tutu-*`, para não competir com a aplicação que já consome o mesmo emulador.

O runtime é o [emulador oficial do Pub/Sub](https://cloud.google.com/pubsub/docs/emulator). Esta interface não substitui esse emulador; ela sobe junto com ele e expõe o que está acontecendo.

## Fluxo

1. Suba o Tutu.
2. Rode o projeto, microserviço ou monólito sob teste.
3. Aponte as variáveis de ambiente do Pub/Sub para o emulador local, em vez do GCP. O projeto local é `pedidos-local` e o emulador escuta em `localhost:8085`.
4. Publique e consuma como no teste de integração, ou pare no depurador. Acompanhe a fila em http://localhost:7654.

## Como executar

```bash
podman compose up --build
```

- Interface: http://localhost:7654
- API no host: http://localhost:5080 (no container a API escuta `8080`)
- Emulador: `localhost:8085`

```bash
dotnet test backend/Tutu.Api.Tests
cd frontend && npm run dev    # http://127.0.0.1:5173, proxy /api e /metrics para :5080
cd frontend && npm run build
```

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

## Roadmap

- Suporte a gRPC na interface, além do acesso HTTP atual.
- Validação de esquemas das mensagens publicadas e consumidas.
- Carga de teste com replay de mensagens.
- Cadastro de testes: ao voltar, os testes salvos continuam disponíveis e podem ser reexecutados.

## Referências

- [Emulador do Pub/Sub](https://cloud.google.com/pubsub/docs/emulator)
