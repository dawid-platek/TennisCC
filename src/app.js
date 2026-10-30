'use strict';

const path = require('node:path');
const express = require('express');
const session = require('express-session');
const { openDatabase, getSessionSecret } = require('./db');
const SQLiteStore = require('./session-store');
const { csrf, loadUser, notFound } = require('./middleware');
const { formatDate } = require('./util');

function createApp({ db = openDatabase() } = {}) {
  const app = express();
  const production = process.env.NODE_ENV === 'production';

  app.set('view engine', 'ejs');
  app.set('views', path.join(__dirname, '..', 'views'));
  app.disable('x-powered-by');
  if (production) app.set('trust proxy', 1);

  app.locals.formatDate = formatDate;

  app.use((req, res, next) => {
    res.set({
      'X-Content-Type-Options': 'nosniff',
      'X-Frame-Options': 'DENY',
      'Referrer-Policy': 'same-origin',
      'Content-Security-Policy': "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; form-action 'self'; frame-ancestors 'none'",
    });
    next();
  });
  app.use(express.static(path.join(__dirname, '..', 'public'), { maxAge: production ? '1d' : 0 }));
  app.use(express.urlencoded({ extended: false, limit: '100kb' }));
  app.use(
    session({
      name: 'tenis.sid',
      store: new SQLiteStore(db),
      secret: getSessionSecret(db),
      resave: false,
      saveUninitialized: false,
      rolling: true,
      cookie: { httpOnly: true, sameSite: 'lax', secure: production, maxAge: 7 * 24 * 60 * 60 * 1000 },
    })
  );
  app.use(loadUser(db));
  app.use(csrf);

  app.use(require('./routes/auth')(db));
  app.use(require('./routes/players')(db));
  app.use(require('./routes/parents')(db));

  app.use((req, res) => notFound(res));
  app.use((err, req, res, next) => {
    console.error(err);
    res.status(500).render('error', { title: 'Błąd', message: 'Wystąpił nieoczekiwany błąd serwera.' });
  });

  return app;
}

module.exports = { createApp };
