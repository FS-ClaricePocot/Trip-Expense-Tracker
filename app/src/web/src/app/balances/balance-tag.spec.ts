import { balanceTagText } from './balance-tag';

describe('balanceTagText (F5.R2, F5.R6, F5.AC3, F5.AC4)', () => {
  it('reads "is owed" for a positive balance, with no minus sign', () => {
    expect(balanceTagText('owed', 5000)).toBe('is owed 50.00');
  });

  it('reads "owes" for a negative balance, with the absolute amount', () => {
    expect(balanceTagText('owes', -1000)).toBe('owes 10.00');
  });

  it('reads "settled up" with no amount for a zero balance', () => {
    expect(balanceTagText('settled', 0)).toBe('settled up');
  });

  it('groups thousands in the amount (F5.AC11)', () => {
    expect(balanceTagText('owed', 184250)).toBe('is owed 1,842.50');
  });
});
