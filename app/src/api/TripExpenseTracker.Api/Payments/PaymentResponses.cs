using TripExpenseTracker.Core.Payments;

namespace TripExpenseTracker.Api.Payments;

/// <summary>Body of the 201 from <c>POST /api/trips/{tripId}/payments</c> (F6 Contract changes). Amounts are integer cents.</summary>
public sealed record PaymentRecordedResponse(
    Guid Id,
    Guid PayerPersonId,
    Guid PayeePersonId,
    long AmountCents,
    DateTimeOffset MarkedPaidAt)
{
    public static PaymentRecordedResponse From(Payment payment) => new(
        payment.Id,
        payment.PayerPersonId,
        payment.PayeePersonId,
        payment.AmountCents,
        payment.MarkedPaidAt);
}

/// <summary>Body of <c>GET /api/trips/{tripId}/settle-up</c> (F6 Contract changes). Integer cents; percentage is null when nothing is owed.</summary>
public sealed record SettleUpResponse(
    IReadOnlyList<SuggestionResponse> Suggestions,
    IReadOnlyList<PaymentListItemResponse> Payments,
    long PaidBackCents,
    long OwedCents,
    long LeftToSettleCents,
    int SuggestionCount,
    int? Percentage,
    long TotalSpentCents,
    int ExpenseCount)
{
    /// <summary>The suggestions keep the order the rule gave them (F6.R2). <c>suggestionCount</c> is their number (F6.R12).</summary>
    public static SettleUpResponse From(SettleUpView view) => new(
        view.Suggestions.Select(SuggestionResponse.From).ToList(),
        view.Payments.Select(PaymentListItemResponse.From).ToList(),
        view.Progress.PaidBackCents,
        view.Progress.OwedCents,
        view.LeftToSettleCents,
        view.Suggestions.Count,
        view.Progress.Percentage,
        view.TotalSpentCents,
        view.ExpenseCount);
}

/// <summary>One suggested payment: payer, payee and amount in integer cents (F6.R1).</summary>
public sealed record SuggestionResponse(
    Guid PayerPersonId,
    string PayerName,
    Guid PayeePersonId,
    string PayeeName,
    long AmountCents)
{
    public static SuggestionResponse From(SettleUpSuggestion suggestion) => new(
        suggestion.PayerPersonId,
        suggestion.PayerName,
        suggestion.PayeePersonId,
        suggestion.PayeeName,
        suggestion.AmountCents);
}

/// <summary>One recorded payment in the Payments made list (F6.R7). Newest first in the list.</summary>
public sealed record PaymentListItemResponse(
    Guid Id,
    Guid PayerPersonId,
    string PayerName,
    Guid PayeePersonId,
    string PayeeName,
    long AmountCents,
    DateTimeOffset MarkedPaidAt)
{
    public static PaymentListItemResponse From(StoredPayment stored) => new(
        stored.Payment.Id,
        stored.Payment.PayerPersonId,
        stored.PayerName,
        stored.Payment.PayeePersonId,
        stored.PayeeName,
        stored.Payment.AmountCents,
        stored.Payment.MarkedPaidAt);
}
