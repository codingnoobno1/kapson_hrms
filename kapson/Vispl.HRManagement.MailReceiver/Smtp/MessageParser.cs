using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;

namespace Vispl.HRManagement.MailReceiver.Smtp;

/// <summary>
/// Pulls the readable parts out of an RFC 5322 message.
///
/// Only as much parsing as an inbox needs: headers, the text and HTML alternatives, and the
/// links. It is not a general mail parser and does not pretend to be — the raw text is kept
/// alongside so anything this gets wrong can still be read.
/// </summary>
public static partial class MessageParser
{
    public static CapturedMessage Parse(string id, string from, IReadOnlyList<string> to, string raw)
    {
        var (headers, body) = Split(raw);

        var subject = Decode(headers.GetValueOrDefault("subject", "(no subject)"));
        var contentType = headers.GetValueOrDefault("content-type", "text/plain");
        var encoding = headers.GetValueOrDefault("content-transfer-encoding", "7bit");

        string? text = null;
        string? html = null;

        if (contentType.Contains("multipart/", StringComparison.OrdinalIgnoreCase))
        {
            var boundary = BoundaryOf(contentType);

            if (boundary is not null)
            {
                foreach (var part in body.Split("--" + boundary))
                {
                    var (partHeaders, partBody) = Split(part.TrimStart('\r', '\n'));
                    var partType = partHeaders.GetValueOrDefault("content-type", "");
                    var partEncoding = partHeaders.GetValueOrDefault("content-transfer-encoding", "7bit");
                    var decoded = DecodeBody(partBody, partEncoding);

                    if (partType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                    {
                        html ??= decoded;
                    }
                    else if (partType.Contains("text/plain", StringComparison.OrdinalIgnoreCase))
                    {
                        text ??= decoded;
                    }
                }
            }
        }
        else if (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            html = DecodeBody(body, encoding);
        }
        else
        {
            text = DecodeBody(body, encoding);
        }

        return new CapturedMessage(
            id,
            DateTimeOffset.UtcNow,
            headers.GetValueOrDefault("from", from),
            to,
            subject,
            text?.Trim(),
            html?.Trim(),
            LinksIn((text ?? "") + "\n" + (html ?? "")),
            raw);
    }

    /// <summary>Headers to the first blank line, body after it.</summary>
    private static (Dictionary<string, string> Headers, string Body) Split(string message)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var separator = message.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var width = 4;

        if (separator < 0)
        {
            separator = message.IndexOf("\n\n", StringComparison.Ordinal);
            width = 2;
        }

        if (separator < 0)
        {
            return (headers, message);
        }

        var raw = message[..separator].Replace("\r\n", "\n");
        string? name = null;
        var value = new StringBuilder();

        foreach (var line in raw.Split('\n'))
        {
            // A line starting with whitespace continues the one before it.
            if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t') && name is not null)
            {
                value.Append(' ').Append(line.Trim());
                continue;
            }

            if (name is not null)
            {
                headers[name] = value.ToString();
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                name = null;
                continue;
            }

            name = line[..colon].Trim();
            value.Clear().Append(line[(colon + 1)..].Trim());
        }

        if (name is not null)
        {
            headers[name] = value.ToString();
        }

        return (headers, message[(separator + width)..]);
    }

    private static string? BoundaryOf(string contentType)
    {
        var match = BoundaryPattern().Match(contentType);
        return match.Success ? match.Groups[1].Value.Trim('"') : null;
    }

    private static string DecodeBody(string body, string encoding)
    {
        if (encoding.Contains("base64", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(
                    body.Replace("\r", "").Replace("\n", "").Trim()));
            }
            catch (FormatException)
            {
                return body;
            }
        }

        if (encoding.Contains("quoted-printable", StringComparison.OrdinalIgnoreCase))
        {
            return DecodeQuotedPrintable(body);
        }

        return body;
    }

    private static string DecodeQuotedPrintable(string body)
    {
        var output = new StringBuilder(body.Length);

        for (var index = 0; index < body.Length; index++)
        {
            if (body[index] != '=')
            {
                output.Append(body[index]);
                continue;
            }

            // "=" at end of line is a soft break and disappears.
            if (index + 2 < body.Length && body[index + 1] == '\r' && body[index + 2] == '\n')
            {
                index += 2;
                continue;
            }

            if (index + 1 < body.Length && body[index + 1] == '\n')
            {
                index += 1;
                continue;
            }

            if (index + 2 < body.Length &&
                byte.TryParse(body.Substring(index + 1, 2),
                    System.Globalization.NumberStyles.HexNumber, null, out var value))
            {
                output.Append((char)value);
                index += 2;
                continue;
            }

            output.Append(body[index]);
        }

        return output.ToString();
    }

    /// <summary>
    /// Decodes an encoded-word header such as <c>=?utf-8?B?...?=</c>. Subjects with a name in
    /// them arrive this way and would otherwise read as gibberish in the list.
    /// </summary>
    private static string Decode(string header)
    {
        if (!header.Contains("=?", StringComparison.Ordinal))
        {
            return header;
        }

        return EncodedWordPattern().Replace(header, match =>
        {
            var charset = match.Groups[1].Value;
            var kind = match.Groups[2].Value.ToUpperInvariant();
            var payload = match.Groups[3].Value;

            try
            {
                var bytes = kind == "B"
                    ? Convert.FromBase64String(payload)
                    : Encoding.ASCII.GetBytes(DecodeQuotedPrintable(payload.Replace('_', ' ')));

                return Encoding.GetEncoding(charset).GetString(bytes);
            }
            catch (Exception)
            {
                return match.Value;
            }
        });
    }

    /// <summary>
    /// Every http(s) link, de-duplicated, in the order they appear. Trailing punctuation is
    /// trimmed because a URL at the end of a sentence is not part of the sentence.
    ///
    /// Entities are decoded first. The same link in the text and HTML parts otherwise appears
    /// twice, once with a literal <c>&amp;</c> — two entries for one destination, and the
    /// encoded one is not clickable.
    /// </summary>
    private static IReadOnlyList<string> LinksIn(string body) =>
        LinkPattern().Matches(System.Net.WebUtility.HtmlDecode(body))
            .Select(match => match.Value.TrimEnd('.', ',', ')', '"', '\'', '<', '>', ';'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    [GeneratedRegex(@"boundary\s*=\s*(""[^""]+""|[^;\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex BoundaryPattern();

    [GeneratedRegex(@"=\?([^?]+)\?([BbQq])\?([^?]*)\?=")]
    private static partial Regex EncodedWordPattern();

    [GeneratedRegex(@"https?://[^\s""'<>]+")]
    private static partial Regex LinkPattern();
}
