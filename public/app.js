// Ask for confirmation before submitting destructive forms (data-confirm="...").
document.addEventListener('submit', (e) => {
  const msg = e.target.getAttribute('data-confirm');
  if (msg && !window.confirm(msg)) e.preventDefault();
});

// Open the <details> section targeted by the URL hash after a redirect.
if (location.hash) {
  const el = document.querySelector(location.hash);
  if (el && el.tagName === 'DETAILS') el.open = true;
}

// PWA: register the service worker.
if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => navigator.serviceWorker.register('/sw.js').catch(() => {}));
}

// PWA: "Install app" button (Chrome/Android/desktop) and a hint for iOS Safari.
(function installPrompt() {
  const standalone = window.matchMedia('(display-mode: standalone)').matches || navigator.standalone;
  const box = document.getElementById('install');
  if (standalone || !box) return;
  let dismissed = false;
  try { dismissed = localStorage.getItem('install-dismissed') === '1'; } catch (e) {}
  if (dismissed) return;

  const text = box.querySelector('.install-text');
  const btn = box.querySelector('.install-btn');
  box.querySelector('.install-close').addEventListener('click', () => {
    box.hidden = true;
    try { localStorage.setItem('install-dismissed', '1'); } catch (e) {}
  });

  let deferred = null;
  window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    deferred = e;
    text.textContent = 'Zainstaluj Tenis Progress na telefonie, aby mieć szybki dostęp.';
    btn.hidden = false;
    box.hidden = false;
  });
  btn.addEventListener('click', async () => {
    if (!deferred) return;
    deferred.prompt();
    await deferred.userChoice;
    deferred = null;
    box.hidden = true;
  });
  window.addEventListener('appinstalled', () => { box.hidden = true; });

  const ios = /iphone|ipad|ipod/i.test(navigator.userAgent) ||
    (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
  if (ios) {
    text.textContent = 'Dodaj aplikację do ekranu: stuknij „Udostępnij” ⬆️, potem „Do ekranu początkowego”.';
    box.hidden = false;
  }
})();
