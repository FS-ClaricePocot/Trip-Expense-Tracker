namespace TripExpenseTracker.Core.SettleUp;

/// <summary>
/// One suggested payment: the payer pays the payee this many cents (F6.R1, F6.R2).
/// </summary>
public sealed record SuggestedPayment(Guid PayerPersonId, Guid PayeePersonId, long AmountCents);
