'use strict';

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { DatabaseSync } = require('node:sqlite');

const SKILLS = [
  ['forehand', 'Forhend'],
  ['backhand', 'Bekhend'],
  ['serve', 'Serwis'],
  ['volley', 'Wolej'],
  ['return_serve', 'Return'],
  ['footwork', 'Praca nóg'],
  ['tactics', 'Taktyka'],
  ['mental', 'Psychika'],
];

const SCHEMA = `
PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;

CREATE TABLE IF NOT EXISTS settings (
  key TEXT PRIMARY KEY,
  value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS users (
  id INTEGER PRIMARY KEY,
  email TEXT NOT NULL UNIQUE COLLATE NOCASE,
  name TEXT NOT NULL,
  password_hash TEXT NOT NULL,
  role TEXT NOT NULL CHECK (role IN ('coach', 'parent')),
  phone TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS players (
  id INTEGER PRIMARY KEY,
  first_name TEXT NOT NULL,
  last_name TEXT NOT NULL,
  birth_date TEXT,
  level TEXT,
  dominant_hand TEXT CHECK (dominant_hand IN ('right', 'left') OR dominant_hand IS NULL),
  notes TEXT,
  private_notes TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS player_parents (
  player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  parent_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  PRIMARY KEY (player_id, parent_id)
);

CREATE TABLE IF NOT EXISTS assessments (
  id INTEGER PRIMARY KEY,
  player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  date TEXT NOT NULL,
  ${SKILLS.map(([k]) => `${k} INTEGER CHECK (${k} BETWEEN 1 AND 10)`).join(',\n  ')},
  comment TEXT,
  created_by INTEGER REFERENCES users(id) ON DELETE SET NULL,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS trainings (
  id INTEGER PRIMARY KEY,
  player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  date TEXT NOT NULL,
  topic TEXT NOT NULL,
  duration_min INTEGER,
  notes TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS matches (
  id INTEGER PRIMARY KEY,
  player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  date TEXT NOT NULL,
  tournament TEXT,
  opponent TEXT,
  result TEXT NOT NULL CHECK (result IN ('W', 'L')),
  score TEXT,
  notes TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS goals (
  id INTEGER PRIMARY KEY,
  player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  title TEXT NOT NULL,
  target_date TEXT,
  done INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS sessions (
  sid TEXT PRIMARY KEY,
  data TEXT NOT NULL,
  expires INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_pp_parent ON player_parents(parent_id);
CREATE INDEX IF NOT EXISTS idx_assess_player ON assessments(player_id, date);
CREATE INDEX IF NOT EXISTS idx_train_player ON trainings(player_id, date);
CREATE INDEX IF NOT EXISTS idx_match_player ON matches(player_id, date);
CREATE INDEX IF NOT EXISTS idx_goal_player ON goals(player_id);
`;

function openDatabase(file = process.env.DB_PATH || path.join(__dirname, '..', 'data', 'tenis.db')) {
  if (file !== ':memory:') fs.mkdirSync(path.dirname(file), { recursive: true });
  const db = new DatabaseSync(file);
  db.exec(SCHEMA);
  return db;
}

function getSessionSecret(db) {
  if (process.env.SESSION_SECRET) return process.env.SESSION_SECRET;
  const row = db.prepare("SELECT value FROM settings WHERE key = 'session_secret'").get();
  if (row) return row.value;
  const secret = crypto.randomBytes(32).toString('hex');
  db.prepare("INSERT INTO settings (key, value) VALUES ('session_secret', ?)").run(secret);
  return secret;
}

module.exports = { openDatabase, getSessionSecret, SKILLS };
