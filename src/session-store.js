'use strict';

const session = require('express-session');

const DAY_MS = 24 * 60 * 60 * 1000;

// Minimal express-session store backed by the app's SQLite database,
// so logins survive server restarts.
class SQLiteStore extends session.Store {
  constructor(db) {
    super();
    this.db = db;
    this.getStmt = db.prepare('SELECT data FROM sessions WHERE sid = ? AND expires > ?');
    this.setStmt = db.prepare(
      'INSERT INTO sessions (sid, data, expires) VALUES (?, ?, ?) ' +
        'ON CONFLICT(sid) DO UPDATE SET data = excluded.data, expires = excluded.expires'
    );
    this.destroyStmt = db.prepare('DELETE FROM sessions WHERE sid = ?');
    this.touchStmt = db.prepare('UPDATE sessions SET expires = ? WHERE sid = ?');
    this.pruneStmt = db.prepare('DELETE FROM sessions WHERE expires <= ?');
    this.pruneTimer = setInterval(() => this.pruneStmt.run(Date.now()), 60 * 60 * 1000);
    this.pruneTimer.unref();
  }

  static expiry(sess) {
    const exp = sess.cookie && sess.cookie.expires;
    return exp ? new Date(exp).getTime() : Date.now() + DAY_MS;
  }

  get(sid, cb) {
    try {
      const row = this.getStmt.get(sid, Date.now());
      cb(null, row ? JSON.parse(row.data) : null);
    } catch (err) {
      cb(err);
    }
  }

  set(sid, sess, cb) {
    try {
      this.setStmt.run(sid, JSON.stringify(sess), SQLiteStore.expiry(sess));
      cb && cb(null);
    } catch (err) {
      cb && cb(err);
    }
  }

  destroy(sid, cb) {
    try {
      this.destroyStmt.run(sid);
      cb && cb(null);
    } catch (err) {
      cb && cb(err);
    }
  }

  touch(sid, sess, cb) {
    try {
      this.touchStmt.run(SQLiteStore.expiry(sess), sid);
      cb && cb(null);
    } catch (err) {
      cb && cb(err);
    }
  }
}

module.exports = SQLiteStore;
