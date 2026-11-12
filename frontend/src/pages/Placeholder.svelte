<script>
  import ui from '../ui.module.css';
  import styles from './Placeholder.module.css';

  let { name } = $props();
</script>

<main class={ui.page}>
  <div class={styles.card}>
    <div class={styles.titleRow}>
      <span class="material-symbols-outlined">construction</span>
      <h1 class={ui.headlineXl}>{name}</h1>
    </div>
    <p class={ui.bodySm}>
      Fora da v1. Não há HTML Stitch para esta tela. Use Topics, Subscriptions ou Message Browser.
    </p>
  </div>
</main>
