import { tripExpenseText } from './expense-count-text';

describe('tripExpenseText (F4.R17)', () => {
  it('says "no expenses yet" for no expenses (F4.AC21)', () => {
    expect(tripExpenseText(0)).toBe('no expenses yet');
  });

  it('says "1 expense" for one, not "1 expenses" (F4.AC20)', () => {
    expect(tripExpenseText(1)).toBe('1 expense');
  });

  it('says "{n} expenses" for more than one (F4.AC19)', () => {
    expect(tripExpenseText(3)).toBe('3 expenses');
  });
});
