# Tutu

Console local para o emulador Pub/Sub. Outra aplicação usa o mesmo emulador. Este processo só puxa, confirma e rejeita mensagens em subscriptions `tutu-*`.

## Stack

- API: .NET 10, `Google.Cloud.PubSub.V1`, sem banco.
- UI: Svelte 5, Vite, CSS Modules. Sem Tailwind.
- Runtime: emulador gcloud, projeto `pedidos-local`, porta `8085`.
- Imagens Alpine. Compose sobe emulador, API e nginx.

## Como rodar e testar

```bash
podman compose up --build
```

- UI: http://localhost:8080
- API no host: http://localhost:5080 (no container a API escuta `8080`)
- Emulador: `localhost:8085`

```bash
dotnet test backend/Tutu.Api.Tests
cd frontend && npm run dev    # http://127.0.0.1:5173, proxy /api e /metrics para :5080
cd frontend && npm run build
```

## Estrutura

- `backend/Program.cs` — rotas HTTP e SSE `/events`
- `backend/PubSubConsole.cs` — cliente do emulador e a regra `tutu-*`
- `backend/ConsoleActivity.cs` — contadores, quarentena e logs em memória
- `frontend/src/pages/` — uma tela por arquivo, CSS ao lado (`*.module.css`)
- `frontend/src/ui.module.css` e `app.css` — peças compartilhadas e tokens
- `docker-compose.yml` — `pubsub`, `api`, `web`

## Convenções

- UI em português. Nomes de código em inglês.
- CSS Modules: classe em kebab-case no arquivo, `styles.camelCase` no Svelte.
- Hover anima só `background-color` e `color`. Movimento só com `transform` e `opacity`.
- Números da tela vêm deste processo ou do emulador. Não copiar valores de protótipo.
- DLQ: no quinto nack de uma mensagem puxada aqui, copia para `tutu-dlq-<tópico>` e confirma a original. Não vê o nack da outra aplicação.
- Contadores, quarentena e logs morrem com o processo.

## Evitar

- Banco, autenticação e chamada ao GCP real.
- Pull, ack ou nack fora de `tutu-*`.
- Restart do container do emulador pela UI.
- Commitar `node_modules`, `dist`, `bin` ou `obj`.
- Dependência nova para poucas linhas. Sem MediatR.
