<script>
  import { api } from '../api.js';
  import ui from '../ui.module.css';
  import styles from './Subscriptions.module.css';

  let {
    topics,
    subscriptions,
    health,
    filter = '',
    notice = $bindable(''),
    oncreate,
    onpull,
    ondelete,
  } = $props();

  let selectedId = $state('');
  let localFilter = $state('');
  let topicFilter = $state('all');
  let kindFilter = $state('all');
  let pulled = $state(null);
  let busy = $state(false);

  $effect(() => {
    if (!selectedId && subscriptions[0]) selectedId = subscriptions[0].id;
    if (selectedId && subscriptions.length && !subscriptions.some((s) => s.id === selectedId)) {
      selectedId = subscriptions[0]?.id ?? '';
    }
  });

  const query = $derived((filter || localFilter).toLowerCase());
  const filtered = $derived(
    subscriptions.filter((s) => {
      if (kindFilter === 'tutu' && !s.isObservation) return false;
      if (kindFilter === 'other' && s.isObservation) return false;
      if (topicFilter !== 'all' && s.topicId !== topicFilter) return false;
      if (query && !`${s.id} ${s.topicId}`.toLowerCase().includes(query)) return false;
      return true;
    }),
  );
  const selected = $derived(subscriptions.find((s) => s.id === selectedId) ?? filtered[0] ?? null);
  const resource = $derived(
    selected ? `projects/${health.projectId}/subscriptions/${selected.id}` : '',
  );

  async function pullOne() {
    if (!selected?.isObservation) return;
    busy = true;
    notice = '';
    try {
      const result = await api(`/api/subscriptions/${encodeURIComponent(selected.id)}/pull`, {
        method: 'POST',
        body: JSON.stringify({ maxMessages: 1 }),
      });
      pulled = result.messages?.[0] ?? null;
      notice = pulled ? `Pull ${pulled.id}` : 'Fila vazia';
    } catch (err) {
      notice = err.message;
    } finally {
      busy = false;
    }
  }

  async function settle(kind) {
    if (!pulled || !selected) return;
    notice = '';
    try {
      await api(`/api/subscriptions/${encodeURIComponent(selected.id)}/${kind}`, {
        method: 'POST',
        body: JSON.stringify({ ackIds: [pulled.ackId] }),
      });
      notice = kind.toUpperCase();
      pulled = null;
    } catch (err) {
      notice = err.message;
    }
  }

  function copy(text) {
    navigator.clipboard.writeText(text);
  }

  function kindClass(id) {
    return kindFilter === id ? ui.segmentOn : ui.segment;
  }
</script>

<main class={ui.page}>
  <div class={styles.column}>
    <div class={styles.hero}>
      <div>
        <div class={styles.titleLine}>
          <span class={ui.headlineXl}>Subscriptions (Assinaturas)</span>
          <span class="{ui.pill} {ui.pillOk} {ui.labelMd} {ui.semibold}">
            <span class="{ui.dot} {health.online ? ui.ping : ''}"></span>
            {subscriptions.length} ativas
          </span>
          <span class="{ui.pill} {ui.pillMuted}">Emulator Pub/Sub</span>
        </div>
        <p class="{ui.bodySm} {styles.lede}">
          Monitoramento operacional de partições emuladas. Pull de mensagens só nas filas tutu-*.
        </p>
      </div>
      <div class={styles.actions}>
        <button class="{ui.secondaryButton} {ui.secondaryButtonLg}" type="button" disabled title="Ack em massa não está na v1">
          <span class="material-symbols-outlined {ui.icon16} {ui.toneSecondary}">done_all</span>
          <span>Ack em Massa</span>
        </button>
        <button class="{ui.secondaryButton} {ui.secondaryButtonLg}" type="button" disabled title="Seek não está na v1">
          <span class="material-symbols-outlined {ui.icon16} {ui.tonePrimary}">history_toggle_off</span>
          <span>Seek Global</span>
        </button>
        <button class="{ui.primaryButton} {ui.primaryButtonLg}" type="button" onclick={oncreate}>
          <span class="material-symbols-outlined {ui.icon18}">add_circle</span>
          <span>+ Criar Assinatura</span>
        </button>
      </div>
    </div>

    <div class={ui.metrics}>
      <div class="{ui.metricCard} {ui.cardRaised}">
        <div class={styles.metricHead}>
          <span class="{ui.labelSm} {ui.upper}">Backlog Total Unacked</span>
          <span class="material-symbols-outlined {ui.icon18} {ui.toneTertiary}">inventory_2</span>
        </div>
        <div class={styles.metricValue}>
          <span class="{ui.headlineXl} {ui.semibold}">0</span>
          <span class="{ui.labelSm} {ui.muted}">—</span>
        </div>
        <div class={ui.bar}><div class="{ui.barFill} {ui.barTertiary}"></div></div>
      </div>
      <div class="{ui.metricCard} {ui.cardRaised}">
        <div class={styles.metricHead}>
          <span class="{ui.labelSm} {ui.upper}">Taxa de Consumo Agregada</span>
          <span class="material-symbols-outlined {ui.icon18} {ui.toneSecondary}">speed</span>
        </div>
        <div class={styles.metricValue}>
          <span class="{ui.headlineXl} {ui.semibold} {ui.toneSecondary}">0</span>
          <span class="{ui.labelSm} {ui.muted}">msgs/sec</span>
        </div>
        <div class={styles.spark}><span></span><span></span><span></span><span></span><span></span><span></span></div>
      </div>
      <div class="{ui.metricCard} {ui.cardRaised}">
        <div class={styles.metricHead}>
          <span class="{ui.labelSm} {ui.upper}">Oldest Message in Pipeline</span>
          <span class="material-symbols-outlined {ui.icon18} {ui.tonePrimary}">schedule</span>
        </div>
        <div class={styles.metricValue}>
          <span class="{ui.headlineXl} {ui.semibold} {ui.tonePrimary}">—</span>
        </div>
        <span class="{ui.bodySm} {ui.truncate}">Ack Deadline Médio: {subscriptions[0]?.ackDeadlineSeconds ?? 10} seg</span>
      </div>
      <div class="{ui.metricCard} {ui.cardRaised}">
        <div class={styles.metricHead}>
          <span class="{ui.labelSm} {ui.upper}">DLQ Transbordo (24h)</span>
          <span class="material-symbols-outlined {ui.icon18} {ui.toneTertiary}">warning</span>
        </div>
        <div class={styles.metricValue}>
          <span class="{ui.headlineXl} {ui.semibold}">0</span>
          <span class="{ui.labelSm} {ui.muted}">sem telemetria</span>
        </div>
        <span class={ui.bodySm}>DLQ não está na v1</span>
      </div>
    </div>

    <div class={styles.filters}>
      <div class={styles.filtersMain}>
        <div class={styles.search}>
          <span class="material-symbols-outlined {styles.searchIcon} {ui.muted}">search</span>
          <input
            class="{ui.field} {ui.fieldSearch} {ui.fieldMono} {styles.subSearch}"
            placeholder="Buscar por subscription ID, tópico ou rota push..."
            type="text"
            bind:value={localFilter}
          />
        </div>
        <div class={styles.kinds}>
          <button class={kindClass('all')} type="button" onclick={() => (kindFilter = 'all')}>Todos ({subscriptions.length})</button>
          <button class={kindClass('tutu')} type="button" onclick={() => (kindFilter = 'tutu')}>tutu-* ({subscriptions.filter((s) => s.isObservation).length})</button>
          <button class={kindClass('other')} type="button" onclick={() => (kindFilter = 'other')}>Outras ({subscriptions.filter((s) => !s.isObservation).length})</button>
        </div>
        <div class={styles.pair}>
          <span class="{ui.labelSm} {ui.muted}">Backlog:</span>
          <select class="{styles.filterSelect} {ui.muted}" disabled>
            <option>Todos os Estados</option>
          </select>
        </div>
        <div class={styles.pair}>
          <span class="{ui.labelSm} {ui.muted}">Tópico:</span>
          <select class={styles.filterSelect} bind:value={topicFilter}>
            <option value="all">Todos os Tópicos Origem</option>
            {#each topics as topic}
              <option value={topic.id}>{topic.id}</option>
            {/each}
          </select>
        </div>
      </div>
      <div class={styles.pair}>
        <span class="{ui.labelSm} {ui.muted}">Exibindo
          <span class="{ui.ink} {ui.semibold}">{filtered.length}</span> de {subscriptions.length}</span>
      </div>
    </div>

    <div class={styles.workspace}>
      <div class={styles.list}>
        <div class={ui.scroll}>
          <table class={ui.table}>
            <thead>
              <tr class={styles.head}>
                <th>Subscription ID</th>
                <th>Tópico de Origem</th>
                <th>Tipo</th>
                <th>Backlog</th>
                <th>Ack Dead.</th>
                <th>DLQ</th>
                <th class={ui.right}>Throughput</th>
                <th class={ui.center}>Ações</th>
              </tr>
            </thead>
            <tbody class="{ui.bodySm} {styles.body}">
              {#each filtered as sub}
                {@const active = sub.id === selected?.id}
                <tr
                  class="{ui.dataRow} {active ? ui.dataRowActive : ''}"
                  onclick={() => { selectedId = sub.id; pulled = null; }}
                >
                  <td>
                    <div class={styles.idLine}>
                      <span class="{ui.dot} {ui.dotLg}"></span>
                      <div>
                        <span class="{ui.labelMd} {ui.semibold} {ui.truncate} {styles.id} {ui.block} {active ? ui.tonePrimary : ui.ink}">{sub.id}</span>
                        <span class="{ui.labelSm} {ui.muted} {ui.truncate}">{sub.isObservation ? 'observação UI' : 'outra app'}</span>
                      </div>
                    </div>
                  </td>
                  <td>
                    <span class="{styles.topicLink} {ui.labelSm} {ui.tonePrimary}">
                      <span class="material-symbols-outlined {ui.icon14}">topic</span>
                      <span>{sub.topicId}</span>
                    </span>
                  </td>
                  <td>
                    <span class="{ui.chip} {ui.chipHigh} {ui.tonePrimary} {ui.semibold}">PULL</span>
                  </td>
                  <td>
                    <span class="{ui.chip} {ui.chipOk}">0 msgs</span>
                  </td>
                  <td>
                    <span class="{ui.labelSm} {ui.ink}">{sub.ackDeadlineSeconds}s</span>
                  </td>
                  <td>
                    <span class="{ui.labelSm} {ui.faint}">Desabilitado</span>
                  </td>
                  <td class={ui.right}>
                    <span class="{ui.labelMd} {ui.muted} {ui.semibold}">0 msgs/s</span>
                  </td>
                  <td class={ui.center}>
                    <div class={styles.rowActions}>
                      <button
                        class="{ui.iconQuiet} {ui.hoverTone}"
                        title={sub.isObservation ? 'Puxar Mensagens' : 'Pull só em tutu-*'}
                        type="button"
                        disabled={!sub.isObservation}
                        onclick={(e) => { e.stopPropagation(); onpull(sub); }}
                      >
                        <span class="material-symbols-outlined {ui.icon18}">download</span>
                      </button>
                      <button class={ui.iconQuiet} title="Seek não está na v1" type="button" disabled>
                        <span class="material-symbols-outlined {ui.icon18}">fast_rewind</span>
                      </button>
                      <button class="{ui.iconQuiet} {ui.toneTertiary}" title="Apagar subscription" type="button" onclick={(e) => { e.stopPropagation(); ondelete(sub.id); }}>
                        <span class="material-symbols-outlined {ui.icon18}">delete</span>
                      </button>
                    </div>
                  </td>
                </tr>
              {:else}
                <tr>
                  <td colspan="8" class="{ui.muted}">Nenhuma subscription ainda.</td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
        <div class="{styles.tableFoot} {ui.labelSm} {ui.muted}">
          <span>Expiração Padrão: <span class={ui.ink}>Nunca expira (31 dias inativo)</span></span>
          <span>Ack Deadline Range: 10s - 600s</span>
        </div>
      </div>

      <div class={styles.inspector}>
        {#if selected}
          <div class={styles.inspectorHead}>
            <div>
              <span class="{ui.labelSm} {ui.muted} {ui.upper}">Subscription Inspector</span>
              <h2 class="{ui.headlineLg} {ui.semibold} {ui.truncate}">{selected.id}</h2>
              <span class="{ui.labelSm} {ui.tonePrimary} {ui.truncate} {ui.block}">{resource}</span>
            </div>
            <div class={styles.inspectorTools}>
              <button class={ui.iconButton} title="Copiar Resource Path" type="button" onclick={() => copy(resource)}>
                <span class="material-symbols-outlined {ui.icon16}">content_copy</span>
              </button>
              <button class={ui.iconButton} title="Editar não está na v1" type="button" disabled>
                <span class="material-symbols-outlined {ui.icon16}">tune</span>
              </button>
            </div>
          </div>
          <div class={styles.stats}>
            <div class={styles.stat}>
              <span class="{ui.labelSm} {ui.muted}">Ack Latency (p95)</span>
              <span class="{ui.headlineMd} {ui.semibold}">—</span>
              <span class="{ui.labelSm} {ui.muted}">sem telemetria</span>
            </div>
            <div class={styles.stat}>
              <span class="{ui.labelSm} {ui.muted}">Oldest Unacked Age</span>
              <span class="{ui.headlineMd} {ui.semibold}">—</span>
              <span class="{ui.labelSm} {ui.muted}">sem telemetria</span>
            </div>
          </div>
          <div class="{styles.meta} {ui.labelSm} {ui.muted}">
            <div class={styles.metaRow}><span>Origem:</span><span class="{ui.ink} {ui.semibold}">{selected.topicId}</span></div>
            <div class={styles.metaRow}><span>Delivery Type:</span><span class="{ui.tonePrimary} {ui.semibold}">PULL</span></div>
            <div class={styles.metaRow}><span>Ack Deadline:</span><span class={ui.ink}>{selected.ackDeadlineSeconds} segundos</span></div>
            <div class={styles.metaRow}><span>Dead Letter Topic:</span><span>Desabilitado</span></div>
            <div class={styles.metaRow}><span>Observação UI:</span><span class={selected.isObservation ? `${ui.toneSecondary} ${ui.semibold}` : ui.ink}>{selected.isObservation ? 'tutu-*' : 'não'}</span></div>
          </div>
          <div class={styles.pull}>
            <div class={styles.pullHead}>
              <span class="{ui.headlineMd} {styles.pullTitle}">
                <span class="material-symbols-outlined {ui.icon18} {ui.tonePrimary}">terminal</span>
                Simular Pull (Live Test)
              </span>
              <span class="{ui.labelSm} {ui.muted}">max_messages=1</span>
            </div>
            <div class={styles.pullRow}>
              <button
                class={styles.run}
                type="button"
                disabled={!selected.isObservation || busy}
                onclick={pullOne}
              >
                <span class="material-symbols-outlined {ui.icon18}">play_arrow</span>
                <span>Executar Pull</span>
              </button>
              <button class={styles.ack} type="button" disabled={!pulled} onclick={() => settle('ack')}>
                <span class="material-symbols-outlined {ui.icon16}">check_circle</span>
                <span>ACK</span>
              </button>
              <button class={styles.nack} type="button" disabled={!pulled} onclick={() => settle('nack')}>
                <span class="material-symbols-outlined {ui.icon16}">cancel</span>
                <span>NACK</span>
              </button>
            </div>
            <div class="{styles.output} {ui.labelSm}">
              <div class="{styles.outputHead} {ui.faint}">
                <span>Simulated Output (gcloud CLI)</span>
                <span class={ui.toneSecondary}>{pulled ? 'PULLED' : selected.isObservation ? 'READY' : 'BLOQUEADO'}</span>
              </div>
              <pre class={ui.muted}><code>{pulled
                ? JSON.stringify({ id: pulled.id, data: pulled.data, attributes: pulled.attributes }, null, 2)
                : selected.isObservation
                  ? `$ gcloud pubsub subscriptions pull ${selected.id} --auto-ack=false --limit=1\nAguardando execução do pull manual...`
                  : 'Pull só é permitido em subscriptions tutu-*.'}</code></pre>
            </div>
          </div>
        {:else}
          <p class={ui.bodySm}>Selecione uma subscription.</p>
        {/if}
      </div>
    </div>
  </div>
</main>
