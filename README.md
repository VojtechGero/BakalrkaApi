# API pro aplikaci pro správu PDF dokumentů s využitím OCR

Tento repozitář obsahuje backendové API, které slouží jako serverová část desktopové aplikace vyvíjené v rámci bakalářské práce na téma:

**„Aplikace pro správu PDF dokumentů s využitím OCR technologie“**

## Klientská aplikace

Frontendová desktopová aplikace, která s tímto API komunikuje, je dostupná zde:  
🔗 https://github.com/VojtechGero/Bakalarka

### Postup Spuštění

1. **Získání přístupových údajů**
   - V Azure portálu si vytvořte (nebo použijte existující) službu Azure Document Intelligence.
   - Získané přihlašovací údaje obsahují:
     - `URL` služby
     - `KEY` pro autentizaci

2. **Nastavení konfiguračního souboru**
   - V repozitáři se **nenachází** soubor `appsettings.json`, protože je uveden v `.gitignore`.
   - Místo toho je k dispozici šablona `appsettings.json.template`, která obsahuje kompletní strukturu očekávaného konfiguračního souboru.
   - Pro nastavení postupujte následovně:
     1. Zkopírujte soubor `appsettings.json.template`.
     2. Přejmenujte kopii na `appsettings.json`.
     3. Vyplňte hodnoty `URL` a `KEY` ve vhodné sekci souboru.

3. **Příklad struktury souboru `appsettings.json`:**
  ```javascript
  {
    "Logging": {
      "LogLevel": {
        "Default": "Information",
        "Microsoft.AspNetCore": "Warning"
      }
    },
    "AllowedHosts": "*",
    "Api": {
      "Url": "https://path.to.api.com",
      "Key": "apikey12345"
    }
  }
  ```
