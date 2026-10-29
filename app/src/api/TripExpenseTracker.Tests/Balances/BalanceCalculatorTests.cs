using FluentAssertions;
using TripExpenseTracker.Core.Balances;

namespace TripExpenseTracker.Tests.Balances;

/// <summary>Balance formula, ordering and status (F5.R1, F5.R2, F5.R4, F5.AC1, F5.AC3, F5.AC5, F5.AC6, F5.AC14).</summary>
public class BalanceCalculatorTests
{
    private static readonly Guid Sam = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Alex = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Jo = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly BalancePerson SamP = new(Sam, "Sam", 1);
    private static readonly BalancePerson AlexP = new(Alex, "Alex", 2);
    private static readonly BalancePerson JoP = new(Jo, "Jo", 3);

    private static readonly IReadOnlyList<(Guid PersonId, long Seq)> Everyone = new[] { (Sam, 1L), (Alex, 2L), (Jo, 3L) };

    private static readonly IReadOnlyList<BalancePayment> NoPayments = Array.Empty<BalancePayment>();

    [Fact]
    public void Example_1_gives_owed_and_owes_balances_that_add_to_zero()
    {
        // Sam paid 90.00, Alex paid 30.00, all three share both (F5.AC1, F5.AC2).
        var expenses = new[]
        {
            new BalanceExpense(9000, Sam, Everyone),
            new BalanceExpense(3000, Alex, Everyone),
        };

        var balances = BalanceCalculator.Calculate(new[] { SamP, AlexP, JoP }, expenses, NoPayments);

        balances.Select(b => b.BalanceCents).Should().Equal(5000, -1000, -4000);
        balances.Select(b => b.Status).Should().Equal(BalanceStatus.Owed, BalanceStatus.Owes, BalanceStatus.Owes);
        balances.Sum(b => b.BalanceCents).Should().Be(0);
    }

    [Fact]
    public void Example_2_gives_the_leftover_cent_to_the_earliest_added()
    {
        // Sam paid 10.00, shared by Sam, Alex, Jo in that order (F5.AC5).
        var expenses = new[] { new BalanceExpense(1000, Sam, Everyone) };

        var balances = BalanceCalculator.Calculate(new[] { SamP, AlexP, JoP }, expenses, NoPayments);

        balances.Select(b => b.BalanceCents).Should().Equal(666, -333, -333);
        balances.Sum(b => b.BalanceCents).Should().Be(0);
    }

    [Fact]
    public void Payer_who_is_not_a_participant_is_owed_the_full_amount()
    {
        // Sam paid 30.00 for Alex and Jo, Sam not a participant (F5.AC14).
        var expenses = new[]
        {
            new BalanceExpense(3000, Sam, new[] { (Alex, 2L), (Jo, 3L) }),
        };

        var balances = BalanceCalculator.Calculate(new[] { SamP, AlexP, JoP }, expenses, NoPayments);

        balances.Select(b => b.BalanceCents).Should().Equal(3000, -1500, -1500);
    }

    [Fact]
    public void Person_in_no_expense_is_zero_settled_and_still_listed()
    {
        // F5.AC3, F5.AC8, F5.R6 edge case: a zero balance is "settled", never "owes 0".
        var expenses = new[] { new BalanceExpense(3000, Sam, new[] { (Sam, 1L), (Alex, 2L) }) };

        var balances = BalanceCalculator.Calculate(new[] { SamP, AlexP, JoP }, expenses, NoPayments);

        balances.Should().HaveCount(3);
        var jo = balances.Single(b => b.PersonId == Jo);
        jo.BalanceCents.Should().Be(0);
        jo.Status.Should().Be(BalanceStatus.Settled);
    }

    [Fact]
    public void Balances_are_returned_in_seq_order_even_when_people_were_given_in_another_order()
    {
        // F5.AC6: added Sam, then Alex, then Jo, but the list is passed as Jo, Sam, Alex.
        var balances = BalanceCalculator.Calculate(new[] { JoP, SamP, AlexP }, Array.Empty<BalanceExpense>(), NoPayments);

        balances.Select(b => b.Name).Should().Equal("Sam", "Alex", "Jo");
    }

    [Fact]
    public void No_expenses_gives_every_person_zero_settled()
    {
        var balances = BalanceCalculator.Calculate(new[] { SamP, AlexP, JoP }, Array.Empty<BalanceExpense>(), NoPayments);

        balances.Select(b => b.BalanceCents).Should().Equal(0, 0, 0);
        balances.Should().OnlyContain(b => b.Status == BalanceStatus.Settled);
    }

    [Fact]
    public void Payments_move_balances_so_the_formula_is_ready_for_F6()
    {
        // F5.R1: a payment from Alex to Sam moves Alex up and Sam down, and the total stays 0.
        var expenses = new[] { new BalanceExpense(9000, Sam, Everyone) };
        var payments = new[] { new BalancePayment(Alex, Sam, 1000) };

        var balances = BalanceCalculator.Calculate(new[] { SamP, AlexP, JoP }, expenses, payments);

        balances.Select(b => b.BalanceCents).Should().Equal(5000, -2000, -3000);
        balances.Sum(b => b.BalanceCents).Should().Be(0);
    }

    [Fact]
    public void Sum_of_balances_is_zero_for_many_random_expense_sets()
    {
        // F5.AC2, F5.R3. Fixed seed, so the run is repeatable and does not depend on the clock.
        var random = new Random(20261028);
        var people = Enumerable.Range(1, 5)
            .Select(i => new BalancePerson(Guid.NewGuid(), $"Person {i}", i))
            .ToList();

        for (var run = 0; run < 200; run++)
        {
            var expenses = new List<BalanceExpense>();
            var expenseCount = random.Next(0, 8);
            for (var e = 0; e < expenseCount; e++)
            {
                var payer = people[random.Next(people.Count)].Id;
                var participants = people
                    .Where(_ => random.Next(2) == 1)
                    .Select(p => (p.Id, p.Seq))
                    .ToList();
                if (participants.Count == 0)
                {
                    participants.Add((people[0].Id, people[0].Seq));
                }

                expenses.Add(new BalanceExpense(random.Next(1, 100_000), payer, participants));
            }

            var balances = BalanceCalculator.Calculate(people, expenses, NoPayments);

            balances.Sum(b => b.BalanceCents).Should().Be(0, "run {0} must balance", run);
        }
    }

    [Fact]
    public void Expense_naming_a_payer_not_on_the_trip_is_refused()
    {
        var stranger = Guid.NewGuid();
        var expenses = new[] { new BalanceExpense(1000, stranger, Everyone) };

        var act = () => BalanceCalculator.Calculate(new[] { SamP, AlexP, JoP }, expenses, NoPayments);

        act.Should().Throw<ArgumentException>();
    }
}
