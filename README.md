# SmartGuard

SmartGuard is a home security and property monitoring dashboard with a React frontend and a .NET 8 API. It includes resident and administrator workspaces, property and event tracking, alerts, reports, subscription billing through M-PESA STK Push, and administrator email delivery through SMTP.

## Requirements

- .NET 8 SDK
- Node.js and npm

## Run locally

Start the API from the repository root:

```powershell
dotnet run --project backend/SmartGuard.API/SmartGuard.API.csproj
```

The API listens on `http://localhost:5156`; Swagger is at `http://localhost:5156/swagger`.

In a second terminal, start the frontend:

```powershell
cd frontend
npm ci
npm run dev
```

The frontend is available at `http://localhost:5173`.

## Email delivery

The admin email composer uses Resend SMTP. To run the API with email enabled, stop any existing API instance and run `backend/SmartGuard.API/Run-With-Resend.ps1`. The script prompts for a sender address on a Resend-verified domain and a Resend API key. The key is not written to project files or persisted as an environment variable after the API stops.

Local SQLite data and uploaded profile/property photos are intentionally excluded from Git.

## Subscriptions and M-PESA

The app seeds three editable plan suggestions: Basic (KSh 499/month), Standard (KSh 999/month), and Professional (KSh 1,999/month). New accounts receive a 14-day Standard trial. Plan feature and property/device limits are checked by the API. Residents can view their plan and billing history, start an STK Push, and schedule cancellation; administrators can review subscription counts and revenue.

To enable Daraja sandbox payments, create/obtain the app credentials through the Safaricom Daraja developer portal and configure the backend only. The callback must be a public HTTPS address that Safaricom can reach (localhost alone will not work; use an HTTPS development tunnel for local sandbox work). In PowerShell, set these in the same terminal before starting the API:

```powershell
$env:Daraja__BaseUrl = "https://sandbox.safaricom.co.ke"
$env:Daraja__ConsumerKey = "<sandbox consumer key>"
$env:Daraja__ConsumerSecret = "<sandbox consumer secret>"
$env:Daraja__ShortCode = "<sandbox shortcode>"
$env:Daraja__Passkey = "<sandbox passkey>"
$env:Daraja__CallbackToken = "<long random secret>"
$env:Daraja__CallbackUrl = "https://<public-https-host>/api/payments/mpesa/callback/$env:Daraja__CallbackToken"
dotnet run --project backend/SmartGuard.API/SmartGuard.API.csproj
```

Use values issued in the Daraja portal for your app and product configuration. Keep consumer credentials, passkey, and callback token out of React and source control. After the backend callback confirms payment, the user's profile shows the payment as awaiting administrator approval. An administrator approves it from the user's profile to activate the subscription for one month. M-PESA STK Push asks the customer to authorize each payment, so this prototype does not silently charge recurring monthly payments. Production use also needs production Daraja credentials and an always reachable HTTPS callback. Prices and limits live in `SubscriptionService.Plans` and can be changed after validating costs and customer demand.

The optional PostgreSQL provider is selected by setting `Database:Provider` to `Postgres` or supplying `ConnectionStrings:Postgres`; set the connection string using a backend environment variable, for example:

```powershell
$env:ConnectionStrings__Postgres = "Host=localhost;Database=smartguard;Username=<user>;Password=<password>"
$env:Database__Provider = "Postgres"
```
