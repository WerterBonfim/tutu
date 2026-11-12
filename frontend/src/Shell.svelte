<script>
  import ui from './ui.module.css';
  import styles from './Shell.module.css';

  let {
    page,
    health,
    uptime,
    filter = $bindable(''),
    notice = '',
    onnavigate,
    retained = 0,
    backlog = 0,
    onnewtopic,
    onnewsub,
    onpublish,
    onclear,
    children,
  } = $props();

  const nav = [
    { id: 'overview', icon: 'monitoring', label: 'Overview & Health' },
    { id: 'topics', icon: 'topic', label: 'Topics' },
    { id: 'subscriptions', icon: 'mark_email_unread', label: 'Subscriptions' },
    { id: 'messages', icon: 'send', label: 'Message Browser & Pub' },
    { id: 'dlq', icon: 'report', label: 'Dead Letter Queues' },
    { id: 'settings', icon: 'tune', label: 'Emulator Settings' },
    { id: 'guide', icon: 'menu_book', label: 'Guia da Interface' },
    { id: 'tutorial', icon: 'school', label: 'Tutorial & Arquitetura' },
  ];

  function linkClass(id) {
    return page === id ? styles.navOn : styles.navLink;
  }
</script>

<aside class={styles.rail}>
  <div>
    <div class={styles.brand}>
      <div class={styles.brandRow}>
        <div class={styles.logo}>
          <span class="material-symbols-outlined">hub</span>
        </div>
        <div class={styles.brandText}>
          <span class="{ui.headlineMd} {ui.truncate}">GCP Pub/Sub</span>
          <span class="{ui.bodySm} {styles.brandSub}">Emulator Suite</span>
        </div>
      </div>
      <div class={styles.status}>
        <span class="{ui.dot} {ui.dotLg} {health.online ? ui.pulse : ui.dotTertiary}"></span>
        <span class="{ui.labelSm} {health.online ? ui.toneSecondary : ui.toneTertiary}">localhost:{health.hostPort}</span>
        <span class="{ui.labelSm} {ui.bold} {styles.statusLabel} {health.online ? ui.toneSecondary : ui.toneTertiary}">{health.online ? 'ONLINE' : 'OFFLINE'}</span>
      </div>
    </div>
    <div class={styles.projectWrap}>
      <div class={styles.project}>
        <div class={styles.projectId}>
          <span class="material-symbols-outlined {ui.icon18} {ui.tonePrimary}">layers</span>
          <div class={styles.brandText}>
            <span class="{ui.labelSm} {ui.upper} {ui.muted}">Simulated Project</span>
            <span class="{ui.labelMd} {ui.semibold} {ui.truncate} {ui.ink}">{health.projectId}</span>
          </div>
        </div>
        <span class="material-symbols-outlined {ui.icon18} {ui.muted}">unfold_more</span>
      </div>
    </div>
    <nav class={styles.nav}>
      {#each nav as item}
        <button class={linkClass(item.id)} type="button" onclick={() => onnavigate(item.id)}>
          <span class="material-symbols-outlined {ui.icon18}">{item.icon}</span>
          <span class={ui.bodyMd}>{item.label}</span>
        </button>
      {/each}
    </nav>
  </div>
  <div class={styles.foot}>
    {#if health.error}
      <p class="{ui.labelSm} {ui.toneTertiary} {styles.note}">{health.error}</p>
    {/if}
    {#if notice}
      <p class="{ui.labelSm} {ui.toneSecondary} {styles.note}">{notice}</p>
    {/if}
    <div class={styles.daemon}>
      <div>
        <span class="{ui.labelSm} {ui.muted}">DAEMON RUNTIME</span>
        <span class="{ui.labelMd} {ui.toneSecondary} {ui.block}">{uptime}</span>
      </div>
      <button
        class={styles.restart}
        title="Restart do emulador não está na v1"
        type="button"
        disabled
      >
        <span class="material-symbols-outlined {ui.icon18}">restart_alt</span>
      </button>
    </div>
  </div>
</aside>

<div class={styles.content}>
  <header class={styles.header}>
    <div class={styles.searchWrap}>
      <div class={styles.search}>
        <span class="material-symbols-outlined {styles.searchIcon}">search</span>
        <input
          class="{ui.field} {ui.fieldBody} {styles.searchInput}"
          placeholder="Filtrar por tópico, subscription ID, attributes ou message ID..."
          type="search"
          bind:value={filter}
        />
      </div>
    </div>
    <div class={styles.headerActions}>
      <div class={styles.stats}>
        <div class={styles.stat}>
          <span class="{ui.labelSm} {ui.muted}">Porta:</span>
          <span class="{ui.labelSm} {ui.tonePrimary} {ui.semibold}">{health.hostPort}</span>
        </div>
        <div class={styles.stat}>
          <span class="{ui.labelSm} {ui.muted}">Retidas:</span>
          <span class="{ui.labelSm} {ui.ink} {ui.semibold}">{retained}</span>
        </div>
        <div class={styles.stat}>
          <span class="{ui.labelSm} {ui.muted}">Backlog:</span>
          <span class="{ui.labelSm} {ui.toneSecondary} {ui.semibold}">{backlog} msgs</span>
        </div>
      </div>
      <div class={styles.headerButtons}>
        <button class={ui.secondaryButton} type="button" onclick={onnewtopic}>
          <span class="material-symbols-outlined {ui.icon16}">add</span>Novo Tópico
        </button>
        <button class={ui.secondaryButton} type="button" onclick={onnewsub}>
          <span class="material-symbols-outlined {ui.icon16}">bookmark_add</span>Nova Subscription
        </button>
        <button class={styles.publish} type="button" onclick={onpublish}>
          <span class="material-symbols-outlined {ui.icon16}">send</span>Publicar Mensagem
        </button>
      </div>
      <div class={styles.tools}>
        <button class={styles.tool} title="Apagar todos os tópicos do emulador" type="button" onclick={onclear}>
          <span class="material-symbols-outlined {ui.icon18}">delete_sweep</span>
        </button>
        <button class={styles.tool} title="Tema claro não está na v1" type="button" disabled>
          <span class="material-symbols-outlined {ui.icon18}">dark_mode</span>
        </button>
        <div class={styles.avatar}>
          <span class="material-symbols-outlined">person</span>
        </div>
      </div>
    </div>
  </header>
  {@render children()}
</div>
