# Toolbox Manager — UI

React + TypeScript + Vite + Tailwind frontend. Talks to the [API](../api/) for everything; no direct DB or SQS access.

## Pages

| Route | Component | Purpose |
|-------|-----------|---------|
| `/`                       | `ApplicationsList`   | All registered apps, with an "Include inactive" toggle |
| `/applications/new`       | `ApplicationForm`    | Register a new app with inline parameter definitions |
| `/applications/:id`       | `ApplicationDetail`  | Metadata, parameters, recent runs, **Run** button |
| `/applications/:id/edit`  | `ApplicationForm`    | Edit an existing app + its parameters |
| `/runs`                   | `RunHistory`         | All runs across apps, filterable by status, polls every 5s |
| `/runs/:id`               | `RunDetail`          | Status, parameter values, stdout / stderr; polls while non-terminal |

## Stack

- **React 18** with React Router 6
- **TanStack Query** for server state and polling
- **Tailwind CSS** for styling (no UI component library — keeps the bundle tiny)
- **TypeScript 5** in strict mode

## Configuration

The only build-time / runtime variable is the API base URL:

```
VITE_API_BASE_URL=http://localhost:8080
```

In dev (Vite), set it in your shell or in `.env`. In a built container, it's baked in at `docker build` time via the `VITE_API_BASE_URL` arg in [Dockerfile](Dockerfile).

> Vite inlines `import.meta.env.VITE_*` constants at build time. To change the API URL for a deployed UI you must rebuild the image (or swap to a runtime-config bootstrap, which we haven't done yet because we have one deploy target).

## Local development

### Inside docker compose (recommended)

```powershell
docker compose up --build ui
# UI: http://localhost:5173
```

### Direct (Vite dev server)

```powershell
cd ui
npm install
npm run dev
```

Make sure the API is running (either `dotnet run` or `docker compose up api`) and `VITE_API_BASE_URL` points at it.

## Build

```powershell
npm run build       # outputs ./dist/
npm run preview     # serves the built bundle on :5173 to sanity-check
```

## Project layout

```
src/
├── api/
│   ├── client.ts      Thin fetch wrapper (no axios). Throws on non-2xx.
│   └── types.ts       Mirrors the C# DTOs.
├── components/
│   ├── Layout.tsx
│   ├── StatusBadge.tsx
│   └── RunModal.tsx
├── pages/
│   ├── ApplicationsList.tsx
│   ├── ApplicationDetail.tsx
│   ├── ApplicationForm.tsx
│   ├── RunHistory.tsx
│   └── RunDetail.tsx
├── App.tsx           Route table
├── main.tsx          Bootstrapping + React Query provider
└── index.css         Tailwind layers and shared `btn` / `field` utilities
```

## Tests

None in the initial scaffold. When added, use Vitest + React Testing Library.
