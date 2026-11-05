namespace MediClinic.Domain;

internal static class Guard
{
    internal static string Required(string value, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        return value.Trim();
    }

    internal static void TimeRange(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (endsAt <= startsAt)
            throw new ArgumentException("End must be after start.", nameof(endsAt));
    }
}
