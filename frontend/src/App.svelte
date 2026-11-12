<script>
  import { onMount } from 'svelte';
  import { api, listenCatalog } from './api.js';
  import ui from './ui.module.css';
  import styles from './App.module.css';
  import Shell from './Shell.svelte';
  import Topics from './pages/Topics.svelte';
  import Subscriptions from './pages/Subscriptions.svelte';
  import Messages from './pages/Messages.svelte';
  import Overview from './pages/Overview.svelte';
  import Dlq from './pages/Dlq.svelte';
  import Settings from './pages/Settings.svelte';
  import Guide from './pages/Guide.svelte';
  import Tutorial from './pages/Tutorial.svelte';

  let page = $state('topics');
  let health = $state({ online: false, projectId: 'pedidos-local', hostPort: '8085', error: null });
  let topics = $state([]);
  let subscriptions = $state([]);
  let notice = $state('');
  let filter = $state('');
  let startedAt = $state(Date.now());
  let now = $state(Date.now());
  let dialog = $state(null);
  let topicId = $state('');
  let subId = $state('');
  let subTopicId = $state('');
  let messageTopic = $state('');
  let openPublisher = $state(false);
  let busy = $state(false);
  let overview = $state(null);
  let dlq = $state([]);
  let settings = $state(null);
  let logs = $state({ lines: [], paused: false });

  const uptime = $derived(formatUptime(now - startedAt));

  async function refreshOps() {
    try {
      overview = await api('/api/overview');
      const dead = await api('/api/dlq');
      dlq = dead.messages ?? [];
      settings = await api('/api/settings');
      logs = await api('/api/logs');
    } catch (err) {
      notice = err.message;
    }
  }

  onMount(() => {
    const tick = setInterval(() => (now = Date.now()), 1000);
    const ops = setInterval(refreshOps, 2000);
    refreshOps();
    const stop = listenCatalog((type, data) => {
      if (type === 'snapshot') {
        health = data.health ?? health;
        topics = data.topics ?? [];
        subscriptions = data.subscriptions ?? [];
        return;
      }
      if (type === 'health') health = data;
      if (type === 'topics') topics = data.topics ?? [];
      if (type === 'subscriptions') subscriptions = data.subscriptions ?? [];
    });
    return () => {
      clearInterval(tick);
      clearInterval(ops);
      stop();
    };
  });

  function formatUptime(ms) {
    const s = Math.max(0, Math.floor(ms / 1000));
    const h = String(Math.floor(s / 3600)).padStart(2, '0');
    const m = String(Math.floor((s % 3600) / 60)).padStart(2, '0');
    const sec = String(s % 60).padStart(2, '0');
    return `${h}h:${m}m:${sec}s`;
  }

  function goMessages(id, publish) {
    messageTopic = id ?? topics[0]?.id ?? '';
    openPublisher = !!publish;
    page = 'messages';
    dialog = null;
  }

  function openTopicDialog() {
    topicId = '';
    dialog = 'topic';
  }

  function openSubDialog(id) {
    subId = '';
    subTopicId = id ?? topics[0]?.id ?? '';
    dialog = 'sub';
  }

  async function createTopic() {
    busy = true;
    notice = '';
    try {
      await api('/api/topics', { method: 'POST', body: JSON.stringify({ id: topicId }) });
      dialog = null;
    } catch (err) {
      notice = err.message;
    } finally {
      busy = false;
    }
  }

  async function createSub() {
    busy = true;
    notice = '';
    try {
      await api('/api/subscriptions', {
        method: 'POST',
        body: JSON.stringify({ id: subId, topicId: subTopicId }),
      });
      dialog = null;
    } catch (err) {
      notice = err.message;
    } finally {
      busy = false;
    }
  }

  function pullFromSub(sub) {
    goMessages(sub.topicId, false);
  }

  async function removeTopic(id) {
    if (!confirm(`Apagar o tópico ${id}?`)) return;
    try {
      await api(`/api/topics/${encodeURIComponent(id)}`, { method: 'DELETE' });
    } catch (err) {
      notice = err.message;
    }
  }

  async function removeSub(id) {
    if (!confirm(`Apagar a subscription ${id}?`)) return;
    try {
      await api(`/api/subscriptions/${encodeURIComponent(id)}`, { method: 'DELETE' });
    } catch (err) {
      notice = err.message;
    }
  }

  async function clearAll() {
    if (!confirm('Apagar todos os tópicos do emulador?')) return;
    try {
      await api('/api/admin/clear', { method: 'POST' });
      notice = 'Estado limpo';
    } catch (err) {
      notice = err.message;
    }
  }

  async function replay(id) {
    try {
      await api(`/api/dlq/${encodeURIComponent(id)}/replay`, { method: 'POST' });
      await refreshOps();
      notice = 'Replay publicado';
    } catch (err) {
      notice = err.message;
    }
  }

  async function purgeDlq() {
    await api('/api/dlq/purge', { method: 'POST' });
    await refreshOps();
  }

  async function exportDlq() {
    const res = await fetch('/api/dlq/export');
    const text = await res.text();
    await navigator.clipboard.writeText(text);
    notice = 'NDJSON copiado';
  }

  async function saveSettings(body) {
    await api('/api/settings', { method: 'PUT', body: JSON.stringify(body) });
    await refreshOps();
  }

  async function exportCatalog() {
    const catalog = await api('/api/catalog');
    const blob = new Blob([JSON.stringify(catalog, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'catalogo.json';
    link.click();
    URL.revokeObjectURL(url);
  }

  async function importCatalog(file) {
    const catalog = JSON.parse(await file.text());
    await api('/api/catalog', { method: 'POST', body: JSON.stringify(catalog) });
    notice = 'Catálogo importado';
  }
</script>

<Shell
  {page}
  {health}
  {uptime}
  {notice}
  retained={overview?.inflight ?? 0}
  backlog={overview?.inflight ?? 0}
  bind:filter
  onnavigate={(id) => {
    if (id === 'messages') openPublisher = false;
    page = id;
  }}
  onnewtopic={openTopicDialog}
  onnewsub={() => openSubDialog()}
  onpublish={() => goMessages(topics[0]?.id, true)}
  onclear={clearAll}
>
  {#if page === 'overview'}
    <Overview {overview} logs={logs.lines} onopen={() => (page = 'topics')} />
  {:else if page === 'topics'}
    <Topics {topics} {subscriptions} {health} {filter} oncreate={openTopicDialog} onpublish={(id) => goMessages(id, true)} onnewsub={openSubDialog} ondelete={removeTopic} />
  {:else if page === 'subscriptions'}
    <Subscriptions {topics} {subscriptions} {health} {filter} bind:notice oncreate={() => openSubDialog()} onpull={pullFromSub} ondelete={removeSub} />
  {:else if page === 'messages'}
    <Messages {topics} {subscriptions} {health} {filter} bind:notice initialTopic={messageTopic} {openPublisher} />
  {:else if page === 'dlq'}
    <Dlq messages={dlq} onreplay={replay} onpurge={purgeDlq} onexport={exportDlq} />
  {:else if page === 'settings'}
    <Settings {settings} {logs} onsave={saveSettings} onpause={async () => { await api('/api/logs/pause', { method: 'POST' }); await refreshOps(); }} onclearlogs={async () => { await api('/api/logs/clear', { method: 'POST' }); await refreshOps(); }} onclear={clearAll} onexport={exportCatalog} onimport={importCatalog} />
  {:else if page === 'guide'}
    <Guide onnavigate={(id) => (page = id)} />
  {:else if page === 'tutorial'}
    <Tutorial ontry={() => goMessages(topics[0]?.id, true)} />
  {/if}
</Shell>

{#if dialog}
  <div class={styles.backdrop}>
    <form
      class={styles.dialog}
      onsubmit={(e) => { e.preventDefault(); dialog === 'topic' ? createTopic() : createSub(); }}
    >
      <div class={styles.dialogHead}>
        <h2 class={ui.headlineLg}>{dialog === 'topic' ? 'Criar Tópico' : 'Criar Assinatura'}</h2>
        <button class={styles.close} type="button" onclick={() => (dialog = null)}>
          <span class="material-symbols-outlined {ui.icon18}">close</span>
        </button>
      </div>
      {#if dialog === 'topic'}
        <input class={styles.input} bind:value={topicId} placeholder="id do tópico" required />
      {:else}
        <input class={styles.input} bind:value={subId} placeholder="id da subscription" required />
        <select class={styles.input} bind:value={subTopicId} required>
          <option value="">tópico</option>
          {#each topics as topic}
            <option value={topic.id}>{topic.id}</option>
          {/each}
        </select>
      {/if}
      <div class={styles.actions}>
        <button class={ui.secondaryButton} type="button" onclick={() => (dialog = null)}>Cancelar</button>
        <button class={ui.primaryButton} type="submit" disabled={busy}>Criar</button>
      </div>
    </form>
  </div>
{/if}
