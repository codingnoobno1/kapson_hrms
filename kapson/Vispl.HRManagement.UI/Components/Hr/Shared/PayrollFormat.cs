using System.Globalization;

namespace Vispl.HRManagement.UI.Components.Hr.Shared;

/// <summary>Display rules shared by the payroll page and its modals.</summary>
public static class PayrollFormat
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    public static string Rupees(decimal amount) => string.Format(India, "₹{0:N0}", amount);

    public static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => char.ToUpperInvariant(part[0])));

    public static int AvatarTone(string name) => Math.Abs(name.Aggregate(17, (hash, character) => hash * 31 + character)) % 6;

    /// <summary>"HDFC Bank" → HDFC, "State Bank of India" → SBI: the chip label beside a masked number.</summary>
    public static string BankShort(string bankName)
    {
        var words = bankName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => !string.Equals(word, "of", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return words.Count >= 3
            ? string.Concat(words.Take(3).Select(word => char.ToUpperInvariant(word[0])))
            : (words.FirstOrDefault() ?? bankName).ToUpperInvariant();
    }

    public static string DateInput(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateOnly? ParseDateInput(object? value) =>
        DateOnly.TryParseExact(value?.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
}
