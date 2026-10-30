'use strict';

const { SKILLS } = require('./db');

function text(value, max = 200) {
  const s = typeof value === 'string' ? value.trim() : '';
  return s ? s.slice(0, max) : null;
}

function date(value) {
  const s = typeof value === 'string' ? value.trim() : '';
  if (!/^\d{4}-\d{2}-\d{2}$/.test(s)) return null;
  const d = new Date(s + 'T00:00:00Z');
  return Number.isNaN(d.getTime()) || d.toISOString().slice(0, 10) !== s ? null : s;
}

function int(value, min, max) {
  const n = Number.parseInt(value, 10);
  if (!Number.isInteger(n) || n < min || n > max) return null;
  return n;
}

function today() {
  return new Date().toISOString().slice(0, 10);
}

function age(birthDate) {
  if (!birthDate) return null;
  const b = new Date(birthDate + 'T00:00:00Z');
  const now = new Date();
  let years = now.getUTCFullYear() - b.getUTCFullYear();
  const m = now.getUTCMonth() - b.getUTCMonth();
  if (m < 0 || (m === 0 && now.getUTCDate() < b.getUTCDate())) years--;
  return years;
}

function average(assessment) {
  const vals = SKILLS.map(([k]) => assessment[k]).filter((v) => v != null);
  return vals.length ? vals.reduce((a, b) => a + b, 0) / vals.length : null;
}

function formatDate(iso) {
  if (!iso) return '';
  const [y, m, d] = iso.slice(0, 10).split('-');
  return `${d}.${m}.${y}`;
}

function escapeXml(s) {
  return String(s).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
}

// Server-rendered SVG line chart: one line per series, values on a 0–10 scale.
function lineChart(points, series) {
  if (points.length < 2) return '';
  const W = 640, H = 260, L = 34, R = 16, T = 14, B = 34;
  const x = (i) => L + (i * (W - L - R)) / (points.length - 1);
  const y = (v) => T + ((10 - v) * (H - T - B)) / 10;

  let svg = `<svg class="chart" viewBox="0 0 ${W} ${H}" role="img" aria-label="Wykres postępów">`;
  for (let v = 0; v <= 10; v += 2) {
    svg += `<line class="gridline" x1="${L}" x2="${W - R}" y1="${y(v)}" y2="${y(v)}"/>`;
    svg += `<text class="axis" x="${L - 8}" y="${y(v) + 4}" text-anchor="end">${v}</text>`;
  }
  const step = Math.ceil(points.length / 8);
  points.forEach((p, i) => {
    if (i % step === 0 || i === points.length - 1) {
      svg += `<text class="axis" x="${x(i)}" y="${H - 10}" text-anchor="middle">${escapeXml(formatDate(p.date).slice(0, 5))}</text>`;
    }
  });
  for (const s of series) {
    const coords = points
      .map((p, i) => (p[s.key] == null ? null : `${x(i).toFixed(1)},${y(p[s.key]).toFixed(1)}`))
      .filter(Boolean);
    if (!coords.length) continue;
    svg += `<polyline class="${s.cls}" points="${coords.join(' ')}"/>`;
    points.forEach((p, i) => {
      if (p[s.key] != null) {
        svg += `<circle class="${s.cls}" cx="${x(i).toFixed(1)}" cy="${y(p[s.key]).toFixed(1)}" r="3.5"><title>${escapeXml(formatDate(p.date))}: ${p[s.key].toFixed(1)}</title></circle>`;
      }
    });
  }
  return svg + '</svg>';
}

module.exports = { text, date, int, today, age, average, formatDate, lineChart };
