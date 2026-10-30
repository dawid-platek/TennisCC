'use strict';

// Fills an empty database with demo data: one coach, two parents, three kids.
const bcrypt = require('bcryptjs');
const { openDatabase, SKILLS } = require('../src/db');

const db = openDatabase();
if (db.prepare('SELECT COUNT(*) AS n FROM users').get().n > 0) {
  console.error('Baza nie jest pusta — pomijam dane demonstracyjne.');
  process.exit(1);
}

const hash = bcrypt.hashSync('demo1234', 12);
const addUser = db.prepare('INSERT INTO users (email, name, password_hash, role) VALUES (?, ?, ?, ?)');
const addPlayer = db.prepare(
  'INSERT INTO players (first_name, last_name, birth_date, level, dominant_hand, notes) VALUES (?, ?, ?, ?, ?, ?)'
);
const link = db.prepare('INSERT INTO player_parents (player_id, parent_id) VALUES (?, ?)');
const addAssessment = db.prepare(
  `INSERT INTO assessments (player_id, date, ${SKILLS.map(([k]) => k).join(', ')}, comment) VALUES (?, ?, ${SKILLS.map(() => '?').join(', ')}, ?)`
);

addUser.run('trener@demo.pl', 'Trener Demo', hash, 'coach');
const anna = Number(addUser.run('anna@demo.pl', 'Anna Nowak', hash, 'parent').lastInsertRowid);
const piotr = Number(addUser.run('piotr@demo.pl', 'Piotr Wiśniewski', hash, 'parent').lastInsertRowid);

const kids = [
  ['Zosia', 'Nowak', '2015-04-12', 'Green', 'right', anna],
  ['Kuba', 'Nowak', '2017-09-03', 'Orange', 'left', anna],
  ['Maja', 'Wiśniewska', '2014-01-25', 'Yellow', 'right', piotr],
];

for (const [first, last, birth, level, hand, parent] of kids) {
  const id = Number(addPlayer.run(first, last, birth, level, hand, 'Bardzo zaangażowany zawodnik, regularna frekwencja.').lastInsertRowid);
  link.run(id, parent);
  let base = 3 + Math.floor(Math.random() * 2);
  ['2026-03-01', '2026-05-01', '2026-07-01', '2026-09-01'].forEach((d, i) => {
    const scores = SKILLS.map(() => Math.min(10, base + i + Math.floor(Math.random() * 2)));
    addAssessment.run(id, d, ...scores, i === 3 ? 'Wyraźny postęp w regularności wymian.' : null);
  });
  db.prepare('INSERT INTO trainings (player_id, date, topic, duration_min) VALUES (?, ?, ?, ?)').run(id, '2026-09-10', 'Serwis – rzut piłki i rytm', 60);
  db.prepare('INSERT INTO matches (player_id, date, tournament, opponent, result, score) VALUES (?, ?, ?, ?, ?, ?)').run(
    id, '2026-09-20', 'Turniej Klubowy', 'J. Kowalski', 'W', '6:3 6:4'
  );
  db.prepare('INSERT INTO goals (player_id, title, target_date) VALUES (?, ?, ?)').run(id, '20 wymian z rzędu z linii końcowej', '2026-12-31');
}

console.log('Dane demo gotowe. Loginy (hasło: demo1234): trener@demo.pl, anna@demo.pl, piotr@demo.pl');
