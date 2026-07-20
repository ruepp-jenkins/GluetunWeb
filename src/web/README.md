# GluetunWeb — frontend

The React 19 + Vite + TypeScript + Tailwind CSS v4 single-page app for
[GluetunWeb](../../README.md). It is built into the API's `wwwroot` and served by the ASP.NET Core
backend — there is no separate frontend deployment.

## Develop

```bash
npm install
npm run dev      # Vite dev server on :5173, proxies /api to the backend on :8080
```

Run the backend in another terminal (see the [root README](../../README.md#local-development)).

## Build / check

```bash
npm run build    # tsc -b && vite build → outputs into ../GluetunWeb.Api/wwwroot
npx tsc --noEmit # typecheck only
```

## Layout

```
src/
  api/          typed fetch client + DTO types (mirror the backend Dtos.cs)
  components/   shared UI (ui.tsx: Button/ActionButton/icons, Field, Modal, Table, …)
  context/      AuthContext, ButtonStyleContext (per-browser display preference)
  pages/        one file per route (Guide, Global Settings, Credentials, Providers,
                Custom VPN, Connections, Load Balancers, Login)
  data/         envCatalog.ts — the inline field descriptions/examples (kept in sync
                with docs/ENVVARS.md)
```

Env-var help text is shown **inline** next to each field (never as tooltips) from
`data/envCatalog.ts`; keep it in step with [`docs/ENVVARS.md`](../../docs/ENVVARS.md).
