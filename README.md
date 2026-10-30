# Tenis Progress

Aplikacja dla trenerów tenisa do śledzenia postępów młodych zawodników.
Trener jest administratorem: dodaje dzieci, zakłada konta rodziców i przypisuje im dzieci.
Każdy rodzic po zalogowaniu widzi **wyłącznie swoje dziecko lub dzieci** (tylko do odczytu).

**Technologie:** Blazor WebAssembly (PWA) + ASP.NET Core (.NET 10, minimal API) + EF Core + SQLite.

## Funkcje

**Trener (administrator)**
- lista zawodników z wyszukiwarką, dodawanie / edycja / usuwanie,
- oceny umiejętności w skali 1–10 (forhend, bekhend, serwis, wolej, return, praca nóg, taktyka, psychika)
  z porównaniem do poprzedniej oceny i wykresem średniej w czasie,
- dziennik treningów, mecze i turnieje (bilans W–P), cele z terminami,
- notatki prywatne niewidoczne dla rodziców,
- konta rodziców: zakładanie (z automatycznym hasłem tymczasowym), reset hasła, usuwanie,
  przypisywanie wielu dzieci do jednego rodzica i wielu rodziców do jednego dziecka.

**Rodzic**
- logowanie e-mailem i hasłem, zmiana własnego hasła,
- podgląd postępów tylko swoich dzieci (jedno dziecko → od razu jego profil).

## Struktura

```
src/
  TenisProgress.Server/   ASP.NET Core: REST API (/api), logowanie, baza danych, hostuje klienta
    Endpoints/            AuthEndpoints, PlayerEndpoints, ParentEndpoints
    Data/                 encje EF Core, AppDbContext, migracje
    Auth/                 ciasteczko logowania, role, limit prób logowania
  TenisProgress.Client/   Blazor WebAssembly (PWA): strony, komponenty, service worker
  TenisProgress.Shared/   modele DTO i reguły walidacji wspólne dla klienta i serwera
tests/
  TenisProgress.Tests/    testy integracyjne API (xUnit + WebApplicationFactory)
```

## Bezpieczeństwo
- **Dostęp sprawdza serwer**, nie przeglądarka. Jedna reguła w `PlayerQueries.VisibleTo`
  (`src/TenisProgress.Server/Data/AppDbContext.cs`): trener widzi wszystkich, rodzic tylko dzieci
  powiązane z jego kontem. Cudzy profil zwraca 404 (nie da się „zgadywać” identyfikatorów).
- Wszystkie operacje zmieniające dane wymagają roli trenera (`RequireAuthorization("Coach")`).
- Notatki prywatne i lista rodziców nie są w ogóle wysyłane do przeglądarki rodzica.
- Hasła hashowane (`PasswordHasher` z ASP.NET Core Identity), ciasteczko `HttpOnly` + `SameSite=Strict`
  (ochrona CSRF), limit prób logowania, nagłówki CSP.
- Reset lub zmiana hasła unieważnia wszystkie dotychczasowe sesje (security stamp).

## Uruchomienie

Wymagany [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/TenisProgress.Server     # http://localhost:5170
```

Przy pierwszym uruchomieniu baza tworzy się sama (`src/TenisProgress.Server/data/tenis.db`),
a strona poprosi o utworzenie konta trenera. Ten formularz działa tylko, dopóki w bazie nie ma
żadnego użytkownika. Kolejnych trenerów dodaje się z linii poleceń:

```bash
dotnet run --project src/TenisProgress.Server -- create-coach "Jan Kowalski" jan@example.com haslo1234
```

Dane demonstracyjne (tylko do pustej bazy) — loginy `trener@demo.pl`, `anna@demo.pl`,
`piotr@demo.pl`, hasło `demo1234`:

```bash
dotnet run --project src/TenisProgress.Server -- seed
```

Testy: `dotnet test`.

### Wdrożenie

```bash
dotnet publish src/TenisProgress.Server -c Release -o publish
cd publish && ASPNETCORE_URLS=http://0.0.0.0:8080 dotnet TenisProgress.Server.dll
```

Aplikację wystaw za serwerem z HTTPS (np. Caddy, nginx, Azure App Service). Nagłówki
`X-Forwarded-*` są obsługiwane.

| Ustawienie (zmienna środowiskowa) | Opis |
|---|---|
| `ConnectionStrings__Default` | baza SQLite, domyślnie `Data Source=data/tenis.db` |
| `ASPNETCORE_URLS` | adres i port serwera |

Zmiana modelu danych: `dotnet ef migrations add NazwaZmiany -p src/TenisProgress.Server -o Data/Migrations`
(narzędzie instaluje `dotnet tool restore`). Migracje są stosowane automatycznie przy starcie.

## Aplikacja mobilna (PWA)

Tenis Progress można zainstalować na telefonie jak zwykłą aplikację — z ikoną na ekranie
początkowym i bez paska przeglądarki.

- **Android (Chrome):** stuknij „Zainstaluj” na pasku nad treścią (albo menu ⋮ → „Zainstaluj aplikację”).
- **iPhone / iPad (Safari):** „Udostępnij” → „Do ekranu początkowego”.

Wymagany jest **HTTPS** (wyjątkiem jest `localhost`). Service worker (w wersji opublikowanej)
zapisuje w telefonie tylko pliki aplikacji, więc uruchamia się ona szybko, także bez internetu.
Dane dzieci (`/api`) nigdy nie są zapisywane w pamięci podręcznej (`Cache-Control: no-store`).
Bez połączenia aplikacja pokazuje komunikat „Brak połączenia” z przyciskiem ponowienia.
