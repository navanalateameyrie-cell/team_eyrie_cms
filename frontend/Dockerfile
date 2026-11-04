# syntax=docker/dockerfile:1
# -----------------------------------------------------------------------------
# Build stage — install deps + build the Vite SPA.
# -----------------------------------------------------------------------------
FROM node:20-alpine AS build
WORKDIR /src

# Lockfile layer first.
COPY frontend/package.json frontend/package-lock.json* ./
RUN npm ci --no-audit --no-fund

COPY frontend/ ./
RUN npm run build

# -----------------------------------------------------------------------------
# Runtime stage — nginx serves the built assets + proxies /api to the api service.
# -----------------------------------------------------------------------------
FROM nginx:1.27-alpine AS runtime
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist /usr/share/nginx/html

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD wget -qO- http://127.0.0.1:8080/ || exit 1
