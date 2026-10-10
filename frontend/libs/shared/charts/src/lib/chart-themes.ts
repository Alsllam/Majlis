/**
 * ECharts theme names, matching the files in `docs/brand/tokens/echarts/`. The `ngx-echarts` setup, option builders
 * and `<majlis-chart>` land with the first dashboard (usage and analytics); no screen in the walking skeleton charts.
 */
export const CHART_THEMES = { light: 'majlis-light', dark: 'majlis-dark', dim: 'majlis-dim' } as const;
export type ChartTheme = (typeof CHART_THEMES)[keyof typeof CHART_THEMES];
