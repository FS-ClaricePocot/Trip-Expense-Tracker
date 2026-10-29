namespace TripExpenseTracker.Core.Payments;

/// <summary>What is wrong with a payment's payer and amount, before any trip lookup (F6.R6).</summary>
public enum PaymentRuleFailure
{
    /// <summary>The payer and payee are the same person. Code <c>payer_payee_same</c>.</summary>
    PayerPayeeSame,

    /// <summary>The amount is 0 or less. Code <c>amount_not_positive</c>.</summary>
    AmountNotPositive,
}

/// <summary>Pure payment checks (F6.R6). No I/O, no clock.</summary>
public static class PaymentRules
{
    /// <summary>Returns the first failing rule, or null when the payer, payee and amount are valid.</summary>
    public static PaymentRuleFailure? Check(Guid payerPersonId, Guid payeePersonId, long amountCents)
    {
        if (payerPersonId == payeePersonId)
        {
            return PaymentRuleFailure.PayerPayeeSame;
        }

        if (amountCents <= 0)
        {
            return PaymentRuleFailure.AmountNotPositive;
        }

        return null;
    }
}
