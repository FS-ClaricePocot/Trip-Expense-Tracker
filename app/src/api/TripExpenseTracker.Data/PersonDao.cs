using Dapper;
using Npgsql;
using TripExpenseTracker.Core.People;

namespace TripExpenseTracker.Data;

/// <summary>
/// Dapper implementation of <see cref="IPersonDao"/>. Every statement joins trips and filters on owner_user_id, so a
/// trip the user does not own looks the same as a missing one (F3.R10). Parameters only; no SQL is built from input.
/// </summary>
public sealed class PersonDao : IPersonDao
{
    // Ordered by seq, not added_at, so a person removed and added again goes to the end (F3.R5).
    private const string ListSql = """
        SELECT p.id AS "Id", p.trip_id AS "TripId", p.name AS "Name", p.added_at AS "AddedAt"
        FROM people p
        JOIN trips t ON t.id = p.trip_id
        WHERE t.id = @TripId AND t.owner_user_id = @Owner
        ORDER BY p.seq
        """;

    // Zero rows means the trip is not the owner's or does not exist (F3.R10).
    private const string AddSql = """
        INSERT INTO people (id, trip_id, name, added_at)
        SELECT CAST(@Id AS uuid), t.id, CAST(@Name AS text), CAST(@AddedAt AS timestamptz)
        FROM trips t
        WHERE t.id = @TripId AND t.owner_user_id = @Owner
        """;

    // The owner join in the USING clause means a person on another user's trip matches no row (F3.R10).
    private const string DeleteSql = """
        DELETE FROM people p
        USING trips t
        WHERE p.id = @PersonId AND p.trip_id = t.id AND t.id = @TripId AND t.owner_user_id = @Owner
        """;

    private const string UniqueViolation = "23505";
    private const string ForeignKeyViolation = "23503";
    private const string NameIndex = "ux_people_trip_name";

    private readonly IUnitOfWork _unitOfWork;

    public PersonDao(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<Person>> ListAsync(Guid owner, Guid tripId, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            ListSql,
            new { Owner = owner, TripId = tripId },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<PersonRow>(command);
        return rows.Select(row => row.ToPerson()).ToList();
    }

    public async Task<PersonAddOutcome> AddAsync(Guid owner, Guid tripId, Person person, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            AddSql,
            new
            {
                Owner = owner,
                TripId = tripId,
                Id = person.Id,
                Name = person.Name,
                AddedAt = person.AddedAt.UtcDateTime,
            },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        try
        {
            var rows = await connection.ExecuteAsync(command);
            return rows > 0 ? PersonAddOutcome.Added : PersonAddOutcome.TripNotFound;
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation && ex.ConstraintName == NameIndex)
        {
            // Same name as someone on the trip, ignoring capitals (F3.R3). Also covers two adds that race.
            return PersonAddOutcome.Duplicate;
        }
    }

    public async Task<PersonDeleteOutcome> DeleteAsync(Guid owner, Guid tripId, Guid personId, CancellationToken cancellationToken)
    {
        var connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            DeleteSql,
            new { Owner = owner, TripId = tripId, PersonId = personId },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);

        try
        {
            var rows = await connection.ExecuteAsync(command);
            return rows > 0 ? PersonDeleteOutcome.Removed : PersonDeleteOutcome.NotFound;
        }
        catch (PostgresException ex) when (ex.SqlState == ForeignKeyViolation)
        {
            // An expense or payment still refers to the person. The backstop for the race in F3.R7 and F3.AC8.
            return PersonDeleteOutcome.InUse;
        }
    }

    /// <summary>
    /// Reads a row with the types Npgsql returns. timestamptz comes back as DateTime (UTC), not DateTimeOffset,
    /// so the row is converted to the domain type here (same as TripDao).
    /// </summary>
    internal sealed record PersonRow(Guid Id, Guid TripId, string Name, DateTime AddedAt)
    {
        public Person ToPerson() => new(Id, TripId, Name, new DateTimeOffset(DateTime.SpecifyKind(AddedAt, DateTimeKind.Utc)));
    }
}
