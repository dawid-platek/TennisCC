'use strict';

const { test, before, after } = require('node:test');
const assert = require('node:assert/strict');
const { openDatabase } = require('../src/db');
const { createApp } = require('../src/app');

let server;
let base;

before(async () => {
  const app = createApp({ db: openDatabase(':memory:') });
  await new Promise((resolve) => {
    server = app.listen(0, resolve);
  });
  base = `http://127.0.0.1:${server.address().port}`;
});

after(() => server.close());

// Tiny browser: keeps the session cookie and the current CSRF token.
function client() {
  let cookie = '';
  let csrf = '';
  async function request(method, path, form) {
    const res = await fetch(base + path, {
      method,
      redirect: 'manual',
      headers: {
        cookie,
        ...(form ? { 'content-type': 'application/x-www-form-urlencoded' } : {}),
      },
      body: form ? new URLSearchParams({ _csrf: csrf, ...form }).toString() : undefined,
    });
    const set = res.headers.get('set-cookie');
    if (set) cookie = set.split(';')[0];
    const body = await res.text();
    const m = body.match(/name="_csrf" value="([^"]+)"/);
    if (m) csrf = m[1];
    return { status: res.status, location: res.headers.get('location'), body };
  }
  return {
    get: (path) => request('GET', path),
    post: async (path, form = {}) => {
      if (!csrf) await request('GET', '/login');
      return request('POST', path, form);
    },
    async login(email, password) {
      await request('GET', '/login');
      return request('POST', '/login', { email, password });
    },
  };
}

const coach = client();
const anna = client();
const piotr = client();
const ids = {};

test('first run: setup creates the coach and is then locked', async () => {
  const r = await coach.get('/');
  assert.equal(r.location, '/setup');
  await coach.get('/setup');
  const created = await coach.post('/setup', { name: 'Trener', email: 'coach@test.pl', password: 'tajnehaslo' });
  assert.equal(created.status, 302);
  assert.equal(created.location, '/players');

  const again = await client().get('/setup');
  assert.equal(again.location, '/login');
});

test('coach adds players and parent accounts linked to them', async () => {
  await coach.get('/players');
  for (const [first, last] of [['Zosia', 'Nowak'], ['Kuba', 'Nowak'], ['Maja', 'Wiśniewska']]) {
    const r = await coach.post('/players', { first_name: first, last_name: last, birth_date: '2015-05-05' });
    assert.equal(r.status, 302);
    ids[first] = Number(r.location.split('/').pop());
  }
  await coach.get('/parents');
  let r = await coach.post('/parents', { name: 'Anna Nowak', email: 'anna@test.pl', password: 'annahaslo' });
  assert.equal(r.status, 302);
  r = await coach.post('/parents', { name: 'Piotr W', email: 'piotr@test.pl', password: 'piotrhaslo', player_ids: String(ids.Maja) });
  assert.equal(r.status, 302);

  // Link Anna to both of her children from the player pages.
  const list = await coach.get('/parents');
  const parentId = list.body.split('Anna Nowak')[1].match(/\/parents\/(\d+)\/reset-password/)[1];
  for (const kid of ['Zosia', 'Kuba']) {
    await coach.get(`/players/${ids[kid]}`);
    r = await coach.post(`/players/${ids[kid]}/parents`, { parent_id: parentId });
    assert.equal(r.status, 302);
  }

  await coach.get(`/players/${ids.Zosia}`);
  r = await coach.post(`/players/${ids.Zosia}/assessments`, { date: '2026-09-01', forehand: '6', serve: '4', comment: 'Dobry trening' });
  assert.equal(r.status, 302);
  await coach.post(`/players/${ids.Zosia}/edit`, { first_name: 'Zosia', last_name: 'Nowak', private_notes: 'SEKRET-TRENERA' });
});

test('parent with several children sees exactly their own children', async () => {
  const r = await anna.login('anna@test.pl', 'annahaslo');
  assert.equal(r.status, 302);
  const my = await anna.get('/my');
  assert.equal(my.status, 200);
  assert.match(my.body, /Zosia/);
  assert.match(my.body, /Kuba/);
  assert.doesNotMatch(my.body, /Maja/);

  const zosia = await anna.get(`/players/${ids.Zosia}`);
  assert.equal(zosia.status, 200);
  assert.match(zosia.body, /Forhend/);
  assert.doesNotMatch(zosia.body, /SEKRET-TRENERA/, 'private coach notes must be hidden from parents');
  assert.doesNotMatch(zosia.body, /Nowa ocena/, 'parents get a read-only view');
});

test('parent cannot open another family\'s child', async () => {
  const r = await anna.get(`/players/${ids.Maja}`);
  assert.equal(r.status, 404);
  assert.doesNotMatch(r.body, /Wiśniewska/);
  assert.equal((await anna.get('/players/999999')).status, 404);
});

test('parent cannot use coach-only pages or actions', async () => {
  assert.equal((await anna.get('/players')).status, 403);
  assert.equal((await anna.get('/parents')).status, 403);
  await anna.get(`/players/${ids.Zosia}`);
  const r = await anna.post(`/players/${ids.Zosia}/assessments`, { date: '2026-10-01', forehand: '10' });
  assert.equal(r.status, 403);
  const del = await anna.post(`/players/${ids.Maja}/delete`);
  assert.equal(del.status, 403);
});

test('parent with one child is taken straight to that child', async () => {
  await piotr.login('piotr@test.pl', 'piotrhaslo');
  const r = await piotr.get('/my');
  assert.equal(r.location, `/players/${ids.Maja}`);
  assert.equal((await piotr.get(`/players/${ids.Zosia}`)).status, 404);
});

test('anonymous users are sent to login, bad credentials rejected, CSRF enforced', async () => {
  const anon = client();
  assert.equal((await anon.get(`/players/${ids.Zosia}`)).location, '/login');
  assert.equal((await anon.login('anna@test.pl', 'zlehaslo')).status, 401);

  const res = await fetch(`${base}/login`, {
    method: 'POST',
    headers: { 'content-type': 'application/x-www-form-urlencoded' },
    body: 'email=anna%40test.pl&password=annahaslo',
    redirect: 'manual',
  });
  assert.equal(res.status, 403);
});

test('PWA: manifest and service worker are served; data pages are not cached', async () => {
  const manifest = await fetch(`${base}/manifest.webmanifest`);
  assert.equal(manifest.status, 200);
  const m = JSON.parse(await manifest.text());
  assert.equal(m.display, 'standalone');
  assert.ok(m.icons.some((i) => i.sizes === '512x512'));

  const sw = await fetch(`${base}/sw.js`);
  assert.equal(sw.status, 200);
  assert.equal(sw.headers.get('cache-control'), 'no-cache');

  const page = await fetch(`${base}/login`);
  assert.match(page.headers.get('cache-control'), /no-store/);
});
