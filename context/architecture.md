# Architecture Context

## Stack

| Layer     | Technology                  | Role   |
| --------- | --------------------------- | ------ |
| Framework | [e.g. Next.js + TypeScript] | [Role] |
| UI        | [e.g. Tailwind + shadcn/ui] | [Role] |
| Auth      | [e.g. Clerk]                | [Role] |
| Database  | [e.g. Prisma + PostgreSQL]  | [Role] |
| [Layer]   | [Technology]                | [Role] |

## System Boundaries

- `[folder]` — [What this folder owns and is responsible for]
- `[folder]` — [What this folder owns and is responsible for]
- `[folder]` — [What this folder owns and is responsible for]
- `[folder]` — [What this folder owns and is responsible for]

## Storage Model

- **[Storage type e.g. Database]**: [What lives here —
  e.g. metadata, ownership, relationships]
- **[Storage type e.g. Blob/File Storage]**: [What lives
  here — e.g. generated files, media, large artifacts]

## Auth and Access Model

- The API issues and validates its own JWTs (`POST /api/auth/login`, 8 h, claims `sub`/`name`/`email`/`role`,
  HMAC key from `Jwt:SigningKey`). Passwords are hashed with ASP.NET Core `PasswordHasher<User>`.
- Four roles (`FloodLink.Domain.Security.Roles`): Volunteer (mobile), Coordinator (web), DepotManager (web +
  mobile check-in), Admin (everything). Self-registration always creates a Volunteer; only an Admin creates
  Coordinator/DepotManager accounts (`POST /api/users`).
- Every endpoint except `/api/auth/login`, `/api/auth/register`, `/health` and `/swagger` needs a token, in
  every environment. Role checks use the policies in `Api/Auth/Policies.cs`; Admin passes every policy.
- Ownership: a report belongs to the user in its `ReportedBy` (always taken from the token). A Volunteer can
  read only their own reports, and only the status / dispatch status / delivery confirmation of runs that
  include one of their reports (others return 404).
- Clients: React keeps the session in `sessionStorage` and sends it through `apiFetch`; Flutter keeps it in
  `flutter_secure_storage` and sends it through `ApiClient`. A 401 signs the client out.
- Demo accounts (one per role, `Infrastructure/DemoAccounts.cs`) are seeded locked; the startup seeder hashes
  `Seed:DemoPassword` into them, so no password is committed.

## Service Layer and Conventions

- Business operations live in services, not controllers: `ReportService` (report submission + urgency),
  `StockReservationService` (reserve / commit / release / check-in), `WorkflowService` (start / status / retry)
  and `DispatchService` (approve / reject / revision / delivery). Controllers map service results to HTTP.
- Every list endpoint pages through `Paging.ToPageAsync` and returns `PaginatedResult<T>`
  (`items`, `page`, `pageSize`, `totalItems`, `totalPages`); page size 1–100, default 20.
- `UpdatedAt` is never set by hand: entities implementing `IHasUpdatedAt` are stamped by `AppDbContext.SaveChanges`.
- Logging uses `ILogger` message templates with named fields (`WorkflowRunId`, `Agent`, `DurationMs`, `UserId`);
  outside Development the console output is JSON.

## Invariants

1. [Rule the codebase must never violate — e.g. Request
   handlers do not run long-lived background work]
2. [Invariant two]
3. [Invariant three]
4. [Invariant four]
