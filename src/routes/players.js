'use strict';

const express = require('express');
const { SKILLS } = require('../db');
const { requireLogin, requireCoach, notFound, flash } = require('../middleware');
const { text, date, int, today, age, average, lineChart } = require('../util');

const RECORD_TABLES = { assessments: 'assessments', trainings: 'trainings', matches: 'matches', goals: 'goals' };

module.exports = function playerRoutes(db) {
  const router = express.Router();

  const q = {
    player: db.prepare('SELECT * FROM players WHERE id = ?'),
    isParentOf: db.prepare('SELECT 1 FROM player_parents WHERE player_id = ? AND parent_id = ?'),
    latestAssessment: db.prepare('SELECT * FROM assessments WHERE player_id = ? ORDER BY date DESC, id DESC LIMIT 1'),
    parentsOf: db.prepare(
      'SELECT u.id, u.name, u.email FROM users u JOIN player_parents pp ON pp.parent_id = u.id WHERE pp.player_id = ? ORDER BY u.name'
    ),
  };

  // Central access rule: coaches see every player, parents only their own children.
  function canView(user, playerId) {
    if (user.role === 'coach') return true;
    return !!q.isParentOf.get(playerId, user.id);
  }

  function loadPlayer(req, res, next) {
    const id = int(req.params.id, 1, Number.MAX_SAFE_INTEGER);
    const player = id && q.player.get(id);
    // Same response for "doesn't exist" and "not yours" so parents can't probe other ids.
    if (!player || !canView(req.user, player.id)) return notFound(res);
    req.player = player;
    next();
  }

  function summary(player) {
    const latest = q.latestAssessment.get(player.id);
    return { ...player, age: age(player.birth_date), latestAvg: latest ? average(latest) : null };
  }

  function playerFields(body) {
    const hand = body.dominant_hand;
    return {
      first_name: text(body.first_name, 80),
      last_name: text(body.last_name, 80),
      birth_date: date(body.birth_date),
      level: text(body.level, 60),
      dominant_hand: hand === 'right' || hand === 'left' ? hand : null,
      notes: text(body.notes, 4000),
      private_notes: text(body.private_notes, 4000),
    };
  }

  // ---- Parent home -------------------------------------------------------

  router.get('/my', requireLogin, (req, res) => {
    if (req.user.role === 'coach') return res.redirect('/players');
    const players = db
      .prepare(
        'SELECT p.* FROM players p JOIN player_parents pp ON pp.player_id = p.id WHERE pp.parent_id = ? ORDER BY p.first_name'
      )
      .all(req.user.id)
      .map(summary);
    if (players.length === 1) return res.redirect(`/players/${players[0].id}`);
    res.render('my', { title: 'Moje dzieci', players });
  });

  // ---- Coach: players list -----------------------------------------------

  router.get('/players', requireCoach, (req, res) => {
    const players = db.prepare('SELECT * FROM players ORDER BY last_name, first_name').all().map((p) => ({
      ...summary(p),
      parents: q.parentsOf.all(p.id),
    }));
    res.render('players', { title: 'Zawodnicy', players });
  });

  router.post('/players', requireCoach, (req, res) => {
    const f = playerFields(req.body);
    if (!f.first_name || !f.last_name) {
      flash(req, 'error', 'Imię i nazwisko zawodnika są wymagane.');
      return res.redirect('/players');
    }
    const { lastInsertRowid } = db
      .prepare(
        'INSERT INTO players (first_name, last_name, birth_date, level, dominant_hand, notes, private_notes) VALUES (?, ?, ?, ?, ?, ?, ?)'
      )
      .run(f.first_name, f.last_name, f.birth_date, f.level, f.dominant_hand, f.notes, f.private_notes);
    flash(req, 'success', `Dodano zawodnika: ${f.first_name} ${f.last_name}.`);
    res.redirect(`/players/${lastInsertRowid}`);
  });

  // ---- Player profile (coach + own parents) ------------------------------

  router.get('/players/:id', requireLogin, loadPlayer, (req, res) => {
    const p = req.player;
    const isCoach = req.user.role === 'coach';
    const assessments = db
      .prepare('SELECT * FROM assessments WHERE player_id = ? ORDER BY date ASC, id ASC')
      .all(p.id)
      .map((a) => ({ ...a, avg: average(a) }));
    const latest = assessments[assessments.length - 1] || null;
    const previous = assessments[assessments.length - 2] || null;
    const trainings = db.prepare('SELECT * FROM trainings WHERE player_id = ? ORDER BY date DESC, id DESC').all(p.id);
    const matches = db.prepare('SELECT * FROM matches WHERE player_id = ? ORDER BY date DESC, id DESC').all(p.id);
    const goals = db.prepare('SELECT * FROM goals WHERE player_id = ? ORDER BY done ASC, target_date IS NULL, target_date, id').all(p.id);
    const wins = matches.filter((m) => m.result === 'W').length;

    const player = { ...p, age: age(p.birth_date) };
    if (!isCoach) delete player.private_notes;

    res.render('player', {
      title: `${p.first_name} ${p.last_name}`,
      player,
      isCoach,
      skills: SKILLS,
      assessments: assessments.slice().reverse(),
      latest,
      previous,
      chart: lineChart(assessments, [{ key: 'avg', cls: 's-avg' }]),
      trainings,
      matches,
      stats: { wins, losses: matches.length - wins, trainings: trainings.length },
      goals,
      parents: q.parentsOf.all(p.id),
      allParents: isCoach ? db.prepare("SELECT id, name, email FROM users WHERE role = 'parent' ORDER BY name").all() : [],
      today: today(),
    });
  });

  // ---- Coach: edit / delete player ---------------------------------------

  router.get('/players/:id/edit', requireCoach, loadPlayer, (req, res) => {
    res.render('player-edit', { title: 'Edycja zawodnika', player: req.player });
  });

  router.post('/players/:id/edit', requireCoach, loadPlayer, (req, res) => {
    const f = playerFields(req.body);
    if (!f.first_name || !f.last_name) {
      flash(req, 'error', 'Imię i nazwisko zawodnika są wymagane.');
      return res.redirect(`/players/${req.player.id}/edit`);
    }
    db.prepare(
      'UPDATE players SET first_name = ?, last_name = ?, birth_date = ?, level = ?, dominant_hand = ?, notes = ?, private_notes = ? WHERE id = ?'
    ).run(f.first_name, f.last_name, f.birth_date, f.level, f.dominant_hand, f.notes, f.private_notes, req.player.id);
    flash(req, 'success', 'Zapisano zmiany.');
    res.redirect(`/players/${req.player.id}`);
  });

  router.post('/players/:id/delete', requireCoach, loadPlayer, (req, res) => {
    db.prepare('DELETE FROM players WHERE id = ?').run(req.player.id);
    flash(req, 'success', `Usunięto zawodnika: ${req.player.first_name} ${req.player.last_name}.`);
    res.redirect('/players');
  });

  // ---- Coach: parent links -----------------------------------------------

  router.post('/players/:id/parents', requireCoach, loadPlayer, (req, res) => {
    const parentId = int(req.body.parent_id, 1, Number.MAX_SAFE_INTEGER);
    const parent = parentId && db.prepare("SELECT id FROM users WHERE id = ? AND role = 'parent'").get(parentId);
    if (parent) {
      db.prepare('INSERT OR IGNORE INTO player_parents (player_id, parent_id) VALUES (?, ?)').run(req.player.id, parent.id);
      flash(req, 'success', 'Przypisano rodzica.');
    } else {
      flash(req, 'error', 'Wybierz rodzica z listy.');
    }
    res.redirect(`/players/${req.player.id}#rodzice`);
  });

  router.post('/players/:id/parents/:parentId/remove', requireCoach, loadPlayer, (req, res) => {
    db.prepare('DELETE FROM player_parents WHERE player_id = ? AND parent_id = ?').run(req.player.id, req.params.parentId);
    flash(req, 'success', 'Odłączono rodzica od zawodnika.');
    res.redirect(`/players/${req.player.id}#rodzice`);
  });

  // ---- Coach: progress records -------------------------------------------

  router.post('/players/:id/assessments', requireCoach, loadPlayer, (req, res) => {
    const d = date(req.body.date);
    const scores = SKILLS.map(([k]) => int(req.body[k], 1, 10));
    if (!d || scores.every((s) => s == null)) {
      flash(req, 'error', 'Podaj datę i ocenę przynajmniej jednej umiejętności (1–10).');
    } else {
      db.prepare(
        `INSERT INTO assessments (player_id, date, ${SKILLS.map(([k]) => k).join(', ')}, comment, created_by)
         VALUES (?, ?, ${SKILLS.map(() => '?').join(', ')}, ?, ?)`
      ).run(req.player.id, d, ...scores, text(req.body.comment, 2000), req.user.id);
      flash(req, 'success', 'Zapisano ocenę umiejętności.');
    }
    res.redirect(`/players/${req.player.id}#oceny`);
  });

  router.post('/players/:id/trainings', requireCoach, loadPlayer, (req, res) => {
    const d = date(req.body.date);
    const topic = text(req.body.topic, 200);
    if (!d || !topic) {
      flash(req, 'error', 'Podaj datę i temat treningu.');
    } else {
      db.prepare('INSERT INTO trainings (player_id, date, topic, duration_min, notes) VALUES (?, ?, ?, ?, ?)').run(
        req.player.id, d, topic, int(req.body.duration_min, 1, 600), text(req.body.notes, 2000)
      );
      flash(req, 'success', 'Dodano trening.');
    }
    res.redirect(`/players/${req.player.id}#treningi`);
  });

  router.post('/players/:id/matches', requireCoach, loadPlayer, (req, res) => {
    const d = date(req.body.date);
    const result = req.body.result === 'W' || req.body.result === 'L' ? req.body.result : null;
    if (!d || !result) {
      flash(req, 'error', 'Podaj datę i wynik meczu.');
    } else {
      db.prepare('INSERT INTO matches (player_id, date, tournament, opponent, result, score, notes) VALUES (?, ?, ?, ?, ?, ?, ?)').run(
        req.player.id, d, text(req.body.tournament, 200), text(req.body.opponent, 200), result,
        text(req.body.score, 60), text(req.body.notes, 2000)
      );
      flash(req, 'success', 'Dodano mecz.');
    }
    res.redirect(`/players/${req.player.id}#mecze`);
  });

  router.post('/players/:id/goals', requireCoach, loadPlayer, (req, res) => {
    const title = text(req.body.title, 300);
    if (!title) {
      flash(req, 'error', 'Podaj treść celu.');
    } else {
      db.prepare('INSERT INTO goals (player_id, title, target_date) VALUES (?, ?, ?)').run(req.player.id, title, date(req.body.target_date));
      flash(req, 'success', 'Dodano cel.');
    }
    res.redirect(`/players/${req.player.id}#cele`);
  });

  router.post('/players/:id/goals/:goalId/toggle', requireCoach, loadPlayer, (req, res) => {
    db.prepare('UPDATE goals SET done = 1 - done WHERE id = ? AND player_id = ?').run(req.params.goalId, req.player.id);
    res.redirect(`/players/${req.player.id}#cele`);
  });

  router.post('/players/:id/:kind/:recordId/delete', requireCoach, loadPlayer, (req, res) => {
    const table = RECORD_TABLES[req.params.kind];
    if (!table) return notFound(res);
    db.prepare(`DELETE FROM ${table} WHERE id = ? AND player_id = ?`).run(req.params.recordId, req.player.id);
    flash(req, 'success', 'Usunięto wpis.');
    res.redirect(`/players/${req.player.id}`);
  });

  return router;
};
