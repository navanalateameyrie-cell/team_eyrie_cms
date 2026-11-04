# End-to-end tests (Playwright)

Smoke tests that drive the real browser against a running stack.

## Run

```
docker compose up -d --build
cd frontend
npm install
npm run e2e:install   # one-time: fetches chromium
npm run e2e
```

Environment:

| Variable          | Default                 |
| ----------------- | ----------------------- |
| `CMS_BASE_URL`    | `http://localhost:5173` |
| `CMS_ADMIN_EMAIL` | `admin@cms.local`       |
| `CMS_ADMIN_PASS`  | `Admin!23`              |

## Scope

This suite is **intentionally narrow** — login smoke only. Deep flows (client create → invoice →
PDF → email) are covered by the backend integration suite today; broader e2e coverage lands in a
dedicated phase.
