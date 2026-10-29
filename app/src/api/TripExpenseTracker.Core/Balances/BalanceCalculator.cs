using TripExpenseTracker.Core.Expenses;

namespace TripExpenseTracker.Core.Balances;

/// <summary>A person on the trip as balances see them: id, name, and order (<c>people.seq</c>, F3.R5).</summary>
public sealed record BalancePerson(Guid Id, string Name, long Seq);

/// <summary>An expense as balances see it: the amount in cents, the payer, and the participants with their order.</summary>
public sealed record BalanceExpense(long AmountCents, Guid PayerId, IReadOnlyList<(Guid PersonId, long Seq)> Participants);

/// <summary>A payment from one person to another. F5 always receives an empty list; F6 creates payments (F5.R1).</summary>
public sealed record BalancePayment(Guid FromPersonId, Guid ToPersonId, long AmountCents);

/// <summary>
/// Balance formula (F5.R1): balance = paid − sum(shares) + payments made − payments received.
/// Shares come from <see cref="ExpenseShares"/>, so the leftover-cent rule is not repeated (F4.R7).
/// Pure logic, no I/O.
/// </summary>
public static class BalanceCalculator
{
    /// <summary>
    /// One balance per person in <c>seq</c> order, including people with a zero balance (F5.R4).
    /// Throws <see cref="ArgumentException"/> if an expense or payment names a person who is not on the trip.
    /// </summary>
    public static IReadOnlyList<PersonBalance> Calculate(
        IReadOnlyList<BalancePerson> people,
        IReadOnlyList<BalanceExpense> expenses,
        IReadOnlyList<BalancePayment> payments)
    {
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(expenses);
        ArgumentNullException.ThrowIfNull(payments);

        var known = people.Select(p => p.Id).ToHashSet();
        var net = people.ToDictionary(p => p.Id, _ => 0L);

        foreach (var expense in expenses)
        {
            if (!known.Contains(expense.PayerId))
            {
                throw new ArgumentException("An expense names a payer who is not on the trip.", nameof(expenses));
            }

            net[expense.PayerId] += expense.AmountCents;

            foreach (var share in ExpenseShares.Compute(expense.AmountCents, expense.Participants))
            {
                if (!known.Contains(share.PersonId))
                {
                    throw new ArgumentException("An expense names a participant who is not on the trip.", nameof(expenses));
                }

                net[share.PersonId] -= share.Cents;
            }
        }

        foreach (var payment in payments)
        {
            if (!known.Contains(payment.FromPersonId) || !known.Contains(payment.ToPersonId))
            {
                throw new ArgumentException("A payment names a person who is not on the trip.", nameof(payments));
            }

            net[payment.FromPersonId] += payment.AmountCents;
            net[payment.ToPersonId] -= payment.AmountCents;
        }

        return people
            .OrderBy(p => p.Seq)
            .Select(p => new PersonBalance(p.Id, p.Name, net[p.Id]))
            .ToList();
    }
}
