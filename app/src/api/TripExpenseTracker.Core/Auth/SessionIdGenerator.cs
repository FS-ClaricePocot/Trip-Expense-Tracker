using System.Buffers.Text;
using System.Security.Cryptography;

namespace TripExpenseTracker.Core.Auth;

/// <summary>Creates session IDs: 32 random bytes, base64url encoded (F1.R6). Not derived from user data.</summary>
public static class SessionIdGenerator
{
    public const int ByteLength = 32;

    public static string Create() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ByteLength));
}
