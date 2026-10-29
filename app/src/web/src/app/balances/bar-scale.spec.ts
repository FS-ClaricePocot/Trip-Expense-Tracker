import { barWidthPercent } from './bar-scale';

describe('barWidthPercent (F5.R6, F5.AC17)', () => {
  it('gives no bar for a zero balance', () => {
    expect(barWidthPercent(0)).toBe(0);
  });

  it('is proportional up to 50.00, where the bar reaches halfway from the centre to the end', () => {
    expect(barWidthPercent(2500)).toBe(12.5);
    expect(barWidthPercent(5000)).toBe(25);
  });

  it('uses the magnitude, so a negative balance gives the same length on the left', () => {
    expect(barWidthPercent(-5000)).toBe(25);
    expect(barWidthPercent(-2500)).toBe(12.5);
  });

  it('clips above 50.00 at the end of the track', () => {
    expect(barWidthPercent(5001)).toBe(50);
    expect(barWidthPercent(8000)).toBe(50);
    expect(barWidthPercent(-8000)).toBe(50);
  });
});
