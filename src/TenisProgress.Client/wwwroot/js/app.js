// PWA: register the service worker (offline app shell; API data is never cached).
if ('serviceWorker' in navigator) {
  navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' }).catch(() => {});
}

// PWA: "Install app" banner (Chrome/Android/desktop) and a hint for iOS Safari.
// The banner markup lives in MainLayout.razor, which Blazor renders after startup,
// so look it up lazily.
(function installPrompt() {
  const standalone = window.matchMedia('(display-mode: standalone)').matches || navigator.standalone;
  if (standalone) return;
  try { if (localStorage.getItem('install-dismissed') === '1') return; } catch (e) {}

  const ios = /iphone|ipad|ipod/i.test(navigator.userAgent) ||
    (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
  let deferred = null;

  function show(text, withButton) {
    const box = document.getElementById('install');
    if (!box) return false;
    box.querySelector('.install-text').textContent = text;
    const btn = box.querySelector('.install-btn');
    btn.hidden = !withButton;
    btn.onclick = async () => {
      if (!deferred) return;
      deferred.prompt();
      await deferred.userChoice;
      deferred = null;
      box.hidden = true;
    };
    box.querySelector('.install-close').onclick = () => {
      box.hidden = true;
      try { localStorage.setItem('install-dismissed', '1'); } catch (e) {}
    };
    box.hidden = false;
    return true;
  }

  // Retry until Blazor has rendered the layout.
  function showWhenReady(text, withButton, tries = 40) {
    if (!show(text, withButton) && tries > 0) setTimeout(() => showWhenReady(text, withButton, tries - 1), 250);
  }

  window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    deferred = e;
    showWhenReady('Zainstaluj Tenis Progress na telefonie, aby mieć szybki dostęp.', true);
  });
  window.addEventListener('appinstalled', () => {
    const box = document.getElementById('install');
    if (box) box.hidden = true;
  });
  if (ios) showWhenReady('Dodaj aplikację do ekranu: stuknij „Udostępnij” ⬆️, potem „Do ekranu początkowego”.', false);
})();
