# Free Demo Deployment Guide (Vercel + Render + Free PostgreSQL)

This guide deploys **CMS_CA** completely on free tiers for live client/stakeholder demos:

- **Frontend (React + Vite)**: Vercel (Hobby Free)
- **Backend (.NET 8 API)**: Render (Free Docker Web Service)
- **Database (PostgreSQL 16)**: Render Free Postgres (or **Neon** / **Supabase** Free Postgres if you need > 30 days)

---

## Architecture Overview

```
Browser
  ├─ Loads SPA from Vercel (https://<your-app>.vercel.app)
  │    └─ frontend/vercel.json rewrites SPA routes -> /index.html
  └─ Calls API directly on Render (https://<your-api>.onrender.com/api/v1/...)
       └─ CORS allowed via CORS_ALLOWED_ORIGINS
       └─ Npgsql connects via DATABASE_URL (postgres://... + SSL)
```

---

## Step 1: Deploy Backend + Database on Render (Free Blueprint)

1. Push this repository to GitHub/GitLab.
2. Open the [Render Dashboard](https://dashboard.render.com) → **New +** → **Blueprint**.
3. Select this repository. Render automatically detects [`render.yaml`](../render.yaml) in the repo root.
4. *(Optional latency tip for India)*: Before applying, edit `render.yaml` to uncomment `region: singapore` under **both** `cms-ca-api` and `cms-ca-db`.
5. Click **Apply**. Render provisions:
   - `cms-ca-db` (Free PostgreSQL)
   - `cms-ca-api` (Free Docker Web Service built from `backend/Dockerfile`)
6. Wait for the first deploy to finish and go green (`/api/v1/health`).
   - On startup, `Db__AutoMigrate=true`, `Db__SeedOnStartup=true`, and `Db__SeedDemoData=true` automatically apply EF Core migrations and seed default roles, Indian states, entity types, services, compliance templates, and the demo admin account (`admin@cms.local` / `Admin!23`).
7. Copy your Render backend URL, e.g. `https://cms-ca-api.onrender.com` (no trailing slash).
   - Swagger UI is available at `https://<your-api>.onrender.com/swagger` for demo API inspection (`Swagger__Enabled=true`).

---

## Step 2: Deploy Frontend on Vercel (Free)

1. Open [Vercel](https://vercel.com/new) → import the same repository.
2. In **Configure Project**:
   - **Root Directory**: `frontend` *(important — `frontend/vercel.json` lives here)*
   - **Framework Preset**: `Vite` (auto-detected)
   - **Build Command**: `npm run build`
   - **Output Directory**: `dist`
3. Under **Environment Variables**, add:
   - **Name**: `VITE_API_BASE_URL`
   - **Value**: `https://cms-ca-api.onrender.com` *(your exact Render URL from Step 1, no trailing slash)*
4. Click **Deploy**.
5. Once live, open your `https://<your-app>.vercel.app` URL and sign in with `admin@cms.local` / `Admin!23`.

---

## Step 3 (Recommended): Lock Down CORS to Your Exact Vercel URL

`render.yaml` sets `CORS_ALLOWED_ORIGINS="https://*.vercel.app,http://localhost:5173"` so your initial deploy and Vercel preview links work right away.

Once your production Vercel domain is known, tighten CORS in **Render Dashboard → `cms-ca-api` → Environment**:

- Update `CORS_ALLOWED_ORIGINS` to:
  ```
  https://your-exact-app.vercel.app
  ```
- Save changes (triggers a quick redeploy).

---

## Free-Tier Demo Caveats & Tips

| Limitation | Behavior & Free Workaround |
|---|---|
| **Render Free Web Service Cold Starts** | Spins down after 15 min of inactivity; first request takes **20–50 seconds**. `LoginPage` automatically pings `/api/v1/health` as soon as the page opens and shows a wake-up banner if login takes > 4s. For a scheduled live demo, optionally point a free pinger (e.g., Cron-job.org / UptimeRobot) at `/api/v1/health` every 10–14 min during demo hours (staying within Render's 750 free hours/month). |
| **Render Free Postgres 30-Day Expiry** | Render Free Postgres instances expire 30 days after creation. If your demo needs to stay alive longer without upgrading, create a free database on **Neon** or **Supabase**, copy its `postgresql://...` connection string, and paste it into `DATABASE_URL` in Render's Environment tab (you can then delete the `cms-ca-db` service). `PostgresConnectionStringHelper` automatically parses cloud `postgresql://` URIs and enforces SSL. |
| **Ephemeral Uploaded Files (`App_Data`)** | Render Free Web Services do not retain local disk files across restarts/redeploys, so uploaded documents in `App_Data/documents` reset on redeploy while PostgreSQL rows remain. Upload fresh sample files during/before a demo, or switch to persistent disk / S3-compatible object storage before production use. |
| **Email & Password Reset Flows** | Demo deployments without SMTP log/drop password-reset links depending on mail configuration; avoid demoing external email delivery unless SMTP env vars are configured. |

---

## Local Development Compatibility

Nothing changes locally: leave `VITE_API_BASE_URL` unset (or empty in `frontend/.env.local`), and Vite's dev proxy (`frontend/vite.config.ts`) or `docker-compose.yml` nginx proxy continues forwarding `/api/...` seamlessly.
