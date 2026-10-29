using TripExpenseTracker.Core.Auth;
using TripExpenseTracker.Core.Balances;
using TripExpenseTracker.Core.Trips;

namespace TripExpenseTracker.Core.Payments;

/// <summary>
/// Mark as paid and delete payment (F6.R5, R6, R8, R15, R18). Every write takes the trip lock first, inside one
/// transaction, so a payment is recorded only for a suggestion that is still current (F6.R18). A foreign or missing
/// trip gives <see cref="PaymentFailure.NotFound"/> for every call (F6.R15).
/// </summary>
public sealed class PaymentService
{
    private readonly IPaymentDao _payments;
    private readonly IBalanceDao _balances;
    private readonly ITripDao _trips;
    private readonly ITripLock _lock;
    private readonly IAuthTransaction _transaction;
    private readonly TimeProvider _time;

    public PaymentService(
        IPaymentDao payments,
        IBalanceDao balances,
        ITripDao trips,
        ITripLock tripLock,
        IAuthTransaction transaction,
        TimeProvider time)
    {
        _payments = payments;
        _balances = balances;
        _trips = trips;
        _lock = tripLock;
        _transaction = transaction;
        _time = time;
    }

    /// <summary>
    /// Records the payment in full if the payer, payee and amount are a current suggestion (F6.R6, R11, R18).
    /// Otherwise nothing is recorded and the refusal says why.
    /// </summary>
    public async Task<PaymentRecordResult> MarkPaidAsync(
        Guid owner,
        Guid tripId,
        Guid payerPersonId,
        Guid payeePersonId,
        long amountCents,
        CancellationToken cancellationToken)
    {
        var rule = PaymentRules.Check(payerPersonId, payeePersonId, amountCents);
        if (rule is not null)
        {
            return PaymentRecordResult.Refused(rule == PaymentRuleFailure.PayerPayeeSame
                ? PaymentFailure.PayerPayeeSame
                : PaymentFailure.AmountNotPositive);
        }

        try
        {
            return await _transaction.RunAsync<PaymentRecordResult>(async token =>
            {
                if (!await _lock.LockAsync(owner, tripId, token))
                {
                    return PaymentRecordResult.Refused(PaymentFailure.NotFound);
                }

                var snapshot = await _balances.GetSnapshotAsync(owner, tripId, token);
                if (!snapshot.People.Any(p => p.Id == payerPersonId))
                {
                    return PaymentRecordResult.Refused(PaymentFailure.PayerNotOnTrip);
                }

                if (!snapshot.People.Any(p => p.Id == payeePersonId))
                {
                    return PaymentRecordResult.Refused(PaymentFailure.PayeeNotOnTrip);
                }

                var payments = await _payments.ListAsync(owner, tripId, token);
                var balances = SettleUpMath.Balances(snapshot, payments);
                var suggestions = SettleUpMath.Suggest(balances, snapshot.People);

                var matches = suggestions.Any(s =>
                    s.PayerPersonId == payerPersonId &&
                    s.PayeePersonId == payeePersonId &&
                    s.AmountCents == amountCents);
                if (!matches)
                {
                    return PaymentRecordResult.Refused(PaymentFailure.SuggestionsChanged);
                }

                var payment = new Payment(Guid.NewGuid(), tripId, payerPersonId, payeePersonId, amountCents, _time.GetUtcNow());
                if (!await _payments.InsertAsync(owner, payment, token))
                {
                    // Throwing rolls back the transaction, so nothing is kept.
                    throw new PaymentRefusedException(PaymentFailure.NotFound);
                }

                return PaymentRecordResult.Recorded(payment);
            }, cancellationToken);
        }
        catch (PaymentRefusedException refused)
        {
            return PaymentRecordResult.Refused(refused.Failure);
        }
    }

    /// <summary>Deletes the owner's payment on the trip. Not found when the trip or the payment is not the owner's (F6.R8, R15).</summary>
    public Task<PaymentDeleteResult> DeleteAsync(Guid owner, Guid tripId, Guid paymentId, CancellationToken cancellationToken) =>
        _transaction.RunAsync<PaymentDeleteResult>(async token =>
        {
            if (!await _lock.LockAsync(owner, tripId, token))
            {
                return PaymentDeleteResult.Refused(PaymentFailure.NotFound);
            }

            return await _payments.DeleteAsync(owner, tripId, paymentId, token)
                ? PaymentDeleteResult.Done()
                : PaymentDeleteResult.Refused(PaymentFailure.PaymentNotFound);
        }, cancellationToken);

    /// <summary>Carries a refusal out of the transaction so it rolls back. Never leaves the service.</summary>
    private sealed class PaymentRefusedException(PaymentFailure failure) : Exception
    {
        public PaymentFailure Failure { get; } = failure;
    }
}
