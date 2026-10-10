import { CHART_THEMES } from './chart-themes';

describe('chart themes', () => {
  it('has one theme per color mode', () => {
    expect(Object.keys(CHART_THEMES)).toEqual(['light', 'dark', 'dim']);
  });
});
