namespace TripExpenseTracker.Core.Auth;

/// <summary>One-way password hashing (F1.R3). Implemented in the API project, where ASP.NET is available.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
