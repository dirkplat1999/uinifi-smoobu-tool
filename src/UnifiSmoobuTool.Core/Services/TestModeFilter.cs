using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Services;

/// <summary>
/// When test mode is enabled, only reservations matching a configured phone number, email, or
/// guest name should be touched by automation, so real guests are never disturbed by a test run.
/// </summary>
public static class TestModeFilter
{
    public static bool IsTestReservation(Reservation reservation, IReadOnlyCollection<TestModeRule> rules)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(rules);

        foreach (var rule in rules)
        {
            var matches = rule.Type switch
            {
                TestModeRuleType.PhoneNumber => MatchesPhone(reservation.GuestPhone, rule.Value),
                TestModeRuleType.Email => MatchesEmail(reservation.GuestEmail, rule.Value),
                TestModeRuleType.GuestName => reservation.GuestFullName.Contains(
                    rule.Value, StringComparison.OrdinalIgnoreCase),
                _ => false,
            };

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Should this reservation be processed at all, given the global test-mode toggle?</summary>
    public static bool ShouldProcess(Reservation reservation, bool testModeEnabled, IReadOnlyCollection<TestModeRule> rules)
        => !testModeEnabled || IsTestReservation(reservation, rules);

    private static bool MatchesPhone(string? guestPhone, string ruleValue)
        => guestPhone is not null && NormalizePhone(guestPhone).Contains(NormalizePhone(ruleValue), StringComparison.Ordinal);

    private static bool MatchesEmail(string? guestEmail, string ruleValue)
        => guestEmail is not null && string.Equals(guestEmail.Trim(), ruleValue.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string NormalizePhone(string phone) => new(phone.Where(char.IsDigit).ToArray());
}
