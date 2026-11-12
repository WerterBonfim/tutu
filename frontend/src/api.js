export async function api(path, options = {}) {
  const res = await fetch(path, {
    headers: { 'Content-Type': 'application/json', ...(options.headers ?? {}) },
    ...options,
  });
  if (res.status === 204) {
    return null;
  }
  const body = await res.json().catch(() => ({}));
  if (!res.ok) {
    throw new Error(body.message || body.error || res.statusText);
  }
  return body;
}

export function listenCatalog(onEvent) {
  const source = new EventSource('/events');
  for (const type of ['snapshot', 'health', 'topics', 'subscriptions']) {
    source.addEventListener(type, (event) => {
      onEvent(type, JSON.parse(event.data));
    });
  }
  source.onerror = () => onEvent('health', {
    online: false,
    projectId: 'pedidos-local',
    hostPort: '8085',
    error: 'stream de catálogo desconectado',
  });
  return () => source.close();
}
