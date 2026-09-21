# FloodLink AI — Monorepo

**An AI-Coordinated Disaster & Flood Relief Resource Platform for Sri Lanka**

FloodLink AI connects field volunteers, depot staff, and relief coordinators through
a structured, auditable workflow: field reports → AI triage → inventory matching →
route/ETA → deterministic validation → **human approval** → dispatch.

---

## Repository Structure

```
FloodLink/
├── backend/          # ASP.NET Core Web API, EF Core, PostgreSQL, xUnit tests
│   ├── src/          # Production projects
│   └── tests/        # Test projects
├── web/              # Vite + React + TypeScript + Tailwind CSS (coordinator dashboard)
├── mobile/           # Flutter (field volunteer app)
├── docker-compose.yml
├── global.json
└── Directory.Packages.props
```

---

## Getting Started

### Prerequisites

| Tool | Minimum Version |
|------|----------------|
| .NET SDK | 9.0 (pinned in `global.json`) |
| Docker + Docker Compose | Docker Desktop or Engine ≥ 24 |
| Node.js | 20 LTS |
| Flutter SDK | 3.22+ |

---

### 1 — Start the Database

```bash
# From the FloodLink/ root
docker-compose up -d
```

This starts a `postgres:16` container on port **5432** with:
- Database: `floodlink`
- User: `floodlink`
- Password: `floodlink_dev_pw`

---

### 2 — Run the Backend API

```bash
cd backend/src/FloodLink.Api

# First time only: apply EF Core migrations
dotnet ef database update --project ../FloodLink.Infrastructure

# Start the API (listens on http://localhost:5000 by default)
dotnet run
```

> Health check: `GET http://localhost:5000/health` → `{"status":"Healthy"}`
>
> Swagger UI: `http://localhost:5000/swagger`

#### Run Backend Tests

```bash
cd backend/tests/FloodLink.Tests
dotnet test
```

---

### 3 — Run the Web Dashboard

```bash
cd web

# Install dependencies
npm install

# Start Vite dev server (http://localhost:5173)
npm run dev
```

Copy `.env.example` to `.env` and set `VITE_API_BASE_URL` if needed:

```bash
cp .env.example .env
```

---

### 4 — Run the Mobile App

```bash
cd mobile

# Fetch dependencies
flutter pub get

# Run on a connected device or emulator
flutter run
```

---

## Team

| Member | Role | Component |
|--------|------|-----------|
| Sanka (Member C) | Team Lead / CI Owner | Orchestration, Workflow Engine, Route/ETA Agent |
| Sharani (Member A) | Member | Field Intake, Shelter Reporting, Triage/Planner Agent |
| Eshini (Member B) | Member | Inventory & Depot Management, Logistics/Matching Agent |
| Ijini (Member D) | Member | Validation, Approval & Dispatch Audit, Validation/Safety Agent |

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for branch naming, PR rules, code ownership,
and state-management conventions.
