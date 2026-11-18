namespace Vispl.HRManagement.VO.Employee;

/// <summary>
/// One spelling for an employee's phone number, so the unique (TenantKey, Phone) index catches
/// the same number typed differently: "98765 43210", "098765-43210" and "+91 98765 43210" are all
/// stored as "+919876543210". A bare 10-digit number is taken as Indian (+91).
/// </summary>
public static class EmployeePhone
{
    /// <summary>The stored form, or null for an empty value. Throws <see cref="FormatException"/> for something that is not a phone number.</summary>
    public static string? Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var text = phone.Trim();
        var hasPlus = text.StartsWith('+') || text.StartsWith("00", StringComparison.Ordinal);

        foreach (var character in text)
        {
            if (!char.IsDigit(character) && character is not (' ' or '-' or '(' or ')' or '.' or '+'))
            {
                throw new FormatException($"\"{phone}\" is not a phone number. Use digits only, for example +91 98765 43210.");
            }
        }

        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());

        if (text.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }
        else if (!hasPlus && digits.Length == 11 && digits[0] == '0')
        {
            digits = "91" + digits[1..];      // 0XXXXXXXXXX — Indian trunk prefix
        }
        else if (!hasPlus && digits.Length == 10)
        {
            digits = "91" + digits;           // XXXXXXXXXX — Indian mobile
        }

        if (digits.Length is < 11 or > 15)
        {
            throw new FormatException($"\"{phone}\" is not a full phone number. Enter 10 digits, or the number with its country code.");
        }

        return "+" + digits;
    }
}
