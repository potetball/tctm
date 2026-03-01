# Tiny Chess Tournament Manager (TCTM)

A lightweight web application for organising and running small chess tournaments (5–20 players). Create a tournament, share an invite link, and let the system handle pairings, result tracking, and standings — no accounts required.

**Demo:** [https://tctm.azurewebsites.net](https://tctm.azurewebsites.net)

## Features

- **Multiple formats** — Round Robin, Swiss, Single Elimination, Double Elimination
- **No accounts needed** — players join via invite code and are identified by tokens stored in local storage
- **Live standings** — results and standings update in real time via SignalR
- **Time control presets** — Bullet, Blitz, and Rapid with configurable minutes
- **Mobile-friendly** — responsive UI built with Vuetify and Tailwind CSS

## Tech Stack

| Layer    | Technology                            |
| -------- | ------------------------------------- |
| Frontend | Vue 3, Vite 7, Vuetify 4, Tailwind CSS 4 |
| Backend  | ASP.NET Core (.NET 10), C#           |
| Database | SQLite via EF Core                    |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (v20.19+ or v22.12+)
- npm (comes with Node.js)

### Clone the repository

```bash
git clone https://github.com/<your-username>/tctm.git
cd tctm/tctm
```

### Install frontend dependencies

```bash
cd tctm.client
npm install
cd ..
```

### Run the application

The ASP.NET project is configured to launch the Vite dev server automatically via SPA proxy, so a single command starts both the backend and frontend:

```bash
cd TCTM.Server
dotnet run
```

The app will be available at the URL shown in the console (typically `https://localhost:7xxx`). The database is created automatically on first run via EF Core migrations.

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
├── TCTM.Server/          # ASP.NET Core backend
│   ├── Controllers/      # API controllers
│   ├── DataModel/        # EF Core entities
│   ├── Dto/              # Data transfer objects
│   ├── Mappings/         # Entity ↔ DTO mappings
│   ├── Migrations/       # EF Core migrations
│   └── Services/         # Business logic (pairing, etc.)
├── tctm.client/          # Vue 3 frontend
│   ├── src/
│   │   ├── api/          # API client modules
│   │   ├── components/   # Reusable Vue components
│   │   ├── pages/        # Route-level page components
│   │   ├── plugins/      # Vuetify config
│   │   └── router/       # Vue Router setup
│   └── public/           # Static assets
└── TCTM.slnx             # Solution file
```

## License

This project is provided as-is for personal and educational use.