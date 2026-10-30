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
