'use strict';

const crypto = require('node:crypto');

function csrf(req, res, next) {
  if (!req.session.csrfToken) req.session.csrfToken = crypto.randomBytes(24).toString('hex');
  res.locals.csrfToken = req.session.csrfToken;
  if (req.method === 'POST') {
    const sent = String((req.body && req.body._csrf) || '');
    const expected = req.session.csrfToken;
    const ok =
      sent.length === expected.length &&
      crypto.timingSafeEqual(Buffer.from(sent), Buffer.from(expected));
    if (!ok) {
      return res.status(403).render('error', {
        title: 'Błąd',
        message: 'Sesja formularza wygasła. Odśwież stronę i spróbuj ponownie.',
      });
    }
  }
  next();
}

// Loads the logged-in user (if any) and exposes it to templates.
function loadUser(db) {
  const stmt = db.prepare('SELECT id, email, name, role FROM users WHERE id = ?');
  return (req, res, next) => {
    req.user = req.session.userId ? stmt.get(req.session.userId) || null : null;
    res.locals.user = req.user;
    res.locals.flash = req.session.flash || null;
    delete req.session.flash;
    next();
  };
}

function requireLogin(req, res, next) {
  if (!req.user) return res.redirect('/login');
  next();
}

function requireCoach(req, res, next) {
  if (!req.user) return res.redirect('/login');
  if (req.user.role !== 'coach') return forbidden(res);
  next();
}

function forbidden(res) {
  return res.status(403).render('error', {
    title: 'Brak dostępu',
    message: 'Nie masz uprawnień do tej strony.',
  });
}

function notFound(res) {
  return res.status(404).render('error', {
    title: 'Nie znaleziono',
    message: 'Taka strona nie istnieje.',
  });
}

function flash(req, type, text) {
  req.session.flash = { type, text };
}

// Simple in-memory brute-force protection for the login form.
function loginLimiter({ max = 10, windowMs = 15 * 60 * 1000 } = {}) {
  const attempts = new Map();
  return {
    blocked(key) {
      const entry = attempts.get(key);
      if (!entry) return false;
      if (Date.now() - entry.start > windowMs) {
        attempts.delete(key);
        return false;
      }
      return entry.count >= max;
    },
    fail(key) {
      const entry = attempts.get(key);
      if (!entry || Date.now() - entry.start > windowMs) attempts.set(key, { count: 1, start: Date.now() });
      else entry.count++;
    },
    reset(key) {
      attempts.delete(key);
    },
  };
}

module.exports = { csrf, loadUser, requireLogin, requireCoach, forbidden, notFound, flash, loginLimiter };
