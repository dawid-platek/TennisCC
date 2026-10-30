'use strict';

const express = require('express');
const bcrypt = require('bcryptjs');
const { requireLogin, flash, loginLimiter } = require('../middleware');
const { text } = require('../util');

const MIN_PASSWORD = 8;

module.exports = function authRoutes(db) {
  const router = express.Router();
  const limiter = loginLimiter();
  const userCount = () => db.prepare('SELECT COUNT(*) AS n FROM users').get().n;

  // Logs the user in, regenerating the session id to prevent fixation.
  function startSession(req, userId, cb) {
    req.session.regenerate((err) => {
      if (err) return cb(err);
      req.session.userId = userId;
      req.session.save(cb);
    });
  }

  router.get('/', (req, res) => {
    if (!req.user) return res.redirect(userCount() === 0 ? '/setup' : '/login');
    res.redirect(req.user.role === 'coach' ? '/players' : '/my');
  });

  // First-run setup: only available while there are no users at all.
  router.get('/setup', (req, res) => {
    if (userCount() > 0) return res.redirect('/login');
    res.render('setup', { title: 'Konfiguracja', values: {} });
  });

  router.post('/setup', (req, res, next) => {
    if (userCount() > 0) return res.redirect('/login');
    const name = text(req.body.name, 100);
    const email = text(req.body.email, 200);
    const password = String(req.body.password || '');
    if (!name || !email || !email.includes('@') || password.length < MIN_PASSWORD) {
      return res.status(400).render('setup', {
        title: 'Konfiguracja',
        values: { name, email },
        error: `Podaj imię, poprawny e-mail i hasło (min. ${MIN_PASSWORD} znaków).`,
      });
    }
    const hash = bcrypt.hashSync(password, 12);
    const { lastInsertRowid } = db
      .prepare("INSERT INTO users (email, name, password_hash, role) VALUES (?, ?, ?, 'coach')")
      .run(email, name, hash);
    startSession(req, Number(lastInsertRowid), (err) => (err ? next(err) : res.redirect('/players')));
  });

  router.get('/login', (req, res) => {
    if (req.user) return res.redirect('/');
    if (userCount() === 0) return res.redirect('/setup');
    res.render('login', { title: 'Logowanie', email: '' });
  });

  router.post('/login', (req, res, next) => {
    const email = text(req.body.email, 200) || '';
    const password = String(req.body.password || '');
    const key = `${req.ip}|${email.toLowerCase()}`;
    if (limiter.blocked(key)) {
      return res.status(429).render('login', {
        title: 'Logowanie',
        email,
        error: 'Zbyt wiele nieudanych prób. Spróbuj ponownie za kilkanaście minut.',
      });
    }
    const user = db.prepare('SELECT id, password_hash FROM users WHERE email = ?').get(email);
    if (!user || !bcrypt.compareSync(password, user.password_hash)) {
      limiter.fail(key);
      return res.status(401).render('login', { title: 'Logowanie', email, error: 'Nieprawidłowy e-mail lub hasło.' });
    }
    limiter.reset(key);
    startSession(req, user.id, (err) => (err ? next(err) : res.redirect('/')));
  });

  router.post('/logout', (req, res) => {
    req.session.destroy(() => {
      res.clearCookie('tenis.sid');
      res.redirect('/login');
    });
  });

  router.get('/account', requireLogin, (req, res) => {
    res.render('account', { title: 'Moje konto' });
  });

  router.post('/account/password', requireLogin, (req, res) => {
    const current = String(req.body.current || '');
    const next = String(req.body.password || '');
    const row = db.prepare('SELECT password_hash FROM users WHERE id = ?').get(req.user.id);
    if (!bcrypt.compareSync(current, row.password_hash)) {
      flash(req, 'error', 'Obecne hasło jest nieprawidłowe.');
    } else if (next.length < MIN_PASSWORD) {
      flash(req, 'error', `Nowe hasło musi mieć co najmniej ${MIN_PASSWORD} znaków.`);
    } else if (next !== req.body.password2) {
      flash(req, 'error', 'Hasła nie są identyczne.');
    } else {
      db.prepare('UPDATE users SET password_hash = ? WHERE id = ?').run(bcrypt.hashSync(next, 12), req.user.id);
      flash(req, 'success', 'Hasło zostało zmienione.');
    }
    res.redirect('/account');
  });

  return router;
};

module.exports.MIN_PASSWORD = MIN_PASSWORD;
