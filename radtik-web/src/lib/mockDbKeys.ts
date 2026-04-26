/** Top-level keys in `public/mock/db.json` — used to hydrate the full object from json-server. */
export const MOCK_DB_KEYS = [
  'stats',
  'trafficSeries',
  'userGrowth',
  'accessPoints',
  'coverageZones',
  'activity',
  'financialRows',
  'employeeTasks',
  'supportInteractions',
  'managerTeam',
  'revenueSeries',
  'clientPlan',
  'clientInvoices',
  'collectionPayments',
  'collectionsChart',
  'users',
] as const

export type MockDbKey = (typeof MOCK_DB_KEYS)[number]
