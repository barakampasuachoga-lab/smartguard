# SmartGuard

SmartGuard is a property security dashboard with a React frontend and a .NET 8 API. It includes resident and administrator workspaces, property and event tracking, alerts, reports, and administrator email delivery through SMTP.

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
