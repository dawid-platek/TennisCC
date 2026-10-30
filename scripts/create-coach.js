'use strict';

// Usage: npm run create-coach -- "Jan Kowalski" jan@example.com haslo1234
const bcrypt = require('bcryptjs');
const { openDatabase } = require('../src/db');

const [name, email, password] = process.argv.slice(2);
if (!name || !email || !password || password.length < 8) {
  console.error('Użycie: npm run create-coach -- "Imię Nazwisko" email@example.com haslo (min. 8 znaków)');
  process.exit(1);
}

const db = openDatabase();
const existing = db.prepare('SELECT id FROM users WHERE email = ?').get(email);
if (existing) {
  db.prepare("UPDATE users SET name = ?, password_hash = ?, role = 'coach' WHERE id = ?").run(name, bcrypt.hashSync(password, 12), existing.id);
  console.log(`Zaktualizowano konto trenera: ${email}`);
} else {
  db.prepare("INSERT INTO users (email, name, password_hash, role) VALUES (?, ?, ?, 'coach')").run(email, name, bcrypt.hashSync(password, 12));
  console.log(`Utworzono konto trenera: ${email}`);
}
