import { pluralize } from './pluralize';

describe('pluralize', () => {
  it('uses the singular only for exactly one (F6 spec, Plural forms)', () => {
    expect(pluralize(1, 'payment', 'payments')).toBe('1 payment');
    expect(pluralize(1, 'expense', 'expenses')).toBe('1 expense');
  });

  it('uses the plural for zero and for two or more', () => {
    expect(pluralize(0, 'payment', 'payments')).toBe('0 payments');
    expect(pluralize(2, 'payment', 'payments')).toBe('2 payments');
    expect(pluralize(3, 'expense', 'expenses')).toBe('3 expenses');
  });
});
