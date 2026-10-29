using TripExpenseTracker.Core.SettleUp;

namespace TripExpenseTracker.Core.Payments;

/// <summary>Why a payment or settle-up request was refused. Mapped to the API codes by the controller.</summary>
public enum PaymentFailure
{
    /// <summary>The trip is not the owner's or does not exist (F6.R15). Same for every settle-up call.</summary>
    NotFound,

    /// <summary>Payer and payee are the same person (F6.R6). Code <c>payer_payee_same</c>.</summary>
    PayerPayeeSame,

    /// <summary>Amount is 0 or less (F6.R6). Code <c>amount_not_positive</c>.</summary>
    AmountNotPositive,

    /// <summary>The payer is not on this trip. Code <c>payer_not_on_trip</c>.</summary>
    PayerNotOnTrip,

    /// <summary>The payee is not on this trip. Code <c>payee_not_on_trip</c>.</summary>
    PayeeNotOnTrip,

    /// <summary>No current suggestion matches the payer, payee and amount (F6.R18). Code <c>suggestions_changed</c>.</summary>
    SuggestionsChanged,

    /// <summary>No payment with that id is on the trip (F6.R15).</summary>
    PaymentNotFound,
}

/// <summary>Result of marking a suggestion as paid. Refused means nothing was recorded (F6.R18).</summary>
public sealed record PaymentRecordResult(Payment? Payment, PaymentFailure? Failure)
{
    public bool Succeeded => Payment is not null;

    public static PaymentRecordResult Recorded(Payment payment) => new(payment, null);

    public static PaymentRecordResult Refused(PaymentFailure failure) => new(null, failure);
}

/// <summary>Result of deleting a payment. Refused means nothing was removed (F6.R8).</summary>
public sealed record PaymentDeleteResult(PaymentFailure? Failure)
{
    public bool Succeeded => Failure is null;

    public static PaymentDeleteResult Done() => new((PaymentFailure?)null);

    public static PaymentDeleteResult Refused(PaymentFailure failure) => new(failure);
}

/// <summary>A suggested payment with the names shown on screen (F6.R1).</summary>
public sealed record SettleUpSuggestion(Guid PayerPersonId, string PayerName, Guid PayeePersonId, string PayeeName, long AmountCents);

/// <summary>
/// The Settle up view data (F6.R1, R7, R11, R12, R14). <see cref="LeftToSettleCents"/> is the sum of positive balances (F5.R8).
/// <see cref="Progress"/> holds paid back, owed and the rounded-down percentage.
/// </summary>
public sealed record SettleUpView(
    IReadOnlyList<SettleUpSuggestion> Suggestions,
    IReadOnlyList<StoredPayment> Payments,
    Progress Progress,
    long LeftToSettleCents,
    long TotalSpentCents,
    int ExpenseCount);

/// <summary>Result of the settle-up read. Only <see cref="PaymentFailure.NotFound"/> can refuse it (F6.R15).</summary>
public sealed record SettleUpResult(SettleUpView? View, PaymentFailure? Failure)
{
    public bool Succeeded => View is not null;

    public static SettleUpResult Found(SettleUpView view) => new(view, null);

    public static SettleUpResult Refused(PaymentFailure failure) => new(null, failure);
}
