<script>
  import ui from '../ui.module.css';
  import styles from './Topics.module.css';

  let {
    topics,
    subscriptions,
    health,
    filter = '',
    oncreate,
    onpublish,
    onnewsub,
    ondelete,
  } = $props();

  let selectedId = $state('');
  let sortKey = $state('name');
  let localFilter = $state('');

  $effect(() => {
    if (!selectedId && topics[0]) selectedId = topics[0].id;
    if (selectedId && topics.length && !topics.some((t) => t.id === selectedId)) {
      selectedId = topics[0]?.id ?? '';
    }
  });

  function subsOf(topicId) {
    return subscriptions.filter((s) => s.topicId === topicId);
  }

  function copy(text) {
    navigator.clipboard.writeText(text);
  }

  const query = $derived((filter || localFilter).toLowerCase());
  const filtered = $derived(
    [...topics]
      .filter((t) => {
        if (!query) return true;
        const names = subsOf(t.id).map((s) => s.id).join(' ');
        return `${t.id} ${names}`.toLowerCase().includes(query);
      })
      .sort((a, b) => (sortKey === 'name' ? a.id.localeCompare(b.id) : 0)),
  );
  const selected = $derived(topics.find((t) => t.id === selectedId) ?? filtered[0] ?? null);
  const linked = $derived(selected ? subsOf(selected.id) : []);
  const resource = $derived(
    selected ? `projects/${health.projectId}/topics/${selected.id}` : '',
  );
  const gcloud = $derived(
    selected
      ? `$(gcloud beta emulators pubsub env-init) && gcloud pubsub topics publish ${selected.id} --message='{"ok":true}'`
      : '',
  );

  function sortClass(key) {
    return sortKey === key ? ui.chipToggleOn : ui.chipToggle;
  }
</script>

<main class={ui.page}>
  <div>
    <div class={styles.header}>
      <div class={styles.titleBlock}>
        <div class={styles.titleIcon}>
          <span class="material-symbols-outlined">topic</span>
        </div>
        <div>
          <div class={styles.titleLine}>
            <h1 class={ui.headlineXl}>Tópicos</h1>
            <span class="{ui.pill} {ui.pillOk}">{topics.length} ATIVOS</span>
            <span class="{ui.chip} {ui.upper}">EMULATOR</span>
          </div>
          <p class={ui.bodySm}>Gerencie barramentos de mensageria e roteamento de assinantes locais.</p>
        </div>
      </div>
      <div class={styles.actions}>
        <button class={ui.secondaryButton} type="button">
          <span class="material-symbols-outlined {ui.icon16} {ui.muted}">sync</span>
          <span>Atualizar Lista</span>
        </button>
        <div class={styles.divider}></div>
        <button class={ui.primaryButton} type="button" onclick={oncreate}>
          <span class="material-symbols-outlined {ui.icon18}">add_circle</span>
          <span>Criar Tópico</span>
        </button>
      </div>
    </div>

    <div class={styles.metricGrid}>
      <div class={ui.metricCard}>
        <div class={styles.metricHead}>
          <span class="{ui.labelSm} {ui.upper}">Vazão Global Ingest</span>
          <span class="material-symbols-outlined {ui.icon18} {ui.tonePrimary}">speed</span>
        </div>
        <div class={styles.metricValue}>
          <span class="{ui.headlineXl} {ui.semibold}">0</span>
          <span class="{ui.labelSm} {ui.muted}">msgs / sec</span>
          <span class="{ui.labelSm} {ui.muted} {styles.metricAside}">—</span>
        </div>
        <div class={ui.bar}><div class="{ui.barFill} {ui.barPrimary}"></div></div>
      </div>
      <div class={ui.metricCard}>
        <div class={styles.metricHead}>
          <span class="{ui.labelSm} {ui.upper}">Backlog Acumulado</span>
          <span class="material-symbols-outlined {ui.icon18} {ui.toneSecondary}">stacked_bar_chart</span>
        </div>
        <div class={styles.metricValue}>
          <span class="{ui.headlineXl} {ui.semibold}">0</span>
          <span class="{ui.labelSm} {ui.muted}">mensagens</span>
          <span class="{ui.chip} {ui.chipMid} {ui.toneSecondary} {styles.metricAside}">Saudável</span>
        </div>
        <div class={ui.bar}><div class="{ui.barFill} {ui.barSecondary}"></div></div>
      </div>
      <div class={ui.metricCard}>
        <div class={styles.metricHead}>
          <span class="{ui.labelSm} {ui.upper}">Subscrições Vinculadas</span>
          <span class="material-symbols-outlined {ui.icon18} {ui.tonePrimary}">mark_email_unread</span>
        </div>
        <div class={styles.metricValue}>
          <span class="{ui.headlineXl} {ui.semibold}">{subscriptions.length}</span>
          <span class="{ui.labelSm} {ui.muted}">endpoints</span>
          <span class="{ui.labelSm} {ui.tonePrimary} {ui.semibold} {styles.metricAside}">100% PULL OK</span>
        </div>
        <div class={ui.bar}><div class="{ui.barFill} {ui.barAccent} {ui.barFull}"></div></div>
      </div>
      <div class={ui.metricCard}>
        <div class={styles.metricHead}>
          <span class="{ui.labelSm} {ui.upper}">Falhas / DLQ (24h)</span>
          <span class="material-symbols-outlined {ui.icon18} {ui.toneTertiary}">error_outline</span>
        </div>
        <div class={styles.metricValue}>
          <span class="{ui.headlineXl} {ui.semibold}">0</span>
          <span class="{ui.labelSm} {ui.muted}">nacks retidos</span>
          <span class="{ui.labelSm} {ui.muted} {styles.metricAside}">—</span>
        </div>
        <div class={ui.bar}><div class="{ui.barFill} {ui.barTertiary}"></div></div>
      </div>
    </div>

    <div class={styles.workspace}>
      <div class={styles.mainCol}>
        <div class={styles.toolbar}>
          <div class={styles.search}>
            <span class="material-symbols-outlined {styles.searchIcon}">filter_list</span>
            <input
              class="{ui.field} {ui.fieldSearch} {ui.fieldBody}"
              placeholder="Filtrar por nome do tópico, schema ou subscription..."
              type="text"
              bind:value={localFilter}
            />
          </div>
          <div class={styles.sort}>
            <span class="{ui.labelSm} {ui.muted} {styles.sortLabel}">Ordenar:</span>
            <button class={sortClass('throughput')} type="button" disabled title="Vazão não está na API">Vazão</button>
            <button class={sortClass('name')} type="button" onclick={() => (sortKey = 'name')}>Nome</button>
            <button class={sortClass('volume')} type="button" disabled title="Volume não está na API">Hoje</button>
          </div>
        </div>

        <div class={ui.panel}>
          <div class={ui.scroll}>
            <table class={ui.table}>
              <thead>
                <tr class={ui.headRow}>
                  <th class={ui.headCellWide}>Nome do Tópico</th>
                  <th class={ui.headCell}>Status</th>
                  <th class={ui.headCell}>Subscriptions</th>
                  <th class={ui.headCell}>Msgs Hoje</th>
                  <th class={ui.headCell}>Throughput</th>
                  <th class={ui.headCell}>Schema</th>
                  <th class={ui.headCell}>Retenção</th>
                  <th class="{ui.headCellWide} {ui.right}">Ações</th>
                </tr>
              </thead>
              <tbody>
                {#each filtered as topic}
                  {@const linkedSubs = subsOf(topic.id)}
                  {@const active = topic.id === selected?.id}
                  <tr
                    class="{ui.dataRow} {active ? ui.dataRowActive : ''}"
                    onclick={() => (selectedId = topic.id)}
                  >
                    <td class={ui.bodyCellWide}>
                      <div class={styles.nameLine}>
                        <span class="{ui.dot} {active ? ui.dotPrimary : ''}"></span>
                        <span class="{ui.labelMd} {ui.semibold} {ui.truncate} {styles.id} {active ? ui.tonePrimary : ui.ink}" title="projects/{health.projectId}/topics/{topic.id}">{topic.id}</span>
                        <button class="{ui.iconQuiet} {ui.hoverTone}" onclick={(e) => { e.stopPropagation(); copy(topic.id); }} title="Copiar ID do Tópico" type="button">
                          <span class="material-symbols-outlined {ui.icon14}">content_copy</span>
                        </button>
                      </div>
                      <span class="{ui.labelSm} {ui.faint} {ui.block} {ui.truncate}">projects/{health.projectId}/topics/...</span>
                    </td>
                    <td class={ui.bodyCell}>
                      <span class="{ui.pill} {ui.pillOk}">
                        <span class={ui.dot}></span> Ativo
                      </span>
                    </td>
                    <td class={ui.bodyCell}>
                      <div class={styles.inline}>
                        <span class="{ui.chip} {ui.ink} {ui.semibold}">{linkedSubs.length}</span>
                        <span class="{ui.labelSm} {ui.faint} {ui.truncate} {styles.subName}">{linkedSubs[0]?.id ?? '—'}</span>
                      </div>
                    </td>
                    <td class={ui.bodyCell}>
                      <div class={styles.stack}>
                        <span class="{ui.labelMd} {ui.ink} {ui.semibold}">0</span>
                        <span class="{ui.labelSm} {ui.muted}">—</span>
                      </div>
                    </td>
                    <td class={ui.bodyCell}>
                      <div class={styles.inline}>
                        <span class="{ui.labelMd} {ui.ink} {ui.semibold}">0</span>
                        <span class="{ui.labelSm} {ui.muted}">m/s</span>
                      </div>
                    </td>
                    <td class={ui.bodyCell}>
                      <span class="{ui.chip} {ui.chipHigh} {ui.truncate} {styles.schema}">None (Raw)</span>
                    </td>
                    <td class="{ui.bodyCell} {ui.labelSm} {ui.muted}">—</td>
                    <td class="{ui.bodyCellWide} {ui.right}">
                      <div class={styles.actionRow}>
                        <button class="{ui.iconButton} {ui.hoverPrimary}" title="Publicar Mensagem" type="button" onclick={(e) => { e.stopPropagation(); onpublish(topic.id); }}>
                          <span class="material-symbols-outlined {ui.icon16}">send</span>
                        </button>
                        <button class={ui.iconButton} title="Criar Subscription" type="button" onclick={(e) => { e.stopPropagation(); onnewsub(topic.id); }}>
                          <span class="material-symbols-outlined {ui.icon16}">bookmark_add</span>
                        </button>
                        <button class="{ui.iconQuiet} {ui.toneTertiary}" title="Apagar tópico" type="button" onclick={(e) => { e.stopPropagation(); ondelete(topic.id); }}>
                          <span class="material-symbols-outlined {ui.icon16}">delete</span>
                        </button>
                      </div>
                    </td>
                  </tr>
                {:else}
                  <tr>
                    <td colspan="8" class="{ui.bodyCellWide} {ui.bodySm}">Nenhum tópico ainda.</td>
                  </tr>
                {/each}
              </tbody>
            </table>
          </div>
        </div>

        <div class="{ui.card} {styles.cli}">
          <span class="material-symbols-outlined {styles.cliIcon}">terminal</span>
          <div class={styles.cliBody}>
            <div class={styles.cliHead}>
              <span class={ui.headlineMd}>Comando gcloud de publicação rápida</span>
              <button class={ui.textButton} type="button" onclick={() => copy(gcloud)} disabled={!gcloud}>
                Copiar CLI
                <span class="material-symbols-outlined {ui.icon14}">file_copy</span>
              </button>
            </div>
            <code class={ui.code}>{gcloud || 'Selecione um tópico.'}</code>
          </div>
        </div>
      </div>

      <div class={styles.side}>
        <div class="{ui.panel} {styles.mainCol}">
          <div class={styles.inspectorHead}>
            <div class={styles.inspectorTitle}>
              <div class={styles.inspectorIcon}>
                <span class="material-symbols-outlined">insights</span>
              </div>
              <div>
                <span class="{ui.labelSm} {ui.muted} {ui.upper} {ui.block}">Inspeção Ativa</span>
                <span class="{ui.labelMd} {ui.semibold} {ui.truncate} {ui.block}">{selected?.id ?? '—'}</span>
              </div>
            </div>
            <span class="{ui.chip} {ui.chipHigh} {ui.toneSecondary}">LIVE STREAM</span>
          </div>
          <div class={styles.inspectorBody}>
            <div>
              <div class={styles.sectionHead}>
                <span class="{ui.labelSm} {ui.muted} {ui.upper}">Throughput Últimos 60 min</span>
                <span class="{ui.labelSm} {ui.muted} {ui.semibold}">Pico: —</span>
              </div>
              <div class={styles.chart}>
                <div class={styles.chartGrid}>
                  <span></span>
                  <span></span>
                  <span></span>
                </div>
                <svg class={styles.chartSvg} preserveAspectRatio="none" viewBox="0 0 300 70">
                  <path d="M 0,68 L 300,68" fill="none" stroke="currentColor" stroke-width="2" opacity="0.4"></path>
                </svg>
                <div class="{styles.chartAxis} {ui.labelSm} {ui.faint}">
                  <span>-60m</span>
                  <span>-30m</span>
                  <span>Agora (0 msgs/sec)</span>
                </div>
              </div>
            </div>
            <div>
              <div class={styles.sectionHead}>
                <span class="{ui.labelSm} {ui.muted} {ui.upper}">Subscrições Vinculadas ({linked.length})</span>
              </div>
              <div class={styles.subList}>
                {#each linked as sub}
                  <div class={styles.subCard}>
                    <div class={styles.subMain}>
                      <div class={styles.subId}>
                        <span class={ui.dot}></span>
                        <span class="{ui.labelMd} {ui.semibold} {ui.truncate}" title={sub.id}>{sub.id}</span>
                      </div>
                      <span class="{ui.labelSm} {ui.muted}">Ack Deadline: {sub.ackDeadlineSeconds}s • PULL{sub.isObservation ? ' · tutu-*' : ''}</span>
                    </div>
                    <div class={styles.subStats}>
                      <span class="{ui.labelMd} {ui.semibold}">0 msgs</span>
                      <span class="{ui.labelSm} {ui.toneSecondary}">em dia</span>
                    </div>
                  </div>
                {:else}
                  <p class="{ui.labelSm} {ui.muted}">Nenhuma subscription neste tópico.</p>
                {/each}
              </div>
            </div>
            <div>
              <div class={styles.sectionHead}>
                <span class="{ui.labelSm} {ui.muted} {ui.upper}">Último Payload Publicado</span>
                <span class="{ui.labelSm} {ui.faint}">—</span>
              </div>
              <div class="{styles.payload} {ui.labelSm} {ui.muted}">
                Sem telemetria de payload no emulador.
              </div>
            </div>
            <div class={styles.dlq}>
              <div class={styles.dlqLabel}>
                <span class="material-symbols-outlined">report</span>
                <div>
                  <span class="{ui.labelSm} {ui.ink} {ui.semibold} {ui.block}">Dead Letter Topic</span>
                  <span class="{ui.labelSm} {ui.muted}">Desabilitado</span>
                </div>
              </div>
              <span class="{ui.chip} {ui.chipHigh} {ui.semibold}">—</span>
            </div>
            <div class={styles.inspectorActions}>
              <button
                class={styles.publish}
                type="button"
                disabled={!selected}
                onclick={() => onpublish(selected.id)}
              >
                <span class="material-symbols-outlined {ui.icon16}">send</span>
                <span>Publicar Teste</span>
              </button>
              <button class={styles.purge} title="Purgar não está na v1" type="button" disabled>
                <span class="material-symbols-outlined {ui.icon16}">delete_sweep</span>
                <span>Purgar</span>
              </button>
            </div>
            {#if resource}
              <span class="{ui.labelSm} {ui.faint} {ui.truncate}">{resource}</span>
            {/if}
          </div>
        </div>
      </div>
    </div>
  </div>
</main>
