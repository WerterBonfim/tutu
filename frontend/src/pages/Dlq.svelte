<script>
  import ui from '../ui.module.css';
  import styles from './screen.module.css';

  let { messages = [], onreplay, onpurge, onexport } = $props();
  let selectedId = $state('');
  const selected = $derived(messages.find((item) => item.id === selectedId) ?? messages[0] ?? null);
</script>

<main class={ui.page}>
  <div class={styles.stack}>
    <div class={styles.head}>
      <div>
        <h1 class={ui.headlineXl}>Dead Letter Queues (DLQ)</h1>
        <p class={ui.bodySm}>Quarentena do quinto nack feito por este console em uma subscription tutu-*.</p>
      </div>
      <div class={styles.actions}>
        <button class={ui.primaryButton} type="button" onclick={() => selected && onreplay(selected.id)} disabled={!selected}>Replay</button>
        <button class={ui.secondaryButton} type="button" onclick={onexport}>Exportar NDJSON</button>
        <button class={ui.secondaryButton} type="button" onclick={onpurge} disabled={!messages.length}>Purgar quarentena</button>
      </div>
    </div>
    <div class={styles.metrics}>
      <article class={ui.metricCard}>
        <span class="{ui.labelSm} {ui.upper} {ui.muted}">Na quarentena</span>
        <strong class={ui.headlineXl}>{messages.length}</strong>
      </article>
    </div>
    <div class={styles.split}>
      <div class={styles.tableWrap}>
        <table class={ui.table}>
          <thead>
            <tr class={ui.headRow}>
              <th class={ui.headCell}>Mensagem</th>
              <th class={ui.headCell}>Tópico</th>
              <th class={ui.headCell}>Tentativas</th>
            </tr>
          </thead>
          <tbody>
            {#each messages as item}
              <tr class="{ui.dataRow} {styles.pick} {selected?.id === item.id ? styles.pickOn : ''}" onclick={() => (selectedId = item.id)}>
                <td class="{ui.bodyCell} {ui.labelSm}">{item.id}</td>
                <td class={ui.bodyCell}>{item.topicId}</td>
                <td class={ui.bodyCell}>{item.attempts}</td>
              </tr>
            {:else}
              <tr><td class="{ui.bodyCellWide} {ui.bodySm}" colspan="3">Nenhuma mensagem em quarentena.</td></tr>
            {/each}
          </tbody>
        </table>
      </div>
      <aside class={ui.card}>
        {#if selected}
          <h2 class="{ui.headlineMd} {ui.truncate}">{selected.id}</h2>
          <p class="{ui.labelSm} {ui.muted}">{selected.reason} · {selected.at}</p>
          <pre class={styles.log}>{selected.data}</pre>
          <button class={ui.primaryButton} type="button" onclick={() => onreplay(selected.id)}>Reenviar para {selected.topicId}</button>
        {:else}
          <p class={ui.bodySm}>Selecione uma mensagem.</p>
        {/if}
      </aside>
    </div>
  </div>
</main>
