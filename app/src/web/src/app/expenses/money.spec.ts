import { formatCents, formatCentsGrouped } from './money';

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

describe('formatCentsGrouped (F5.R9, F5.AC11)', () => {
  it('adds a thousands separator to 1842.50', () => {
    expect(formatCentsGrouped(184250)).toBe('1,842.50');
  });

  it('leaves amounts under 1,000 without a separator', () => {
    expect(formatCentsGrouped(99999)).toBe('999.99');
    expect(formatCentsGrouped(0)).toBe('0.00');
  });

  it('groups millions and keeps the sign for negative input', () => {
    expect(formatCentsGrouped(123456789)).toBe('1,234,567.89');
    expect(formatCentsGrouped(-100000)).toBe('-1,000.00');
  });
});
