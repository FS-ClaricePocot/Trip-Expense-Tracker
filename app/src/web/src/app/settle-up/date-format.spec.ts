import { formatPaymentDate } from './date-format';

describe('formatPaymentDate', () => {
  it('shows the full day, month and year of a UTC timestamp in a fixed zone (F6 spec, Edge cases)', () => {
    expect(formatPaymentDate('2026-10-20T12:00:00Z', 'UTC')).toBe('20 Oct 2026');
  });

  it('converts the UTC timestamp to the zone it is shown in, so the date can change', () => {
    // 23:30 UTC on 20 Oct is 12:30 on 21 Oct in Auckland (UTC+13 in October).
    expect(formatPaymentDate('2026-10-20T23:30:00Z', 'Pacific/Auckland')).toBe('21 Oct 2026');
    expect(formatPaymentDate('2026-10-20T23:30:00Z', 'UTC')).toBe('20 Oct 2026');
  });
});
