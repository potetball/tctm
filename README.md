# Tiny Chess Tournament Manager (TCTM)

A lightweight web application for organising and running small chess tournaments (5–20 players). Create a tournament, share an invite link, and let the system handle pairings, result tracking, and standings — no accounts required.

**Demo:** [https://tctm.azurewebsites.net](https://tctm.azurewebsites.net)

## Features

- **In-app live chess** — play games directly in the browser with a real-time board, move validation, and server-enforced clocks
- **Multiple formats** — Round Robin, Swiss, Single Elimination, Double Elimination
- **No accounts needed** — players join via invite code and are identified by tokens stored in local storage
- **Live standings** — results and standings update in real time via SignalR
- **Time control presets** — Bullet, Blitz, and Rapid with configurable minutes
- **Draw offers & resignation** — full in-game controls; organiser can abort games
- **Mobile-friendly** — responsive UI built with Vuetify and Tailwind CSS

## Tech Stack

| Layer        | Technology                                |
| ------------ | ----------------------------------------- |
| Frontend     | Vue 3, Vite 7, Vuetify 4, Tailwind CSS 4 |
| Backend      | ASP.NET Core (.NET 10), C#                |
| Chess Engine | ChessEngine (C#) — move validation, FEN/SAN, endgame detection |
| Database     | SQLite via EF Core                        |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (v20.19+ or v22.12+)
- npm (comes with Node.js)

### Clone the repository

```bash
git clone https://github.com/potetball/tctm.git
cd tctm/tctm
```

### Install frontend dependencies

```bash
cd tctm.client
npm install
cd ..
```

### Create launch settings

Create the file `TCTM.Server/Properties/launchSettings.json` with the following content (it is not checked in to source control):

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5081",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES": "Microsoft.AspNetCore.SpaProxy"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "https://localhost:7182;http://localhost:5081",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES": "Microsoft.AspNetCore.SpaProxy"
      }
    }
  }
}
```

### Run the application

The ASP.NET project is configured to launch the Vite dev server automatically via SPA proxy, so a single command starts both the backend and frontend:

```bash
cd TCTM.Server
dotnet run
```

The app will be available at `https://localhost:7182` (or `http://localhost:5081`). The database is created automatically on first run via EF Core migrations.

### Run frontend only (standalone)

If you want to work on just the frontend:

```bash
cd tctm.client
npm run dev
```

### Build for production

```bash
cd tctm.client
npm run build
```

```bash
cd TCTM.Server
dotnet publish -c Release
```

## Project Structure

```
tctm/
├── ChessEngine/              # C# chess library
│   ├── ChessBoard/           # Board logic, move generation, endgame rules
│   ├── Builders/             # SAN, PGN, FEN builders
│   ├── Conversions/          # SAN, FEN, PGN conversions
│   └── Types/                # Piece, Move, Position, enums
├── TCTM.Server/              # ASP.NET Core backend
│   ├── Controllers/          # API controllers (incl. LiveGamesController)
│   ├── DataModel/            # EF Core entities (incl. LiveGame)
│   ├── Dto/                  # Data transfer objects
│   ├── Hubs/                 # SignalR hubs (TournamentHub, LiveGameHub)
│   ├── Mappings/             # Entity ↔ DTO mappings
│   ├── Migrations/           # EF Core migrations
│   └── Services/             # Business logic (pairing, live game, clocks)
├── tctm.client/              # Vue 3 frontend
│   ├── src/
│   │   ├── api/              # API client modules (incl. liveGames)
│   │   ├── components/       # Reusable Vue components (board, clocks, etc.)
│   │   ├── composables/      # Vue composables (SignalR hubs, chess engine, store)
│   │   ├── pages/            # Route-level page components (incl. LiveGamePage)
│   │   ├── plugins/          # Vuetify config
│   │   └── router/           # Vue Router setup
│   └── public/               # Static assets
└── TCTM.slnx                 # Solution file
```

## License

This project is provided as-is for personal and educational use.