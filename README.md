# CMS — Client Management System for Chartered Accountants

Production CMS for a CA practice: clients, branches, service files, compliance obligations,
documents, GST invoicing with IRN/QR + digital signature, email delivery, reports, settings UI,
and a read-only client portal.

Stack:
- **Backend**: .NET 8 (Clean Architecture) + EF Core + PostgreSQL. Serilog, JWT + refresh tokens,
  policy-based permissions, append-only audit, QuestPDF, QRCoder.
- **Frontend**: React 18 + TypeScript + Vite + Tailwind.

## Quick start

```
docker compose up -d --build
```

- Web UI: http://localhost:5173
- API:    http://localhost:5080 (Swagger at `/swagger` in Development)

Dev credentials: `admin@cms.local` / `Admin!23`.

See [`docs/deployment.md`](docs/deployment.md) for env vars, migrations, backups, and health.

## Local development

Backend only (hits a locally-run Postgres):

```
cd backend/src/CMS.Api
dotnet run
```

Frontend only (Vite dev server, proxies `/api` to `http://127.0.0.1:5080`):

```
cd frontend
npm install
npm run dev
```

Full test suite:

```
dotnet test backend/tests/CMS.Api.IntegrationTests
```

(Currently 240/240 integration tests.)

## Repo layout

```
backend/
  src/
    CMS.Domain/         # entities + domain-level invariants
    CMS.Application/    # abstractions, DTOs, services
    CMS.Infrastructure/ # EF Core, external adapters (QuestPDF, SMTP, IRP, DSC, ...)
    CMS.Api/            # ASP.NET Core controllers + Program.cs
  tests/
    CMS.Api.IntegrationTests/
frontend/
  src/
    pages/              # admin + portal pages
    components/         # Layout, DocumentsPanel, etc.
    api/                # apiFetch with refresh interceptor
    auth/               # AuthContext
docs/
  deployment.md
```

## Phases shipped

Phase 1–30: platform foundation → auth/RBAC → audit → masters → clients/branches/service files →
engagements → staff assignments → global search → import engine → exports → compliance calendar
→ documents → billing → PDF → reports → email + reminders → settings UI → auth hardening
(refresh tokens, forgot-password, rate limiting, security headers) → e-invoicing IRN+QR → client
portal → credit/debit notes + advances → digital signature on invoices → Docker + deployment.
