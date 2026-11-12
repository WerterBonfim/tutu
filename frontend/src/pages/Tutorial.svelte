<script>
  import { onDestroy } from 'svelte';
  import ui from '../ui.module.css';
  import styles from './Tutorial.module.css';

  let { ontry } = $props();

  const topicCmd = 'gcloud --endpoint=http://localhost:8085 pubsub topics create orders.events --project=local-project-dev';
  const subCmd = 'gcloud --endpoint=http://localhost:8085 pubsub subscriptions create sub-orders-worker --topic=orders.events --ack-deadline=60 --project=local-project-dev';
  const dlqCmd = 'gcloud --endpoint=http://localhost:8085 pubsub subscriptions update sub-orders-worker --dead-letter-topic=orders.dlq --max-delivery-attempts=5';
  const pythonCmd = `def callback(message):
    try:
        process_payload(message.data)
        message.ack()  # Confirmação bem-sucedida
    except TransientException:
        message.nack() # Solicita reentrega com backoff
    except Exception as fatal:
        log_error(fatal)
        # NACK repetido encaminhará à DLQ após max_attempts
        message.nack()`;
  const envCmd = `export PUBSUB_EMULATOR_HOST=localhost:8085
export PUBSUB_PROJECT_ID=local-project-dev`;
  const allCmd = `# 1. Configurar variáveis de ambiente do emulador
export PUBSUB_EMULATOR_HOST=localhost:8085
export PUBSUB_PROJECT_ID=local-project-dev

# 2. Criar tópicos
gcloud --endpoint=http://localhost:8085 pubsub topics create orders.events --project=local-project-dev
gcloud --endpoint=http://localhost:8085 pubsub topics create orders.dlq --project=local-project-dev

# 3. Criar subscription com política de Dead Letter Queue
gcloud --endpoint=http://localhost:8085 pubsub subscriptions create sub-orders-worker --topic=orders.events --ack-deadline=60 --dead-letter-topic=orders.dlq --max-delivery-attempts=5 --project=local-project-dev`;

  let toast = $state('');
  let toastOk = $state(true);
  let sim = $state('idle');
  const timers = [];

  function later(fn, ms) {
    const id = setTimeout(fn, ms);
    timers.push(id);
  }

  onDestroy(() => timers.forEach(clearTimeout));

  function showToast(message, ok = true) {
    toast = message;
    toastOk = ok;
    later(() => {
      if (toast === message) toast = '';
    }, 3200);
  }

  async function copy(text, message = 'Comando copiado com sucesso!') {
    try {
      await navigator.clipboard.writeText(text);
      showToast(message);
    } catch {
      showToast('Não foi possível copiar.', false);
    }
  }

  function simulate() {
    if (sim !== 'idle') return;
    sim = 'run';
    later(() => {
      sim = 'done';
      showToast('Simulação concluída: 1 mensagem movida para orders.dlq após 5 NACKs!');
      later(() => (sim = 'idle'), 3000);
    }, 1500);
  }
</script>

<main class={ui.page}>
  <div class={styles.page}>
    <header class={styles.header}>
      <div class={styles.intro}>
        <div class={styles.badges}>
          <span class={styles.badgePrimary}><span class="material-symbols-outlined {ui.icon14}">school</span> Documentação Interativa</span>
          <span class={styles.badgeMuted}><span class="{ui.dot}"></span> Emulador Local v475.0.0</span>
          <span class={styles.badgeMuted}><span class="material-symbols-outlined {ui.icon14} {ui.tonePrimary}">lan</span> Porta: 8085</span>
          <span class="{styles.badgeMuted} {ui.faint}">SDK: google-cloud-pubsub</span>
        </div>
        <h1 class={ui.headlineXl}>Tutorial & Arquitetura do GCP Pub/Sub</h1>
        <p class={ui.bodyMd}>Guia interativo e documentação visual para desenvolvedores: conceitos fundamentais, ciclo de vida de mensagens, padrões de mensageria assíncrona e testes de resiliência no emulador local.</p>
      </div>
      <div class={styles.actions}>
        <button class={ui.secondaryButton} type="button" onclick={() => copy(allCmd, 'Todos os snippets foram copiados para a área de transferência!')}>
          <span class="material-symbols-outlined {ui.icon16} {ui.muted}">content_copy</span>
          Copiar Snippets de Exemplo
        </button>
        <button class={ui.primaryButton} type="button" onclick={ontry}>
          Testar no Message Browser
          <span class="material-symbols-outlined {ui.icon16}">arrow_forward</span>
        </button>
      </div>
    </header>

    <section class={styles.section}>
      <div class={styles.sectionHead}>
        <div class={styles.sectionTitle}>
          <span class={styles.index}>01</span>
          <h2 class={ui.headlineLg}>Visão Geral do Modelo Publish-Subscribe</h2>
        </div>
        <span class={styles.kicker}>Topologia Ponta a Ponta</span>
      </div>
      <div class={styles.panel}>
        <div class={styles.banner}>
          <div class={styles.bannerCopy}>
            <div class={styles.iconBox}><span class="material-symbols-outlined">alt_route</span></div>
            <div>
              <span class="{ui.headlineMd} {ui.semibold}">Desacoplamento Espaço-Temporal Total</span>
              <p class={ui.bodySm}>Produtores (Publishers) desconhecem quem consome os dados. Subscriptions realizam o roteamento e retenção das mensagens de forma autônoma.</p>
            </div>
          </div>
          <div class={styles.actions}>
            <span class="{styles.chip} {ui.toneSecondary}"><span class={ui.dot}></span> Fan-out Ativo</span>
            <span class="{styles.chip} {ui.tonePrimary}">At-least-once Delivery</span>
          </div>
        </div>
        <div class={styles.diagram}>
          <div class={styles.flow}>
            <div class={styles.node}>
              <div class={styles.card}>
                <div class="{styles.iconRound} {styles.tonePrimary}"><span class="material-symbols-outlined">web</span></div>
                <span class={ui.headlineMd}>Publishers</span>
                <span class="{ui.labelSm} {ui.tonePrimary}">Microserviços & API</span>
                <span class={ui.bodySm}>Order Service, Checkout App</span>
              </div>
              <div class={styles.endpoint}>POST /v1/projects/.../topics</div>
            </div>
            <div class={styles.arrow}>
              <span class="{ui.labelSm} {ui.tonePrimary}">publish(msg)</span>
              <div class={styles.line}><span class="material-symbols-outlined">play_arrow</span></div>
              <span class="{ui.labelSm} {ui.faint}">Payload + Attributes</span>
            </div>
            <div class="{styles.node} {styles.nodeWide}">
              <div class={styles.card}>
                <div class="{styles.iconRound} {styles.toneSecondary}"><span class="material-symbols-outlined">schema</span></div>
                <span class={ui.headlineMd}>Tópico Pub/Sub</span>
                <span class="{ui.labelMd} {ui.toneSecondary} {ui.semibold}">orders.events</span>
                <div class={styles.tags}><span class={styles.mini}>Schema: JSON</span><span class={styles.mini}>Stateless</span></div>
              </div>
              <span class="{ui.labelSm} {ui.toneSecondary}">Fan-out 1 → N Automático</span>
            </div>
            <div class={styles.fork}>
              <div class="{styles.forkRow} {styles.forkSecondary}"><span class={styles.forkLine}></span><span class="material-symbols-outlined">play_arrow</span></div>
              <div class="{styles.forkRow} {styles.forkPrimary}"><span class={styles.forkLine}></span><span class="material-symbols-outlined">play_arrow</span></div>
              <div class="{styles.forkRow} {styles.forkTertiary}"><span class={styles.forkLine}></span><span class="material-symbols-outlined">play_arrow</span></div>
            </div>
            <div class={styles.subs}>
              <div class={styles.subRow}>
                <div class={styles.subCopy}><span class="material-symbols-outlined {ui.icon18} {ui.toneSecondary}">downloading</span><div><span class="{ui.labelMd} {ui.ink}">sub-orders-worker</span><span class="{ui.labelSm} {ui.toneSecondary} {ui.block}">PULL • StreamingPull</span></div></div>
                <span class="material-symbols-outlined {ui.icon16} {ui.faint}">arrow_right_alt</span>
              </div>
              <div class={styles.subRow}>
                <div class={styles.subCopy}><span class="material-symbols-outlined {ui.icon18} {ui.tonePrimary}">call_made</span><div><span class="{ui.labelMd} {ui.ink}">sub-webhook-gateway</span><span class="{ui.labelSm} {ui.tonePrimary} {ui.block}">PUSH • HTTP Webhook</span></div></div>
                <span class="material-symbols-outlined {ui.icon16} {ui.faint}">arrow_right_alt</span>
              </div>
              <div class={styles.subRow}>
                <div class={styles.subCopy}><span class="material-symbols-outlined {ui.icon18} {ui.toneTertiary}">filter_alt</span><div><span class="{ui.labelMd} {ui.ink}">sub-audit-brazil</span><span class="{ui.labelSm} {ui.toneTertiary} {ui.block}">Filter: region = 'BR'</span></div></div>
                <span class="material-symbols-outlined {ui.icon16} {ui.faint}">arrow_right_alt</span>
              </div>
            </div>
            <div class={styles.targets}>
              <div class={styles.target}><span class="{ui.bodySm} {ui.ink} {ui.semibold}">Payment Engine</span><span class="{ui.labelSm} {ui.muted}">Worker Autoscaling (k8s)</span></div>
              <div class={styles.target}><span class="{ui.bodySm} {ui.ink} {ui.semibold}">Cloud Run Endpoint</span><span class="{ui.labelSm} {ui.muted}">Serverless HTTP 200 OK</span></div>
              <div class={styles.target}><span class="{ui.bodySm} {ui.ink} {ui.semibold}">BigQuery Streamer</span><span class="{ui.labelSm} {ui.muted}">Auditoria & Data Lake</span></div>
            </div>
          </div>
        </div>
        <div class={styles.concepts}>
          <div class={styles.concept}><span class="{styles.conceptTitle} {ui.labelSm} {ui.tonePrimary} {ui.semibold}">Desacoplamento no Tempo</span><p class={ui.bodySm}>O Publisher envia o evento mesmo se todos os consumidores estiverem offline. A subscription retém os bytes até a entrega.</p></div>
          <div class={styles.concept}><span class="{styles.conceptTitle} {ui.labelSm} {ui.toneSecondary} {ui.semibold}">Garantia At-Least-Once</span><p class={ui.bodySm}>Toda mensagem é entregue ao menos uma vez para cada assinatura. Operações do consumidor devem ser idempotentes.</p></div>
          <div class={styles.concept}><span class="{styles.conceptTitle} {ui.labelSm} {ui.toneTertiary} {ui.semibold}">Isolamento Multi-Tenant</span><p class={ui.bodySm}>Novas subscriptions podem ser adicionadas sem nenhuma alteração de código ou impacto de throughput no Publisher.</p></div>
        </div>
      </div>
    </section>

    <section class={styles.section}>
      <div class={styles.sectionHead}>
        <div class={styles.sectionTitle}><span class={styles.index}>02</span><h2 class={ui.headlineLg}>Os Quatro Pilares Conceituais</h2></div>
        <span class={styles.kicker}>Mapeamento Técnico Profundo</span>
      </div>
      <div class={styles.pillars}>
        <article class={styles.pillar}>
          <div class={styles.pillarBody}>
            <div class={styles.pillarTop}>
              <div class={styles.row}><div class="{styles.iconSm} {styles.toneSecondary}"><span class="material-symbols-outlined">podcasts</span></div><div><span class="{ui.labelSm} {ui.toneSecondary} {ui.semibold}">Pilar 01</span><h3 class={ui.headlineMd}>Tópicos (Topics)</h3></div></div>
              <span class={styles.chip}>Inbound Conduit</span>
            </div>
            <p class={ui.bodyMd}>O tópico é o recurso de entrada no barramento para o qual os publicadores enviam mensagens. Por si só, um tópico <strong class={ui.ink}>não retém mensagens de forma permanente</strong> — a retenção ocorre nas assinaturas (subscriptions) atreladas a ele.</p>
            <div>
              <span class="{ui.labelSm} {ui.faint}">Características Chave</span>
              <ul class={styles.list}>
                <li><span class="material-symbols-outlined {ui.icon16} {ui.toneSecondary}">check_circle</span><span><strong>Suporte a Schemas:</strong> Validação em tempo de publicação via JSON Schema, Apache Avro ou Protocol Buffers.</span></li>
                <li><span class="material-symbols-outlined {ui.icon16} {ui.toneSecondary}">check_circle</span><span><strong>Particionamento Transparente:</strong> Sem gerenciamento de partições fixas; escalabilidade elástica gerenciada pelo broker.</span></li>
                <li><span class="material-symbols-outlined {ui.icon16} {ui.toneSecondary}">check_circle</span><span><strong>Ordering Keys Opcionais:</strong> Possibilidade de agrupar mensagens com uma chave para garantir entrega sequencial estrita.</span></li>
              </ul>
            </div>
          </div>
          <div class={styles.snippet}>
            <div class="{styles.snippetHead} {ui.labelSm}"><span>gcloud CLI • Criar Tópico no Emulador</span><button class={styles.copy} type="button" onclick={() => copy(topicCmd)}><span class="material-symbols-outlined {ui.icon14}">content_copy</span> Copiar</button></div>
            <pre>gcloud --endpoint=http://localhost:8085 pubsub topics create orders.events \
  --project=local-project-dev</pre>
          </div>
        </article>
        <article class={styles.pillar}>
          <div class={styles.pillarBody}>
            <div class={styles.pillarTop}>
              <div class={styles.row}><div class="{styles.iconSm} {styles.tonePrimary}"><span class="material-symbols-outlined">move_to_inbox</span></div><div><span class="{ui.labelSm} {ui.tonePrimary} {ui.semibold}">Pilar 02</span><h3 class={ui.headlineMd}>Assinaturas (Subscriptions)</h3></div></div>
              <span class={styles.chip}>1-para-Muitos (Fan-out)</span>
            </div>
            <p class={ui.bodyMd}>A subscription representa o fluxo de mensagens de um tópico específico entregue a um conjunto de consumidores. Cada mensagem publicada no tópico é clonada para todas as suas subscriptions anexas.</p>
            <div class={styles.modes}>
              <div class={styles.mode}><div class="{styles.modeTitle} {ui.labelMd} {ui.toneSecondary}"><span class="material-symbols-outlined {ui.icon16}">sync_alt</span> Modo PULL</div><p class={ui.bodySm}>O consumidor faz requisições bidirecionais contínuas (StreamingPull gRPC) puxando lotes conforme sua capacidade de processamento.</p></div>
              <div class={styles.mode}><div class="{styles.modeTitle} {ui.labelMd} {ui.tonePrimary}"><span class="material-symbols-outlined {ui.icon16}">forward_to_inbox</span> Modo PUSH</div><p class={ui.bodySm}>O Pub/Sub dispara requisições HTTP POST contra um endpoint webhook. Requer que o consumer responda status 200/204 para validar o ACK.</p></div>
            </div>
            <p class="{styles.note} {ui.bodySm} {ui.ink}"><span class="material-symbols-outlined {ui.icon16} {ui.tonePrimary}">filter_list</span><span><strong>Filtros de Atributos:</strong> Descarte mensagens antes de pagar custo de tráfego (ex: attributes.eventType = "ORDER_PAID").</span></p>
          </div>
          <div class={styles.snippet}>
            <div class="{styles.snippetHead} {ui.labelSm}"><span>gcloud CLI • Criar Subscription com Ack Deadline</span><button class={styles.copy} type="button" onclick={() => copy(subCmd)}><span class="material-symbols-outlined {ui.icon14}">content_copy</span> Copiar</button></div>
            <pre>gcloud --endpoint=http://localhost:8085 pubsub subscriptions create \
  sub-orders-worker --topic=orders.events --ack-deadline=60</pre>
          </div>
        </article>
        <article class={styles.pillar}>
          <div class={styles.pillarBody}>
            <div class={styles.pillarTop}>
              <div class={styles.row}><div class="{styles.iconSm} {styles.toneNeutral}"><span class="material-symbols-outlined">verified</span></div><div><span class="{ui.labelSm} {ui.muted} {ui.semibold}">Pilar 03</span><h3 class={ui.headlineMd}>Ciclo de Vida: ACK vs NACK</h3></div></div>
              <span class={styles.chip}>Ack Deadline</span>
            </div>
            <p class={ui.bodyMd}>Quando o consumidor recebe uma mensagem, o relógio do <strong class={ui.ink}>Ack Deadline (ex: 10s - 600s)</strong> começa a correr. O consumidor deve reportar o status para liberar a fila.</p>
            <div class={styles.states}>
              <div class={styles.state}><div class={styles.stateName}><span class="{styles.dot} {ui.dot}"></span><span class="{ui.labelMd} {ui.toneSecondary} {ui.semibold}">ACK (Acknowledge)</span></div><span class={ui.bodySm}>Processada com sucesso → Pub/Sub expurga mensagem da sub</span></div>
              <div class={styles.state}><div class={styles.stateName}><span class="{styles.dot} {styles.bad}"></span><span class="{ui.labelMd} {styles.bad} {ui.semibold}">NACK (Negative ACK)</span></div><span class={ui.bodySm}>Consumidor avisa falha transitória → Reentrega imediata/backoff</span></div>
              <div class={styles.state}><div class={styles.stateName}><span class="{styles.dot} {ui.dotTertiary}"></span><span class="{ui.labelMd} {ui.toneTertiary} {ui.semibold}">Timeout Expirado</span></div><span class={ui.bodySm}>Sem resposta no prazo → Mensagem retorna ao backlog</span></div>
            </div>
            <p class="{styles.note} {ui.bodySm} {ui.muted}"><span class="material-symbols-outlined {ui.icon16} {ui.tonePrimary}">info</span><span>O client SDK estende automaticamente o prazo (ack_deadline_extension) enquanto a thread estiver ativa.</span></p>
          </div>
          <div class={styles.snippet}>
            <div class="{styles.snippetHead} {ui.labelSm}"><span>Python SDK • Handler ACK / NACK</span><button class={styles.copy} type="button" onclick={() => copy(pythonCmd)}><span class="material-symbols-outlined {ui.icon14}">content_copy</span> Copiar</button></div>
            <pre class={styles.ink}>def callback(message):
    try:
        process_order(message.data, message.attributes)
        message.ack()   <span class={ui.toneSecondary}># ACK: Removida da fila</span>
    except Exception as err:
        message.nack()  <span class={styles.bad}># NACK: Reentrega pelo broker</span></pre>
          </div>
        </article>
        <article class={styles.pillar}>
          <div class={styles.pillarBody}>
            <div class={styles.pillarTop}>
              <div class={styles.row}><div class="{styles.iconSm} {styles.toneError}"><span class="material-symbols-outlined">report_problem</span></div><div><span class="{ui.labelSm} {styles.bad} {ui.semibold}">Pilar 04</span><h3 class={ui.headlineMd}>Dead Letter Queues (DLQ)</h3></div></div>
              <span class={styles.chip}>Poison Pill Defense</span>
            </div>
            <p class={ui.bodyMd}>Mecanismo crítico de resiliência que intercepta "mensagens venenosas" (payloads malformados ou que quebram o consumidor) e evita que causem crashes em looping (CrashLoopBackOff).</p>
            <div class={styles.mode}>
              <div class={styles.pillarTop}><span class="{ui.labelSm} {ui.ink} {ui.semibold}">Parâmetro de Disparo: max_delivery_attempts</span><span class="{styles.impact} {styles.bad}">5x Tentativas</span></div>
              <div class={styles.meter}><span class={styles.meterOk}></span><span class={styles.meterOk}></span><span class={styles.meterOk}></span><span class={styles.meterWarn}></span><span class="{styles.meterBad} {ui.pulse}"></span></div>
              <span class="{ui.labelSm} {ui.muted}">Após a 5ª falha de processamento (sem receber ACK), o Pub/Sub transfere a mensagem para o tópico DLQ sem descartá-la.</span>
            </div>
            <p class="{styles.note} {ui.bodySm} {ui.ink}"><span class="material-symbols-outlined {ui.icon16} {ui.toneSecondary}">replay</span><span>Permite inspeção forense, ajuste de bugs no consumidor e posterior <strong>Replay idempotente</strong> sem perda de dados.</span></p>
          </div>
          <div class={styles.snippet}>
            <div class="{styles.snippetHead} {ui.labelSm}"><span>gcloud CLI • Configurar DLQ em Subscription</span><button class={styles.copy} type="button" onclick={() => copy(dlqCmd)}><span class="material-symbols-outlined {ui.icon14}">content_copy</span> Copiar</button></div>
            <pre>gcloud --endpoint=http://localhost:8085 pubsub subscriptions update \
  sub-orders-worker --dead-letter-topic=orders.dlq \
  --max-delivery-attempts=5</pre>
          </div>
        </article>
      </div>
    </section>

    <section class={styles.section}>
      <div class={styles.sectionHead}>
        <div class={styles.sectionTitle}><span class={styles.index}>03</span><h2 class={ui.headlineLg}>Tabela Comparativa Rápida (Cheat Sheet)</h2></div>
        <span class={styles.kicker}>Matriz de Responsabilidade</span>
      </div>
      <div class={styles.tableWrap}>
        <div class={ui.scroll}>
          <table class={styles.table}>
            <thead>
              <tr>
                <th>Entidade</th><th>Responsabilidade Principal</th><th>Onde Vive no Emulador</th><th>Comando CLI Típico</th><th>Impacto em Falha</th>
              </tr>
            </thead>
            <tbody class={ui.bodySm}>
              <tr>
                <td><span class="{styles.entity} {ui.toneSecondary}"><span class="material-symbols-outlined {ui.icon16}">podcasts</span> Topic</span></td>
                <td class={ui.muted}>Ponto de ingresso para mensagens; broadcasting fan-out</td>
                <td class="{ui.labelSm} {ui.faint}">Memória Volátil / Engine State</td>
                <td class="{ui.labelSm} {ui.tonePrimary}">topics create &lt;name&gt;</td>
                <td><span class={styles.impact}>Publisher recebe 404/Unavailable</span></td>
              </tr>
              <tr>
                <td><span class="{styles.entity} {ui.tonePrimary}"><span class="material-symbols-outlined {ui.icon16}">move_to_inbox</span> Subscription</span></td>
                <td class={ui.muted}>Armazena backlog e gerencia ack states por consumidor</td>
                <td class="{ui.labelSm} {ui.faint}">Message Store / Backlog Buffer</td>
                <td class="{ui.labelSm} {ui.tonePrimary}">subscriptions create &lt;name&gt;</td>
                <td><span class="{styles.impact} {ui.toneTertiary}">Acúmulo de Backlog</span></td>
              </tr>
              <tr>
                <td><span class="{styles.entity} {ui.ink}"><span class="material-symbols-outlined {ui.icon16}">mail</span> Message (Payload)</span></td>
                <td class={ui.muted}>Dados (bytes) + Metadados (Atributos Key/Value) + Msg ID</td>
                <td class="{ui.labelSm} {ui.faint}">Subscription Ring Buffer</td>
                <td class="{ui.labelSm} {ui.tonePrimary}">topics publish &lt;topic&gt; --message=...</td>
                <td><span class="{styles.impact} {styles.bad}">Re-entrega até ACK ou DLQ</span></td>
              </tr>
              <tr>
                <td><span class="{styles.entity} {styles.bad}"><span class="material-symbols-outlined {ui.icon16}">report_problem</span> Dead Letter Queue</span></td>
                <td class={ui.muted}>Tópico especial que abriga mensagens venenosas após N falhas</td>
                <td class="{ui.labelSm} {ui.faint}">Tópico de Quarentena Isolado</td>
                <td class="{ui.labelSm} {ui.tonePrimary}">subscriptions update --dead-letter-topic=...</td>
                <td><span class="{styles.impact} {ui.toneSecondary}">Sem impacto no tráfego live</span></td>
              </tr>
              <tr>
                <td><span class="{styles.entity} {ui.toneSecondary}"><span class="material-symbols-outlined {ui.icon16}">sort</span> Ordering Keys</span></td>
                <td class={ui.muted}>Garante entrega ordenada (FIFO) para a mesma partition key</td>
                <td class="{ui.labelSm} {ui.faint}">Sequential Dispatcher Virtual</td>
                <td class="{ui.labelSm} {ui.tonePrimary}">publish ... --ordering-key="user-123"</td>
                <td><span class="{styles.impact} {ui.toneTertiary}">Trava apenas a chave afetada</span></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class={styles.section} id="test-guide">
      <div class={styles.sectionHead}>
        <div class={styles.sectionTitle}><span class={styles.index}>04</span><h2 class={ui.headlineLg}>Guia Prático para o Desenvolvedor no Emulador Local</h2></div>
        <span class={styles.kicker}>Playbook de Validação</span>
      </div>
      <div class={styles.playbook}>
        <article class={styles.pillar}>
          <div class={styles.pillarBody}>
            <div class={styles.row}><div class="{styles.iconSm} {styles.tonePrimary}"><span class="material-symbols-outlined">terminal</span></div><div><span class="{ui.labelSm} {ui.tonePrimary} {ui.semibold}">Passo 0 • Setup</span><h3 class={ui.headlineMd}>Variáveis de Ambiente</h3></div></div>
            <p class={ui.bodySm}>Ao configurar a variável PUBSUB_EMULATOR_HOST, os SDKs oficiais (Python, Java, Go, Node.js) desviam automaticamente o tráfego da nuvem do Google para a porta do seu localhost sem necessidade de credenciais de Service Account.</p>
          </div>
          <div class={styles.snippet}>
            <div class="{styles.snippetHead} {ui.labelSm}"><span>Terminal Shell</span><button class={styles.copy} type="button" onclick={() => copy(envCmd)}><span class="material-symbols-outlined {ui.icon14}">content_copy</span> Copiar</button></div>
            <pre>{envCmd}</pre>
          </div>
        </article>
        <article class={styles.pillar}>
          <div class={styles.pillarBody}>
            <div class={styles.row}><div class="{styles.iconSm} {styles.toneTertiary}"><span class="material-symbols-outlined">difference</span></div><div><span class="{ui.labelSm} {ui.toneTertiary} {ui.semibold}">Atenção</span><h3 class={ui.headlineMd}>Emulador vs GCP Nuvem</h3></div></div>
            <ul class={styles.list}>
              <li><span class="material-symbols-outlined {ui.icon16} {ui.toneTertiary}">warning</span><span><strong>Persistência em Memória:</strong> Por padrão, reiniciar o processo do emulador limpa todos os tópicos e mensagens, a menos que iniciado com --data-dir=/tmp/pubsub.</span></li>
              <li><span class="material-symbols-outlined {ui.icon16} {ui.toneSecondary}">bolt</span><span><strong>Latência Quase Nula:</strong> No localhost não há latência de rede regional (Rounds ~0.4ms). Utilize o simulador de atraso do console se for testar concorrência.</span></li>
              <li><span class="material-symbols-outlined {ui.icon16} {ui.tonePrimary}">lock_open</span><span><strong>Sem IAM Real:</strong> Permissões IAM não são avaliadas pelo emulador local; qualquer token ou header de projeto é aceito.</span></li>
            </ul>
          </div>
          <div class={styles.status}><span class="{ui.labelSm} {ui.muted}">Status da Persistência</span><span class="{ui.labelSm} {ui.toneSecondary} {ui.semibold}">VOLÁTIL (/tmp/emul)</span></div>
        </article>
        <article class={styles.pillar}>
          <div class={styles.steps}>
            <div class={styles.row}><div class="{styles.iconSm} {styles.toneSecondary}"><span class="material-symbols-outlined">checklist</span></div><div><span class="{ui.labelSm} {ui.toneSecondary} {ui.semibold}">Hands-on</span><h3 class={ui.headlineMd}>Roteiro Guiado de Teste</h3></div></div>
            <div class={styles.step}><span class={styles.num}>1</span><span>Criar o tópico orders.events e a DLQ orders.dlq</span></div>
            <div class={styles.step}><span class={styles.num}>2</span><span>Vincular assinatura com --max-delivery-attempts=3</span></div>
            <div class={styles.step}><span class={styles.num}>3</span><span>Publicar mensagem inválida e rejeitar com NACK 3 vezes</span></div>
            <div class={styles.step}><span class={styles.num}>4</span><span>Verificar mensagem transferida intacta para a DLQ</span></div>
          </div>
          <button class={styles.run} type="button" disabled={sim !== 'idle'} onclick={simulate}>
            {#if sim === 'run'}
              <span class="material-symbols-outlined {ui.icon16} {styles.spin}">refresh</span> Simulando 5 falhas e roteamento DLQ...
            {:else if sim === 'done'}
              <span class="material-symbols-outlined {ui.icon16} {ui.toneSecondary}">check</span> Cenário DLQ executado com sucesso!
            {:else}
              <span class="material-symbols-outlined {ui.icon16}">play_circle</span> Executar Simulação de DLQ no Emulador
            {/if}
          </button>
        </article>
      </div>
    </section>
  </div>
</main>

{#if toast}
  <div class={styles.toast} role="status">
    <span class="material-symbols-outlined {toastOk ? ui.toneSecondary : ui.tonePrimary}">{toastOk ? 'check_circle' : 'info'}</span>
    <span class={ui.bodySm}>{toast}</span>
  </div>
{/if}
