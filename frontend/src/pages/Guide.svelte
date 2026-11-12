<script>
  import ui from '../ui.module.css';
  import styles from './screen.module.css';

  let { onnavigate } = $props();
</script>

<main class={ui.page}>
  <div class={styles.prose}>
    <div>
      <h1 class={ui.headlineXl}>Guia da Interface</h1>
      <p class={ui.bodySm}>O que cada número desta console significa. Nada aqui é telemetria de exemplo.</p>
    </div>
    <section>
      <h2 class={ui.headlineMd}>Top bar</h2>
      <p class={ui.bodySm}>Porta é a do emulador. Retidas são mensagens que este console puxou e ainda não confirmou. Backlog usa o mesmo inflight.</p>
    </section>
    <section>
      <h2 class={ui.headlineMd}>Overview</h2>
      <p class={ui.bodySm}>Publicadas, pulls, acks e nacks contam só o que passou por esta API. A memória é o working set deste processo, não a JVM do emulador.</p>
      <button class={ui.secondaryButton} type="button" onclick={() => onnavigate('overview')}>Abrir Overview</button>
    </section>
    <section>
      <h2 class={ui.headlineMd}>Tópicos, subscriptions e mensagens</h2>
      <p class={ui.bodySm}>Pull, ack e nack só em subscriptions tutu-*. Apagar tópico remove as subscriptions dele no emulador.</p>
    </section>
    <section>
      <h2 class={ui.headlineMd}>DLQ</h2>
      <p class={ui.bodySm}>No quinto nack de uma mensagem puxada aqui, uma cópia vai para tutu-dlq-&lt;tópico&gt; e a original é confirmada. Replay publica de novo no tópico de origem.</p>
      <button class={ui.secondaryButton} type="button" onclick={() => onnavigate('dlq')}>Abrir DLQ</button>
    </section>
    <section>
      <h2 class={ui.headlineMd}>Settings</h2>
      <p class={ui.bodySm}>Caos atrasa publish e pull deste console. JSON estrito recusa corpo que não é JSON. Limpar estado apaga os tópicos do emulador.</p>
    </section>
  </div>
</main>
