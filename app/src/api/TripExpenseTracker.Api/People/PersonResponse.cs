using TripExpenseTracker.Core.People;

namespace TripExpenseTracker.Api.People;

/// <summary>A person in the list, and the body of an added person (F3 Contract changes).</summary>
public sealed record PersonResponse(Guid Id, string Name, DateTimeOffset AddedAt, bool InExpense, bool InPayment)
{
    /// <summary>A listed person with the usage flags from the service (F3.R7).</summary>
    public static PersonResponse From(PersonWithUsage person) =>
        new(person.Person.Id, person.Person.Name, person.Person.AddedAt, person.InExpense, person.InPayment);

    /// <summary>A person just added. Nothing can use them yet, so both flags are false.</summary>
    public static PersonResponse Added(Person person) =>
        new(person.Id, person.Name, person.AddedAt, InExpense: false, InPayment: false);
}
