# UTB.Minute – Canteen Ordering System

Semestrální projekt do předmětu Architektura a frameworky.

## Architektura (Microservices via Aspire)

Aplikace je rozdělena do několika služeb orchestraných pomocí **.NET Aspire**:
- `minutedb` – PostgreSQL databáze.
- `UTB.Minute.DbManager` – Inicializace databáze a seeding (spouští se automaticky při startu, podporuje `/reset-db`).
- `UTB.Minute.WebApi` – Core backend API. Zajišťuje CRUD operace a logiku pro objednávky, včetně SSE notifikací.
- `Keycloak` – Identity provider, běží v kontejneru a poskytuje OIDC login.
- `UTB.Minute.AdminClient` – Blazor Server aplikace pro vedení menzy (správa jídel a menu). Ochrana rolí `admin`.
- `UTB.Minute.Web` (CanteenClient) – Blazor Server aplikace pro studenty a kuchařky. Poslouchá SSE stream a automaticky se překresluje (real-time notifikace kuchařky + live menu). Ochrana rolí `student` / `cook`.

## Implementované funkce (100% pokrytí zadání)

1. **Entity a repozitáře** (5 b.) – `Dish`, `MenuItem`, `Order`.
2. **Web API** (6 b.) – Endpointy s business logikou (ověření stavů, aktivace/deaktivace).
3. **Aspire orchestrace** (4 b.) – Servisní discovery, PostgreSQL + Keycloak kontejner.
4. **Klient – Správa pro Admina** (3+2 = 5 b.) – Úprava položek, menu.
5. **Klient – Odbavení kuchařky a studenta** (6+2 = 8 b.) – Notifikace, živé updaty, RowVersion zamykání na `MenuItem`.
6. **Zabezpečení (Keycloak)** (6 b.) – Autentizace a autorizace dle role.
7. **Testování** (4 b.) – 22 plně funkčních integračních testů s fixture reálné DB.

## Spuštění projektu

1. Vyžaduje **Docker Desktop**.
2. Otevřete solution ve Visual Studiu nebo spusťte pomocí CLI:
   ```bash
   dotnet run --project UTB.Minute.AppHost
   ```
3. Aspire Dashboard bude k dispozici (zpravidla `https://localhost:15042`). Z něj lze přejít do:
   - **AdminClient**
   - **CanteenClient**

### Testovací uživatelé

Keycloak obsahuje 3 předpřipravené uživatele:
- **admin** (heslo: `admin123`) – Přístup do AdminClient
- **cook** (heslo: `cook123`) – Přístup do Kitchen view v CanteenClient
- **student** (heslo: `student123`) – Přístup do Student view v CanteenClient
