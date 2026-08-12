# RadaTik SPA — Design System (Figma ↔ Code)

## Status

| Item | Status |
|------|--------|
| Code design system (tokens + components) | ✅ Done in `src/` |
| Token export for Figma Variables | ✅ `design-tokens.json` |
| Code Connect templates | ✅ Ready — need real Figma file URLs |
| Live Figma file / Slides via MCP | ⏳ Blocked — Figma MCP not connected in this workspace |

## When Figma MCP is available

1. Create a Design System file (or use `/figma-create-new-file`).
2. Import variables from `design-tokens.json` (light + dark modes).
3. Build components matching: Button, Card, StatCard, Input, StatusBadge, TabGroup, Alert.
4. Wire Code Connect in `figma/components/*.figma.ts` — replace `FIGMA_FILE_KEY` and node IDs.
5. Optionally generate a handoff deck with `/figma-use-slides`.

## Color tokens (`src/index.css`)

| Token | Light | Dark |
|-------|-------|------|
| `rt-primary` | `#2563eb` | `#60a5fa` |
| `rt-page` | `#f1f5f9` | `#050810` |
| `rt-surface` | `#ffffff` | `#0c1222` |
| `rt-elevated` | `#ffffff` | `#151d2e` |
| `rt-green` / `rt-danger` / `rt-accent-orange` | semantic | semantic |

## Semantic tones (`src/lib/tone.ts`)

Use `toneSurface()` / `toneBadge()` / `riskTone()` instead of raw Tailwind emerald/amber/rose.

## Core components (same look on every role/page)

- `Button`, `LinkButton`, `Card`, `StatCard`, `DataTable`
- `Input`, `Select`, `Textarea`, `FieldLabel`
- `StatusBadge`, `TabGroup`, `Toggle`, `Alert`, `QueryStatus`
- `PageContent`, `PageHeader`, `ListRow`

## Navigation UX

- Quick search: `⌘K` / `Ctrl+K`
- Desktop: sidebar + breadcrumbs
- Mobile: bottom nav (primary) + More sheet (all pages)

## Responsive

- **Mobile** `< md`: drawer, bottom nav, card tables
- **Tablet** `md+`: persistent sidebar, 2-col grids
- **Laptop** `lg+`: wider sidebar, 3-col KPIs, full tables
