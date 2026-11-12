<script>
  import { api } from '../api.js';
  import ui from '../ui.module.css';
  import styles from './Messages.module.css';

  let {
    topics,
    subscriptions,
    health,
    filter = '',
    notice = $bindable(''),
    initialTopic = '',
    openPublisher = false,
  } = $props();

  let topicId = $state('');
  let observationId = $state('');
  let tab = $state('inspector');
  let payloadView = $state('json');
  let autoAck = $state(false);
  let messages = $state([]);
  let selectedAck = $state(null);
  let busy = $state(false);
  let data = $state('{\n  "ok": true\n}');
  let attrRows = $state([{ key: 'env', value: 'local' }]);
  let lastPublishId = $state('');

  $effect(() => {
    if (openPublisher) tab = 'publisher';
  });

  $effect(() => {
    if (initialTopic) topicId = initialTopic;
    else if (!topicId && topics[0]) topicId = topics[0].id;
  });

  const selected = $derived(messages.find((m) => m.ackId === selectedAck) ?? messages[0] ?? null);
  const query = $derived(filter.toLowerCase());
  const visible = $derived(
    messages.filter((m) => {
      if (!query) return true;
      const attrs = Object.entries(m.attributes ?? {}).map(([k, v]) => `${k}=${v}`).join(' ');
      return `${m.id} ${m.data} ${attrs}`.toLowerCase().includes(query);
    }),
  );
  const observation = $derived(
    subscriptions.find((s) => s.isObservation && s.topicId === topicId) ?? null,
  );
  const attrCount = $derived(Object.keys(selected?.attributes ?? {}).length);
  const pretty = $derived(formatPayload(selected?.data, payloadView));
  const jsonOk = $derived(isJson(data));

  $effect(() => {
    if (observation) observationId = observation.id;
  });

  function isJson(raw) {
    try {
      JSON.parse(raw);
      return true;
    } catch {
      return false;
    }
  }

  function formatPayload(raw, view) {
    if (!raw) return '';
    if (view === 'raw') return raw;
    if (view === 'base64') return btoa(unescape(encodeURIComponent(raw)));
    if (view === 'hex') {
      return [...new TextEncoder().encode(raw)].map((b) => b.toString(16).padStart(2, '0')).join(' ');
    }
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  }

  function clock(iso) {
    if (!iso) return '—';
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return iso;
    return d.toISOString().slice(11, 23).replace('T', '');
  }

  function sizeLabel(n) {
    if (n == null) return '—';
    return n >= 1024 ? `${(n / 1024).toFixed(2)} KB` : `${n} B`;
  }

  function parseAttrs() {
    const map = {};
    for (const row of attrRows) {
      if (row.key) map[row.key] = row.value;
    }
    return map;
  }

  async function observe() {
    const sub = await api(`/api/topics/${encodeURIComponent(topicId)}/observe`, { method: 'POST' });
    observationId = sub.id;
    return sub.id;
  }

  async function publish() {
    busy = true;
    notice = '';
    lastPublishId = '';
    try {
      await observe();
      const result = await api(`/api/topics/${encodeURIComponent(topicId)}/messages`, {
        method: 'POST',
        body: JSON.stringify({ data, attributes: parseAttrs() }),
      });
      lastPublishId = result.messageId;
      notice = `Publicado ${result.messageId}`;
    } catch (err) {
      notice = err.message;
    } finally {
      busy = false;
    }
  }

  async function pull() {
    if (!topicId) return;
    busy = true;
    notice = '';
    try {
      const id = observationId || (await observe());
      const result = await api(`/api/subscriptions/${encodeURIComponent(id)}/pull`, {
        method: 'POST',
        body: JSON.stringify({ maxMessages: 50 }),
      });
      const batch = result.messages ?? [];
      if (autoAck && batch.length) {
        await api(`/api/subscriptions/${encodeURIComponent(id)}/ack`, {
          method: 'POST',
          body: JSON.stringify({ ackIds: batch.map((m) => m.ackId) }),
        });
      }
      messages = [...batch, ...messages];
      selectedAck = messages[0]?.ackId ?? null;
    } catch (err) {
      notice = err.message;
    } finally {
      busy = false;
    }
  }

  async function settle(kind, message) {
    notice = '';
    try {
      await api(`/api/subscriptions/${encodeURIComponent(message.subscriptionId)}/${kind}`, {
        method: 'POST',
        body: JSON.stringify({ ackIds: [message.ackId] }),
      });
      messages = messages.filter((m) => m.ackId !== message.ackId);
      if (selectedAck === message.ackId) selectedAck = messages[0]?.ackId ?? null;
    } catch (err) {
      notice = err.message;
    }
  }

  function cloneToPublisher() {
    if (!selected) return;
    data = formatPayload(selected.data, 'json');
    attrRows = Object.entries(selected.attributes ?? {}).map(([key, value]) => ({ key, value }));
    if (!attrRows.length) attrRows = [{ key: '', value: '' }];
    tab = 'publisher';
  }

  function copy(text) {
    navigator.clipboard.writeText(text);
  }

  function tabClass(id) {
    return tab === id ? styles.tabOn : styles.tab;
  }

  function viewClass(id) {
    return payloadView === id ? styles.viewOn : styles.view;
  }
</script>

<main class={ui.page}>
  <div class={styles.column}>
    <section class={styles.source}>
      <div class={styles.sourceRow}>
        <div class={styles.sourceMain}>
          <div class="{styles.sourceTag} {ui.labelSm} {ui.upper} {ui.muted}">
            <span class="material-symbols-outlined {ui.icon15} {ui.tonePrimary}">filter_alt</span>
            Fonte Ativa
          </div>
          <div class={styles.topicPick}>
            <select class="{ui.field} {ui.fieldMono} {ui.select} {styles.topicSelect}" bind:value={topicId}>
              <optgroup label="Tópicos">
                {#each topics as topic}
                  <option value={topic.id}>projects/{health.projectId}/topics/{topic.id}</option>
                {/each}
              </optgroup>
            </select>
            <span class="material-symbols-outlined {styles.topicIcon} {ui.tonePrimary}">swap_calls</span>
            <span class="material-symbols-outlined {styles.topicChevron} {ui.muted}">expand_more</span>
          </div>
          <div class={styles.deadline}>
            <span class="material-symbols-outlined {ui.icon16} {ui.toneSecondary}">speed</span>
            <span class="{ui.labelSm} {ui.muted}">Ack Deadline:</span>
            <span class="{ui.labelSm} {ui.toneSecondary} {ui.semibold}">{observation?.ackDeadlineSeconds ?? 10}s</span>
          </div>
        </div>
        <div class={styles.sourceTools}>
          <div class={styles.pullGroup}>
            <button class={styles.pull} type="button" onclick={pull} disabled={!topicId || busy}>
              <span class="material-symbols-outlined {ui.icon16} {ui.tonePrimary}">download</span>
              Puxar 50 Msgs
            </button>
          </div>
          <button class={styles.liveOff} type="button" disabled title="Live Stream não está na v1">
            <span class="{ui.dot} {ui.dotLg} {styles.off}"></span>
            <span class="{ui.labelMd} {ui.muted}">Live Stream: OFF</span>
          </button>
          <label class={styles.autoAck}>
            <input class={styles.check} type="checkbox" bind:checked={autoAck} />
            <span class="{ui.labelSm} {ui.muted}">Auto-ACK</span>
          </label>
          <button class={ui.iconButton} title="Ack all não está na v1" type="button" disabled>
            <span class="material-symbols-outlined {ui.icon17}">done_all</span>
          </button>
        </div>
      </div>
      <div class={styles.sourceMeta}>
        <div class="{styles.attrFilter} {ui.muted}">
          <span class="{ui.labelSm} {ui.upper}">Filtro Atributos:</span>
          <span class="{ui.labelSm} {ui.faint}">use a busca do header</span>
        </div>
        <div class="{styles.statsLine} {ui.labelSm} {ui.muted}">
          <span>Backlog Local: <strong class={ui.ink}>{messages.length} itens</strong></span>
          <span>Taxa Ingestão: <strong class={ui.ink}>0 msg/s</strong></span>
          <span>Atraso: <strong class={ui.ink}>—</strong></span>
        </div>
      </div>
    </section>

    <div class={styles.workspace}>
      <div class={styles.queue}>
        <div class={styles.queueHead}>
          <div class={styles.queueTitle}>
            <span class="material-symbols-outlined {ui.icon18} {ui.tonePrimary}">receipt_long</span>
            <span class={ui.headlineMd}>Fila de Mensagens</span>
            <span class="{ui.pill} {ui.pillMuted}">{visible.length} msgs capturadas</span>
          </div>
          <button class="{ui.labelSm} {styles.clear}" type="button" onclick={() => { messages = []; selectedAck = null; }}>
            <span class="material-symbols-outlined {ui.icon14}">clear_all</span>
            Limpar Visão
          </button>
        </div>
        <div class={ui.scroll}>
          <table class="{ui.table} {styles.fixed}">
            <thead>
              <tr class={styles.head}>
                <th class={styles.colIndex}>#</th>
                <th class={styles.colId}>Message ID</th>
                <th class={styles.colTime}>Publicado</th>
                <th class={styles.colAttrs}>Atributos Principais</th>
                <th class={styles.colSize}>Tam.</th>
                <th class={styles.colStatus}>Status</th>
                <th class={styles.colActions}>Ações</th>
              </tr>
            </thead>
            <tbody class="{styles.divided} {ui.bodySm}">
              {#each visible as message, i}
                {@const active = message.ackId === selected?.ackId}
                {@const attrs = Object.entries(message.attributes ?? {})}
                <tr class="{styles.messageRow} {active ? styles.messageRowActive : ''}" onclick={() => (selectedAck = message.ackId)}>
                  <td class="{styles.index} {ui.labelSm} {ui.tonePrimary}">
                    {#if active}
                      <span class="{ui.dot} {ui.dotPrimary}"></span>
                    {:else}
                      {i + 1}
                    {/if}
                  </td>
                  <td>
                    <div class={styles.idLine}>
                      <span class="{ui.labelSm} {ui.truncate} {active ? `${ui.tonePrimary} ${ui.semibold}` : ui.ink}">{message.id}</span>
                      <button class={styles.copyId} title="Copiar ID" type="button" onclick={(e) => { e.stopPropagation(); copy(message.id); }}>
                        <span class="material-symbols-outlined {ui.icon14}">content_copy</span>
                      </button>
                    </div>
                  </td>
                  <td class="{ui.labelSm} {ui.muted} {ui.nowrap}">{clock(message.publishTime)}</td>
                  <td class={ui.truncate}>
                    {#each attrs.slice(0, 2) as [k, v]}
                      <span class="{styles.attr} {ui.labelSm} {ui.muted}">{k}: {v}</span>
                    {:else}
                      <span class="{ui.labelSm} {ui.faint}">—</span>
                    {/each}
                  </td>
                  <td class="{ui.labelSm} {ui.muted} {ui.nowrap}">{sizeLabel(message.sizeBytes)}</td>
                  <td class={ui.nowrap}>
                    <span class="{ui.chip} {ui.chipInfo}">Puxada</span>
                  </td>
                  <td class={ui.right}>
                    <div class={styles.rowActions}>
                      <button class="{ui.iconButton} {ui.toneSecondary}" title="ACK" type="button" onclick={(e) => { e.stopPropagation(); settle('ack', message); }}>
                        <span class="material-symbols-outlined {ui.icon14}">check_circle</span>
                      </button>
                      <button class="{ui.iconButton} {ui.toneTertiary}" title="NACK" type="button" onclick={(e) => { e.stopPropagation(); settle('nack', message); }}>
                        <span class="material-symbols-outlined {ui.icon14}">cancel</span>
                      </button>
                    </div>
                  </td>
                </tr>
              {:else}
                <tr>
                  <td colspan="7" class="{styles.empty} {ui.bodySm}">Nada puxado. Publique e clique em Puxar 50 Msgs. Fila: {observationId || 'tutu-* ainda não criada'}.</td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
        <div class="{styles.queueFoot} {ui.labelSm} {ui.muted}">
          <div class={styles.buffer}>
            <span class="{ui.dot} {ui.dotLg} {ui.pulse}"></span>
            <span>Buffer: <span class="{ui.ink} {ui.semibold}">{visible.length} / 50</span> mensagens no cursor local</span>
          </div>
        </div>
      </div>

      <div class={styles.inspector}>
        <div class={styles.tabBar}>
          <div class={styles.tabs}>
            <button class={tabClass('inspector')} type="button" onclick={() => (tab = 'inspector')}>
              <span class="material-symbols-outlined {ui.icon17}">data_object</span>
              Payload Inspector
            </button>
            <button class={tabClass('publisher')} type="button" onclick={() => (tab = 'publisher')}>
              <span class="material-symbols-outlined {ui.icon17}">send</span>
              Publicar Mensagem
            </button>
          </div>
          <span class="{ui.labelSm} {ui.muted} {styles.port}">Port {health.hostPort}</span>
        </div>

        {#if tab === 'inspector'}
          <div class={styles.pane}>
            {#if selected}
              <div class={styles.selected}>
                <div class={styles.selectedHead}>
                  <span class="{ui.labelSm} {ui.upper} {ui.muted}">Mensagem Selecionada</span>
                  <button class={ui.chipToggleOn} type="button" onclick={() => copy(pretty)}>
                    <span class="material-symbols-outlined {ui.icon14} {ui.toneSecondary}">code</span>
                    SDK Payload
                  </button>
                </div>
                <div class={styles.metaGrid}>
                  <div class={styles.metaCell}>
                    <span class="{ui.labelSm} {ui.muted} {ui.block}">Message ID</span>
                    <span class="{ui.labelMd} {ui.tonePrimary} {ui.semibold} {styles.break}">{selected.id}</span>
                  </div>
                  <div class={styles.metaCell}>
                    <span class="{ui.labelSm} {ui.muted} {ui.block}">Publish Time</span>
                    <span class="{ui.labelMd} {ui.ink}">{selected.publishTime}</span>
                  </div>
                </div>
                <div class={styles.selectedHead}>
                  <span class="{ui.labelSm} {ui.muted}">Ordering Key: <span class={ui.faint}>—</span></span>
                  <span class="{ui.labelSm} {ui.muted}">Tamanho: <strong class={ui.ink}>{sizeLabel(selected.sizeBytes)}</strong></span>
                </div>
              </div>
              <div class={styles.section}>
                <div class={styles.sectionHead}>
                  <span class="{ui.headlineMd} {styles.sourceMain}">
                    <span class="material-symbols-outlined {ui.icon16} {ui.tonePrimary}">sell</span>
                    Pub/Sub Attributes ({attrCount})
                  </span>
                  <button class={ui.textButton} type="button" onclick={() => copy(JSON.stringify(selected.attributes ?? {}))}>Copiar Todos</button>
                </div>
                <div class={styles.attrTable}>
                  <table class={ui.labelSm}>
                    <thead>
                      <tr class={ui.muted}>
                        <th>Chave</th>
                        <th>Valor</th>
                      </tr>
                    </thead>
                    <tbody class={styles.attrDivided}>
                      {#each Object.entries(selected.attributes ?? {}) as [k, v]}
                        <tr>
                          <td class="{ui.tonePrimary} {ui.medium}">{k}</td>
                          <td class={ui.ink}>{v}</td>
                        </tr>
                      {:else}
                        <tr><td colspan="2" class={ui.faint}>sem atributos</td></tr>
                      {/each}
                    </tbody>
                  </table>
                </div>
              </div>
              <div class={styles.section}>
                <div class={styles.viewBar}>
                  <div class={styles.views}>
                    <button class={viewClass('json')} type="button" onclick={() => (payloadView = 'json')}>JSON Pretty</button>
                    <button class={viewClass('raw')} type="button" onclick={() => (payloadView = 'raw')}>Raw Text</button>
                    <button class={viewClass('base64')} type="button" onclick={() => (payloadView = 'base64')}>Base64</button>
                    <button class={viewClass('hex')} type="button" onclick={() => (payloadView = 'hex')}>Hex</button>
                  </div>
                  <button class={ui.iconButton} title="Copiar Payload" type="button" onclick={() => copy(pretty)}>
                    <span class="material-symbols-outlined {ui.icon15}">content_copy</span>
                  </button>
                </div>
                <div class="{styles.payload} {ui.labelSm}">
                  <pre class={ui.tonePrimary}>{pretty}</pre>
                </div>
              </div>
              <div class={styles.ackRow}>
                <div class={styles.ackActions}>
                  <button class={styles.ack} type="button" onclick={() => settle('ack', selected)}>
                    <span class="material-symbols-outlined {ui.icon16}">check_circle</span>
                    ACK Mensagem
                  </button>
                  <button class={styles.nack} type="button" onclick={() => settle('nack', selected)}>
                    <span class="material-symbols-outlined {ui.icon16}">cancel</span>
                    NACK (Re-enfileirar)
                  </button>
                </div>
                <button class={styles.clone} type="button" onclick={cloneToPublisher}>
                  <span class="material-symbols-outlined {ui.icon16}">edit_note</span>
                  Clonar no Publisher
                </button>
              </div>
            {:else}
              <p class={ui.bodySm}>Selecione uma mensagem puxada.</p>
            {/if}
          </div>
        {:else}
          <div class={styles.pane}>
            <div class={styles.field}>
              <div class={styles.fieldHead}>
                <label class={ui.headlineMd} for="pub-topic">Tópico de Destino</label>
                <div class={styles.templateRow}>
                  <span class={ui.labelSm}>Templates:</span>
                  <button class="{styles.template} {ui.tonePrimary}" type="button" onclick={() => (data = '{\n  "event": "order.created"\n}')}>Order Created</button>
                  <button class="{styles.template} {ui.toneSecondary}" type="button" onclick={() => (data = '{\n  "status": "ok"\n}')}>Payment OK</button>
                  <button class="{styles.template} {ui.ink}" type="button" onclick={() => (data = '{}')}>Limpo</button>
                </div>
              </div>
              <div class={styles.topicField}>
                <select id="pub-topic" class="{ui.field} {ui.fieldMono} {ui.select} {styles.pubSelect}" bind:value={topicId}>
                  {#each topics as topic}
                    <option value={topic.id}>projects/{health.projectId}/topics/{topic.id}</option>
                  {/each}
                </select>
                <span class="material-symbols-outlined {styles.topicIcon} {ui.tonePrimary}">topic</span>
              </div>
            </div>
            <div class={styles.field}>
              <div class={styles.fieldHead}>
                <span class={ui.headlineMd}>Message Attributes (Metadata)</span>
                <button class="{styles.template} {ui.labelSm} {ui.tonePrimary}" type="button" onclick={() => (attrRows = [...attrRows, { key: '', value: '' }])}>
                  <span class="material-symbols-outlined {ui.icon14}">add</span> Adicionar Atributo
                </button>
              </div>
              <div class={styles.attrs}>
                {#each attrRows as row, i}
                  <div class={styles.attrRow}>
                    <input class="{styles.attrInput} {styles.key} {ui.tonePrimary}" placeholder="Key" bind:value={row.key} />
                    <input class="{styles.attrInput} {styles.value}" placeholder="Value" bind:value={row.value} />
                    <button class="{ui.iconQuiet} {ui.toneTertiary}" type="button" onclick={() => (attrRows = attrRows.filter((_, j) => j !== i))}>
                      <span class="material-symbols-outlined {ui.icon16}">delete</span>
                    </button>
                  </div>
                {/each}
              </div>
            </div>
            <div class={styles.field}>
              <div class={styles.fieldHead}>
                <label class={ui.headlineMd} for="publisher-body">Payload da Mensagem (JSON)</label>
                <div class={styles.templateRow}>
                  <span class="{ui.labelSm} {jsonOk ? ui.toneSecondary : ui.toneTertiary}">
                    <span class="material-symbols-outlined {ui.icon14}">{jsonOk ? 'check_circle' : 'error'}</span>
                    {jsonOk ? 'JSON Válido' : 'JSON inválido'}
                  </span>
                  <button class="{styles.template} {ui.labelSm} {ui.muted}" type="button" onclick={() => { try { data = JSON.stringify(JSON.parse(data), null, 2); } catch { /* keep */ } }}>Formatar</button>
                </div>
              </div>
              <textarea id="publisher-body" class={styles.editor} rows="8" bind:value={data}></textarea>
            </div>
            <div class={styles.publishRow}>
              <div class={styles.publishMeta}>
                <button class={styles.publish} type="button" onclick={publish} disabled={!topicId || busy}>
                  <span class="material-symbols-outlined {ui.icon18}">publish</span>
                  Publicar no Emulador
                </button>
                <span class="{ui.labelSm} {ui.muted}">HTTP {health.hostPort}</span>
              </div>
            </div>
            {#if lastPublishId}
              <div class="{styles.success} {ui.labelSm}">
                <span class="material-symbols-outlined {ui.icon16}">check_circle</span>
                <span>Mensagem publicada com sucesso! ID gerado: <strong>{lastPublishId}</strong></span>
              </div>
            {/if}
          </div>
        {/if}
      </div>
    </div>

    <section class={styles.kpis}>
      <div class={ui.metricCard}>
        <div class={styles.kpiHead}>
          <span class="{ui.labelSm} {ui.upper} {ui.muted}">Ack Ratio (Hoje)</span>
          <span class="material-symbols-outlined {ui.icon18} {ui.toneSecondary}">verified</span>
        </div>
        <div class={styles.kpiValue}>
          <span class="{ui.headlineXl} {ui.semibold}">—</span>
          <span class="{ui.labelSm} {ui.muted}">sem telemetria</span>
        </div>
        <div class="{ui.bar} {ui.barTall} {styles.kpiBar}"><div class="{ui.barFill} {ui.barSecondary}"></div></div>
      </div>
      <div class={ui.metricCard}>
        <div class={styles.kpiHead}>
          <span class="{ui.labelSm} {ui.upper} {ui.muted}">Emulador Backlog Total</span>
          <span class="material-symbols-outlined {ui.icon18} {ui.tonePrimary}">inbox</span>
        </div>
        <div class={styles.kpiValue}>
          <span class="{ui.headlineXl} {ui.semibold} {ui.tonePrimary}">0</span>
          <span class="{ui.labelSm} {ui.muted}">mensagens em buffer</span>
        </div>
        <div class="{ui.bar} {ui.barTall} {styles.kpiBar}"><div class="{ui.barFill} {ui.barPrimary}"></div></div>
      </div>
      <div class={ui.metricCard}>
        <div class={styles.kpiHead}>
          <span class="{ui.labelSm} {ui.upper} {ui.muted}">Latência de Entrega (P95)</span>
          <span class="material-symbols-outlined {ui.icon18} {ui.toneSecondary}">timer</span>
        </div>
        <div class={styles.kpiValue}>
          <span class="{ui.headlineXl} {ui.semibold}">—</span>
          <span class="{ui.labelSm} {ui.muted}">Local Loopback</span>
        </div>
      </div>
      <div class={ui.metricCard}>
        <div class={styles.kpiHead}>
          <span class="{ui.labelSm} {ui.upper} {ui.muted}">Taxa Dead-Letter (DLQ)</span>
          <span class="material-symbols-outlined {ui.icon18} {ui.toneTertiary}">error</span>
        </div>
        <div class={styles.kpiValue}>
          <span class="{ui.headlineXl} {ui.semibold}">0</span>
          <span class="{ui.labelSm} {ui.muted}">sem telemetria</span>
        </div>
        <div class="{ui.bar} {ui.barTall} {styles.kpiBar}"><div class="{ui.barFill} {ui.barTertiary}"></div></div>
      </div>
    </section>
  </div>
</main>
