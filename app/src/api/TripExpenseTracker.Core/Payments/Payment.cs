namespace TripExpenseTracker.Core.Payments;

/// <summary>
/// A recorded payment (F6.R6). Amount is whole cents, greater than 0. MarkedPaidAt is UTC, from the clock.
/// Payments are never edited (F6.R9); a mistake is fixed by deleting the payment.
/// </summary>
public sealed record Payment(
    Guid Id,
    Guid TripId,
    Guid PayerPersonId,
    Guid PayeePersonId,
    long AmountCents,
    DateTimeOffset MarkedPaidAt);

/// <summary>A stored payment with the names shown in the list. <see cref="Seq"/> is the list order (F6.R7).</summary>
public sealed record StoredPayment(Payment Payment, long Seq, string PayerName, string PayeeName);
