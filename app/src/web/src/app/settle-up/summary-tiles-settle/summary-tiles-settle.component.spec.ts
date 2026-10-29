import { ComponentFixture, TestBed } from '@angular/core/testing';
import { screen, within } from '@testing-library/angular';
import { SettleUp } from '../settle-up.service';
import { SummaryTilesSettleComponent } from './summary-tiles-settle.component';

/** Example 1 after Jo's payment of 40.00 (F6.AC8). */
const EXAMPLE_1_AFTER_PAYMENT: SettleUp = {
  suggestions: [],
  payments: [],
  paidBackCents: 4000,
  owedCents: 5000,
  leftToSettleCents: 1000,
  suggestionCount: 1,
  percentage: 80,
  totalSpentCents: 12000,
  expenseCount: 2,
};

function render(summary: SettleUp): ComponentFixture<SummaryTilesSettleComponent> {
  TestBed.configureTestingModule({});
  const fixture = TestBed.createComponent(SummaryTilesSettleComponent);
  fixture.componentRef.setInput('summary', summary);
  fixture.detectChanges();
  return fixture;
}

describe('SummaryTilesSettleComponent', () => {
  it('shows 40.00 of 50.00 owed, an 80% bar and 10.00 in 1 payment for Example 1 (F6.AC8)', () => {
    render(EXAMPLE_1_AFTER_PAYMENT);

    const paid = screen.getByRole('heading', { name: /Paid back so far/ });
    expect(within(paid).getByText('40.00')).toBeTruthy();
    expect(within(paid).getByText('50.00')).toBeTruthy();
    expect(screen.getByRole('progressbar', { name: 'Paid back so far' }).getAttribute('aria-valuenow')).toBe('80');
    expect(screen.getByText('80%')).toBeTruthy();
    expect(within(screen.getByRole('heading', { name: /Left to settle/ })).getByText('10.00')).toBeTruthy();
    expect(within(screen.getByRole('heading', { name: /Left to settle/ })).getByText('in 1 payment')).toBeTruthy();
  });

  it('shows the three tiles in order: Paid back so far, Left to settle, Total spent (F6.R12)', () => {
    render(EXAMPLE_1_AFTER_PAYMENT);

    const headings = screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent ?? '');
    expect(headings.length).toBe(3);
    expect(headings[0]).toContain('Paid back so far');
    expect(headings[1]).toContain('Left to settle');
    expect(headings[2]).toContain('Total spent');
  });

  it('shows 99% and not 100% when 199.00 of 200.00 is paid back (F6.AC10)', () => {
    render({ ...EXAMPLE_1_AFTER_PAYMENT, paidBackCents: 19900, owedCents: 20000, leftToSettleCents: 100, percentage: 99 });

    expect(screen.getByText('99%')).toBeTruthy();
    expect(screen.queryByText('100%')).toBeNull();
  });

  it('hides the bar and shows "Nothing to settle yet." when nothing is owed (F6.AC14)', () => {
    const fixture = render({
      ...EXAMPLE_1_AFTER_PAYMENT,
      paidBackCents: 0,
      owedCents: 0,
      leftToSettleCents: 0,
      suggestionCount: 0,
      percentage: null,
      totalSpentCents: 0,
      expenseCount: 0,
    });

    expect(screen.getByText('Nothing to settle yet.')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[role="progressbar"]')).toBeNull();
  });

  it('uses the plural forms for payments and expenses (F6 spec, Plural forms)', () => {
    render({ ...EXAMPLE_1_AFTER_PAYMENT, suggestionCount: 0, expenseCount: 1 });

    expect(within(screen.getByRole('heading', { name: /Left to settle/ })).getByText('in 0 payments')).toBeTruthy();
    expect(within(screen.getByRole('heading', { name: /Total spent/ })).getByText('across 1 expense')).toBeTruthy();
  });

  it('shows "in 2 payments" when two suggestions are left', () => {
    render({ ...EXAMPLE_1_AFTER_PAYMENT, suggestionCount: 2 });

    expect(within(screen.getByRole('heading', { name: /Left to settle/ })).getByText('in 2 payments')).toBeTruthy();
  });
});
