# Tenis Progress

Aplikacja webowa dla trenerów tenisa do śledzenia postępów młodych zawodników.
Trener jest administratorem: dodaje dzieci, zakłada konta rodziców i przypisuje im dzieci.
Każdy rodzic po zalogowaniu widzi **wyłącznie swoje dziecko lub dzieci** (tylko do odczytu).

## Funkcje

**Trener (administrator)**
- lista zawodników, dodawanie / edycja / usuwanie,
- oceny umiejętności w skali 1–10 (forhend, bekhend, serwis, wolej, return, praca nóg, taktyka, psychika)
  z porównaniem do poprzedniej oceny i wykresem średniej w czasie,
- dziennik treningów, mecze i turnieje (bilans W–P), cele z terminami,
- notatki prywatne niewidoczne dla rodziców,
- konta rodziców: zakładanie (z automatycznym hasłem tymczasowym), reset hasła, usuwanie,
  przypisywanie wielu dzieci do jednego rodzica i wielu rodziców do jednego dziecka.

**Rodzic**
- logowanie e-mailem i hasłem, zmiana własnego hasła,
- podgląd postępów tylko swoich dzieci (jedno dziecko → od razu jego profil).

## Bezpieczeństwo
- reguła dostępu w jednym miejscu (`canView` w `src/routes/players.js`): trener widzi wszystkich,
  rodzic tylko dzieci powiązane z nim w tabeli `player_parents`; cudzy profil zwraca 404
  (nie da się „zgadywać” identyfikatorów),
- wszystkie akcje zmieniające dane są dostępne tylko dla trenera,
- hasła hashowane bcryptem, sesje w SQLite z ciasteczkiem `httpOnly`/`sameSite`,
  regeneracja sesji przy logowaniu, ochrona CSRF, limit nieudanych logowań, nagłówki CSP.

## Uruchomienie

Wymagany Node.js ≥ 22.5 (wbudowany `node:sqlite`, bez kompilacji modułów natywnych).

```bash
npm install
npm start            # http://localhost:3000
```

Przy pierwszym uruchomieniu strona poprosi o utworzenie konta trenera
(formularz działa tylko, dopóki w bazie nie ma żadnego użytkownika).
Kolejnych trenerów można dodać z linii poleceń:

```bash
npm run create-coach -- "Jan Kowalski" jan@example.com haslo1234
```

Dane demonstracyjne (tylko do pustej bazy): `npm run seed` — loginy `trener@demo.pl`,
`anna@demo.pl`, `piotr@demo.pl`, hasło `demo1234`.

Testy: `npm test`.

## Aplikacja mobilna (PWA)

Tenis Progress można zainstalować na telefonie jak zwykłą aplikację — z ikoną na ekranie
początkowym i bez paska przeglądarki.

- **Android (Chrome):** otwórz stronę i stuknij „Zainstaluj” na pasku nad treścią
  (albo menu ⋮ → „Zainstaluj aplikację”).
- **iPhone / iPad (Safari):** „Udostępnij” → „Do ekranu początkowego”.

Wymagania: aplikacja musi działać pod adresem **HTTPS** (wyjątkiem jest `localhost`).
Service worker (`public/sw.js`) zapisuje w pamięci telefonu tylko statyczne pliki (style, skrypty, ikony).
Strony z danymi dzieci nigdy nie są zapisywane w pamięci podręcznej (`Cache-Control: no-store`),
więc po wylogowaniu na urządzeniu nic nie zostaje. Bez internetu pojawia się strona „Brak połączenia”.
Po zmianie plików statycznych zwiększ numer wersji `CACHE` w `public/sw.js`.

### Zmienne środowiskowe
| Zmienna | Opis |
|---|---|
| `PORT` | port HTTP (domyślnie 3000) |
| `DB_PATH` | ścieżka do pliku bazy (domyślnie `data/tenis.db`) |
| `SESSION_SECRET` | sekret sesji (domyślnie generowany i zapisywany w bazie) |
| `NODE_ENV=production` | ciasteczka `secure` + `trust proxy` — używać za HTTPS |
