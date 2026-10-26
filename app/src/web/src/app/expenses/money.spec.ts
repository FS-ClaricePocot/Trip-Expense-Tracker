import { formatCents } from './money';

describe('formatCents (F4.R13)', () => {
  it('formats whole cents as a two-decimal amount', () => {
    expect(formatCents(1050)).toBe('10.50');
  });

  it('formats zero as 0.00', () => {
    expect(formatCents(0)).toBe('0.00');
  });

  it('keeps a leading zero for amounts under one unit', () => {
    expect(formatCents(5)).toBe('0.05');
    expect(formatCents(99)).toBe('0.99');
  });

  it('formats large amounts without float rounding', () => {
    expect(formatCents(123456789)).toBe('1234567.89');
  });
});
