<script>
  import ui from '../ui.module.css';
  import styles from './screen.module.css';

  let { overview, logs, onopen } = $props();

  const series = $derived(overview?.publishSeries ?? []);
  const acks = $derived(overview?.ackSeries ?? []);
  const max = $derived(Math.max(1, ...series, ...acks));

  function path(values) {
    if (!values.length) return '';
    return values.map((value, i) => {
      const x = (i / Math.max(1, values.length - 1)) * 300;
      const y = 70 - (value / max) * 60;
      return `${i === 0 ? 'M' : 'L'}${x.toFixed(1)},${y.toFixed(1)}`;
    }).join(' ');
  }

  const memory = $derived(overview ? `${Math.round(overview.workingSetBytes / 1048576)} MB` : '—');
</script>

<main class={ui.page}>
  <div class={styles.stack}>
    <div class={styles.head}>
      <div>
        <h1 class={ui.headlineXl}>Overview & Health</h1>
        <p class={ui.bodySm}>Contagens deste console. A outra aplicação não entra nesses números.</p>
      </div>
    </div>
    <div class={styles.metrics}>
      <article class={ui.metricCard}>
        <span class="{ui.labelSm} {ui.upper} {ui.muted}">Publicadas</span>
        <strong class={ui.headlineXl}>{overview?.published ?? 0}</strong>
        <span class="{ui.labelSm} {ui.muted}">por este console</span>
      </article>
      <article class={ui.metricCard}>
        <span class="{ui.labelSm} {ui.upper} {ui.muted}">Sem ack</span>
        <strong class={ui.headlineXl}>{overview?.inflight ?? 0}</strong>
        <span class="{ui.labelSm} {ui.muted}">{overview?.subscriptionCount ?? 0} subscriptions</span>
      </article>
      <article class={ui.metricCard}>
        <span class="{ui.labelSm} {ui.upper} {ui.muted}">Memória da API</span>
        <strong class={ui.headlineXl}>{memory}</strong>
        <span class="{ui.labelSm} {ui.muted}">pid {overview?.pid ?? '—'}</span>
      </article>
      <article class={ui.metricCard}>
        <span class="{ui.labelSm} {ui.upper} {ui.muted}">Nack / DLQ</span>
        <strong class={ui.headlineXl}>{overview?.nacked ?? 0}</strong>
        <span class="{ui.labelSm} {ui.toneTertiary}">{overview?.deadLettered ?? 0} em quarentena</span>
      </article>
    </div>
    <section class={styles.chart}>
      <div class={styles.head}>
        <h2 class={ui.headlineMd}>Throughput deste console</h2>
        <div class="{styles.legend} {ui.labelSm}">
          <span class={ui.tonePrimary}>publicadas</span>
          <span class={ui.toneSecondary}>ack</span>
        </div>
      </div>
      <svg viewBox="0 0 300 80" preserveAspectRatio="none">
        <path d={path(series)} fill="none" stroke="currentColor" stroke-width="2"></path>
        <path class={styles.seriesAck} d={path(acks)} fill="none" stroke="currentColor" stroke-width="2"></path>
      </svg>
    </section>
    <section class="{ui.card}">
      <h2 class={ui.headlineMd}>Recursos</h2>
      <p class={ui.bodySm}>{overview?.topicCount ?? 0} tópicos · {overview?.subscriptionCount ?? 0} subscriptions · {overview?.acked ?? 0} acks · {overview?.pulled ?? 0} pulls</p>
      <button class="{ui.primaryButton}" type="button" onclick={onopen}>Abrir tópicos</button>
    </section>
    <pre class={styles.log}>{(logs ?? []).slice(-8).join('\n') || 'Sem logs ainda.'}</pre>
  </div>
</main>
