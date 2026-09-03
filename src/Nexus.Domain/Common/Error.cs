namespace Nexus.Domain.Common;

public sealed record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "The specified result value is null.");
    public static readonly Error NotFound = new("Error.NotFound", "The requested resource was not found.");
    public static readonly Error Unauthorized = new("Error.Unauthorized", "You are not authorized to perform this operation.");
    public static readonly Error Conflict = new("Error.Conflict", "A conflict occurred with existing data.");
    public static readonly Error Validation = new("Error.Validation", "One or more validation errors occurred.");
}
