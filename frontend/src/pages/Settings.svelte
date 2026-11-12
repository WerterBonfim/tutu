<script>
  import ui from '../ui.module.css';
  import styles from './screen.module.css';

  let { settings, logs, onsave, onpause, onclearlogs, onclear, onexport, onimport } = $props();
  let chaos = $state(0);
  let strict = $state(false);
  let file = $state(null);
  let ready = $state(false);

  $effect(() => {
    if (!settings || ready) return;
    chaos = settings.chaosMs ?? 0;
    strict = settings.strictJson ?? false;
    ready = true;
  });
</script>

<main class={ui.page}>
  <div class={styles.stack}>
    <div class={styles.head}>
      <div>
        <h1 class={ui.headlineXl}>Configurações do Emulador</h1>
        <p class={ui.bodySm}>Host e projeto vêm do processo. Caos e JSON estrito valem só para este console.</p>
      </div>
      <button class={ui.primaryButton} type="button" onclick={() => onsave({ chaosMs: Number(chaos) || 0, strictJson: strict })}>Salvar</button>
    </div>
    <section class={ui.card}>
      <h2 class={ui.headlineMd}>Rede</h2>
      <p class={ui.bodySm}>Projeto {settings?.projectId} · porta {settings?.hostPort} · pid {settings?.pid} · memória {settings ? Math.round(settings.workingSetBytes / 1048576) : '—'} MB</p>
      <p class="{ui.labelSm} {ui.muted}">Restart do container do emulador não é feito daqui.</p>
    </section>
    <section class={ui.card}>
      <h2 class={ui.headlineMd}>Caos e schema</h2>
      <div class={styles.fieldRow}>
        <label class={ui.bodySm} for="chaos">Atraso de publish/pull (ms)</label>
        <input id="chaos" class={styles.narrow} type="number" min="0" max="5000" bind:value={chaos} />
      </div>
      <label class="{styles.fieldRow} {ui.bodySm}">
        <input type="checkbox" bind:checked={strict} />
        Recusar publish que não seja JSON
      </label>
    </section>
    <section class={ui.card}>
      <div class={styles.head}>
        <h2 class={ui.headlineMd}>Logs deste processo</h2>
        <div class={styles.actions}>
          <button class={ui.secondaryButton} type="button" onclick={onpause}>{logs?.paused ? 'Retomar' : 'Pausar'}</button>
          <button class={ui.secondaryButton} type="button" onclick={onclearlogs}>Limpar</button>
        </div>
      </div>
      <pre class={styles.log}>{(logs?.lines ?? []).join('\n') || 'Sem logs.'}</pre>
    </section>
    <section class={ui.card}>
      <h2 class={ui.headlineMd}>Catálogo</h2>
      <div class={styles.actions}>
        <button class={ui.secondaryButton} type="button" onclick={onexport}>Exportar JSON</button>
        <input type="file" accept="application/json" onchange={(e) => (file = e.currentTarget.files?.[0] ?? null)} />
        <button class={ui.secondaryButton} type="button" disabled={!file} onclick={() => onimport(file)}>Importar</button>
        <button class="{ui.secondaryButton} {ui.toneTertiary}" type="button" onclick={onclear}>Limpar estado</button>
      </div>
    </section>
  </div>
</main>
