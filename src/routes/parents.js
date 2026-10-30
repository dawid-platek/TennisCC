'use strict';

const crypto = require('node:crypto');
const express = require('express');
const bcrypt = require('bcryptjs');
const { requireCoach, notFound, flash } = require('../middleware');
const { text, int } = require('../util');
const { MIN_PASSWORD } = require('./auth');

function randomPassword() {
  // Readable temporary password, e.g. "kX7m-p2Qa-9TzR".
  const raw = crypto.randomBytes(9).toString('base64').replace(/[+/=]/g, '').slice(0, 12);
  return raw.match(/.{1,4}/g).join('-');
}

module.exports = function parentRoutes(db) {
  const router = express.Router();
  router.use('/parents', requireCoach);

  const childrenOf = db.prepare(
    'SELECT p.id, p.first_name, p.last_name FROM players p JOIN player_parents pp ON pp.player_id = p.id WHERE pp.parent_id = ? ORDER BY p.first_name'
  );
  const getParent = db.prepare("SELECT id, name, email, phone FROM users WHERE id = ? AND role = 'parent'");

  router.get('/parents', (req, res) => {
    const parents = db
      .prepare("SELECT id, name, email, phone FROM users WHERE role = 'parent' ORDER BY name")
      .all()
      .map((p) => ({ ...p, children: childrenOf.all(p.id) }));
    const players = db.prepare('SELECT id, first_name, last_name FROM players ORDER BY last_name, first_name').all();
    res.render('parents', { title: 'Rodzice', parents, players, minPassword: MIN_PASSWORD });
  });

  router.post('/parents', (req, res) => {
    const name = text(req.body.name, 100);
    const email = text(req.body.email, 200);
    let password = String(req.body.password || '');
    if (!name || !email || !email.includes('@')) {
      flash(req, 'error', 'Podaj imię i nazwisko oraz poprawny adres e-mail rodzica.');
      return res.redirect('/parents');
    }
    if (db.prepare('SELECT 1 FROM users WHERE email = ?').get(email)) {
      flash(req, 'error', 'Konto z tym adresem e-mail już istnieje.');
      return res.redirect('/parents');
    }
    const generated = !password;
    if (generated) password = randomPassword();
    if (password.length < MIN_PASSWORD) {
      flash(req, 'error', `Hasło musi mieć co najmniej ${MIN_PASSWORD} znaków.`);
      return res.redirect('/parents');
    }
    const playerIds = [].concat(req.body.player_ids || []).map((v) => int(v, 1, Number.MAX_SAFE_INTEGER)).filter(Boolean);

    db.exec('BEGIN');
    try {
      const { lastInsertRowid } = db
        .prepare("INSERT INTO users (email, name, phone, password_hash, role) VALUES (?, ?, ?, ?, 'parent')")
        .run(email, name, text(req.body.phone, 40), bcrypt.hashSync(password, 12));
      const link = db.prepare('INSERT OR IGNORE INTO player_parents (player_id, parent_id) SELECT id, ? FROM players WHERE id = ?');
      for (const pid of playerIds) link.run(lastInsertRowid, pid);
      db.exec('COMMIT');
    } catch (err) {
      db.exec('ROLLBACK');
      throw err;
    }
    flash(
      req,
      'success',
      generated
        ? `Utworzono konto dla ${name}. Login: ${email}, hasło tymczasowe: ${password} — przekaż je rodzicowi (pokazujemy je tylko raz).`
        : `Utworzono konto dla ${name} (login: ${email}).`
    );
    res.redirect('/parents');
  });

  router.post('/parents/:id/reset-password', (req, res) => {
    const parent = getParent.get(int(req.params.id, 1, Number.MAX_SAFE_INTEGER));
    if (!parent) return notFound(res);
    const password = randomPassword();
    db.prepare('UPDATE users SET password_hash = ? WHERE id = ?').run(bcrypt.hashSync(password, 12), parent.id);
    // Log the parent out everywhere by dropping their sessions.
    db.prepare("DELETE FROM sessions WHERE json_extract(data, '$.userId') = ?").run(parent.id);
    flash(req, 'success', `Nowe hasło tymczasowe dla ${parent.name} (${parent.email}): ${password}`);
    res.redirect('/parents');
  });

  router.post('/parents/:id/delete', (req, res) => {
    const parent = getParent.get(int(req.params.id, 1, Number.MAX_SAFE_INTEGER));
    if (!parent) return notFound(res);
    db.prepare('DELETE FROM users WHERE id = ?').run(parent.id);
    db.prepare("DELETE FROM sessions WHERE json_extract(data, '$.userId') = ?").run(parent.id);
    flash(req, 'success', `Usunięto konto rodzica: ${parent.name}.`);
    res.redirect('/parents');
  });

  return router;
};
