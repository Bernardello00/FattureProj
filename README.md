# FattureProj

Servizio ASP.NET Core per estrarre fatture elettroniche FatturaPA (anche XML firmati `.p7m`) da un archivio ZIP e produrre un report Excel.

## Configurazione

Impostare `CompanyIdentity` in `src/Fatture.Web/appsettings.json`. In Azure App Service gli stessi valori possono essere forniti come impostazioni applicazione/variabili d'ambiente usando la convenzione gerarchica .NET:

- `CompanyIdentity__VatCountryCode`
- `CompanyIdentity__VatNumber`
- `CompanyIdentity__FiscalCode`

## Utilizzo

```bash
dotnet run --project src/Fatture.Web
curl -F file=@fatture.zip http://localhost:5000/api/fatture/report -o report-fatture.xlsx
```

Il workbook contiene i fogli strutturati **Fatture** e **Dettaglio IVA**.
