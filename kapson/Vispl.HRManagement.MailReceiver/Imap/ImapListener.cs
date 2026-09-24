using System.Text;
using Microsoft.Extensions.Options;
using Vispl.HRManagement.MailReceiver.Smtp;

namespace Vispl.HRManagement.MailReceiver.Imap;

/// <summary>
/// IMAP4rev1 over the captured mailbox, so a mail client can browse what this server caught
/// without downloading and deleting it the way POP3 does.
///
/// A deliberately small subset — enough for Thunderbird, Outlook or a script to connect, open
/// INBOX and read: CAPABILITY, LOGIN, LIST, LSUB, STATUS, SELECT, EXAMINE, FETCH, UID FETCH,
/// SEARCH, UID SEARCH, STORE, EXPUNGE, CLOSE, NOOP, LOGOUT. There is one mailbox called INBOX
/// and **any credentials are accepted**: this is a development sink, and inventing accounts
/// nobody asked for would add ceremony without adding safety. That is why it binds to loopback.
///
/// Not implemented, and refused rather than faked: IDLE, APPEND, COPY, MOVE, sub-mailboxes,
/// TLS. A client that asks gets a clear NO instead of a silence it has to time out on.
/// </summary>
public sealed class ImapListener(
    Mailbox mailbox,
    IOptions<ImapOptions> options,
    ILogger<ImapListener> logger)
    : LineProtocolListener("IMAP",
        () => options.Value.Port,
        () => options.Value.ListenOnAllInterfaces,
        logger)
{
    private const string Capabilities = "IMAP4rev1 AUTH=PLAIN";

    protected override async Task ConverseAsync(
        StreamReader reader, StreamWriter writer, CancellationToken ct)
    {
        await writer.WriteLineAsync($"* OK [CAPABILITY {Capabilities}] Vispl HR captured mail ready");

        // The message list a SELECTed session works against. IMAP sequence numbers are
        // positions in this snapshot and must not shift under the client mid-session.
        List<CapturedMessage>? selected = null;
        var readOnly = false;

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
            {
                return;
            }

            // Every command is "<tag> <command> [arguments]".
            var space = line.IndexOf(' ');
            if (space <= 0)
            {
                await writer.WriteLineAsync("* BAD Missing command");
                continue;
            }

            var tag = line[..space];
            var rest = line[(space + 1)..].Trim();

            var commandEnd = rest.IndexOf(' ');
            var command = (commandEnd < 0 ? rest : rest[..commandEnd]).ToUpperInvariant();
            var arguments = commandEnd < 0 ? string.Empty : rest[(commandEnd + 1)..].Trim();

            // UID FETCH / UID SEARCH / UID STORE differ from their sequence-number forms only
            // in which identifier they take, so they are unwrapped here and handled together.
            var byUid = false;
            if (command == "UID")
            {
                byUid = true;
                var innerEnd = arguments.IndexOf(' ');
                command = (innerEnd < 0 ? arguments : arguments[..innerEnd]).ToUpperInvariant();
                arguments = innerEnd < 0 ? string.Empty : arguments[(innerEnd + 1)..].Trim();
            }

            switch (command)
            {
                case "CAPABILITY":
                    await writer.WriteLineAsync($"* CAPABILITY {Capabilities}");
                    await writer.WriteLineAsync($"{tag} OK CAPABILITY completed");
                    break;

                case "LOGIN":
                case "AUTHENTICATE":
                    await writer.WriteLineAsync($"{tag} OK LOGIN completed");
                    break;

                case "LIST":
                case "LSUB":
                    await writer.WriteLineAsync($"* {command} (\\HasNoChildren) \"/\" \"INBOX\"");
                    await writer.WriteLineAsync($"{tag} OK {command} completed");
                    break;

                case "STATUS":
                {
                    var all = mailbox.Ordered();
                    await writer.WriteLineAsync(
                        $"* STATUS \"INBOX\" (MESSAGES {all.Count} UNSEEN {mailbox.UnseenCount} " +
                        $"UIDNEXT {all.Count + 1} UIDVALIDITY 1 RECENT 0)");
                    await writer.WriteLineAsync($"{tag} OK STATUS completed");
                    break;
                }

                case "SELECT":
                case "EXAMINE":
                {
                    selected = mailbox.Ordered().ToList();
                    readOnly = command == "EXAMINE";

                    await writer.WriteLineAsync($"* {selected.Count} EXISTS");
                    await writer.WriteLineAsync("* 0 RECENT");
                    await writer.WriteLineAsync("* FLAGS (\\Seen \\Deleted)");
                    await writer.WriteLineAsync("* OK [PERMANENTFLAGS (\\Seen \\Deleted)] Limited");
                    await writer.WriteLineAsync("* OK [UIDVALIDITY 1] UIDs valid");
                    await writer.WriteLineAsync($"* OK [UIDNEXT {selected.Count + 1}] Predicted next UID");
                    await writer.WriteLineAsync(
                        $"{tag} OK [{(readOnly ? "READ-ONLY" : "READ-WRITE")}] {command} completed");
                    break;
                }

                case "FETCH":
                    if (selected is null)
                    {
                        await writer.WriteLineAsync($"{tag} NO Select a mailbox first");
                        break;
                    }

                    await FetchAsync(writer, selected, arguments, byUid);
                    await writer.WriteLineAsync($"{tag} OK FETCH completed");
                    break;

                case "SEARCH":
                {
                    if (selected is null)
                    {
                        await writer.WriteLineAsync($"{tag} NO Select a mailbox first");
                        break;
                    }

                    // Only the searches a client runs on opening a folder are answered
                    // precisely; anything else returns everything, which is honest for a
                    // mailbox this size and never hides a message from somebody looking.
                    var hits = arguments.Contains("UNSEEN", StringComparison.OrdinalIgnoreCase)
                        ? selected.Where(item => !mailbox.IsSeen(item.Id))
                        : selected;

                    var numbers = hits.Select(item =>
                        byUid ? selected.IndexOf(item) + 1 : selected.IndexOf(item) + 1);

                    await writer.WriteLineAsync($"* SEARCH {string.Join(' ', numbers)}".TrimEnd());
                    await writer.WriteLineAsync($"{tag} OK SEARCH completed");
                    break;
                }

                case "STORE":
                {
                    if (selected is null || readOnly)
                    {
                        await writer.WriteLineAsync($"{tag} NO Cannot store here");
                        break;
                    }

                    // Only \Seen is honoured. \Deleted is accepted and acted on at EXPUNGE.
                    foreach (var message in Range(selected, arguments.Split(' ').FirstOrDefault() ?? "1:*"))
                    {
                        if (arguments.Contains("\\Seen", StringComparison.OrdinalIgnoreCase))
                        {
                            mailbox.MarkSeen(message.Id);
                        }

                        if (arguments.Contains("\\Deleted", StringComparison.OrdinalIgnoreCase))
                        {
                            pendingDeletes.Add(message.Id);
                        }
                    }

                    await writer.WriteLineAsync($"{tag} OK STORE completed");
                    break;
                }

                case "EXPUNGE":
                case "CLOSE":
                {
                    foreach (var id in pendingDeletes)
                    {
                        mailbox.Remove(id);
                    }

                    if (pendingDeletes.Count > 0)
                    {
                        Log.LogInformation("IMAP session removed {Count} message(s).", pendingDeletes.Count);
                    }

                    pendingDeletes.Clear();
                    selected = command == "CLOSE" ? null : selected;
                    await writer.WriteLineAsync($"{tag} OK {command} completed");
                    break;
                }

                case "NOOP":
                    await writer.WriteLineAsync($"{tag} OK NOOP completed");
                    break;

                case "LOGOUT":
                    await writer.WriteLineAsync("* BYE Vispl HR captured mail signing off");
                    await writer.WriteLineAsync($"{tag} OK LOGOUT completed");
                    return;

                default:
                    // A clear refusal beats a silence the client has to time out on.
                    Log.LogWarning("Refused unsupported IMAP command {Command}.", command);
                    await writer.WriteLineAsync($"{tag} NO {command} is not supported by this server");
                    break;
            }
        }
    }

    private readonly HashSet<string> pendingDeletes = [];

    private async Task FetchAsync(
        StreamWriter writer, List<CapturedMessage> selected, string arguments, bool byUid)
    {
        var split = arguments.IndexOf(' ');
        var set = split < 0 ? arguments : arguments[..split];
        var wanted = (split < 0 ? string.Empty : arguments[(split + 1)..]).ToUpperInvariant();

        foreach (var message in Range(selected, set))
        {
            var number = selected.IndexOf(message) + 1;
            var raw = message.Raw;
            var bytes = Encoding.UTF8.GetByteCount(raw);
            var flags = mailbox.IsSeen(message.Id) ? "\\Seen" : string.Empty;

            // Whole-body fetches are the ones that actually deliver a message; the rest are a
            // client deciding what to download, and are answered from the same raw source.
            var wantsBody = wanted.Contains("BODY[]") || wanted.Contains("RFC822") ||
                            wanted.Contains("BODY.PEEK[]");

            var head = $"* {number} FETCH (UID {number} FLAGS ({flags})";

            if (wantsBody)
            {
                await writer.WriteLineAsync($"{head} RFC822.SIZE {bytes} BODY[] {{{bytes}}}");
                await writer.WriteAsync(raw);
                await writer.WriteLineAsync(")");

                // BODY.PEEK is explicitly a read that does not mark as seen; BODY[] does.
                if (!wanted.Contains("PEEK"))
                {
                    mailbox.MarkSeen(message.Id);
                }
            }
            else
            {
                var envelope =
                    $"(\"{message.ReceivedUtc:R}\" \"{Quote(message.Subject)}\" " +
                    $"((NIL NIL \"{Quote(Local(message.From))}\" \"{Quote(Domain(message.From))}\")) " +
                    "NIL NIL " +
                    $"((NIL NIL \"{Quote(Local(message.To.FirstOrDefault() ?? ""))}\" " +
                    $"\"{Quote(Domain(message.To.FirstOrDefault() ?? ""))}\")) " +
                    $"NIL NIL NIL \"<{message.Id}@vispl-hr.local>\")";

                await writer.WriteLineAsync(
                    $"{head} RFC822.SIZE {bytes} INTERNALDATE \"{message.ReceivedUtc:dd-MMM-yyyy HH:mm:ss zzz}\" " +
                    $"ENVELOPE {envelope})");
            }
        }
    }

    /// <summary>
    /// Resolves an IMAP message set — "1", "2:5", "3:*", "1,4,7" or "1:*" — to messages.
    /// Sequence numbers and UIDs are the same thing here because this mailbox never renumbers
    /// within a session.
    /// </summary>
    private static IEnumerable<CapturedMessage> Range(List<CapturedMessage> selected, string set)
    {
        if (string.IsNullOrWhiteSpace(set))
        {
            yield break;
        }

        foreach (var part in set.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = part.IndexOf(':');

            if (colon < 0)
            {
                if (int.TryParse(part, out var only) && only >= 1 && only <= selected.Count)
                {
                    yield return selected[only - 1];
                }

                continue;
            }

            var fromText = part[..colon];
            var toText = part[(colon + 1)..];

            var from = fromText == "*" ? selected.Count : int.TryParse(fromText, out var f) ? f : 1;
            var to = toText == "*" ? selected.Count : int.TryParse(toText, out var t) ? t : selected.Count;

            if (from > to)
            {
                (from, to) = (to, from);
            }

            for (var index = Math.Max(1, from); index <= Math.Min(selected.Count, to); index++)
            {
                yield return selected[index - 1];
            }
        }
    }

    private static string Quote(string value) => value.Replace("\"", "'").Replace("\\", "/");

    private static string Local(string address)
    {
        var open = address.LastIndexOf('<');
        var close = address.LastIndexOf('>');
        var bare = open >= 0 && close > open ? address[(open + 1)..close] : address;
        var at = bare.IndexOf('@');
        return at < 0 ? bare : bare[..at];
    }

    private static string Domain(string address)
    {
        var open = address.LastIndexOf('<');
        var close = address.LastIndexOf('>');
        var bare = open >= 0 && close > open ? address[(open + 1)..close] : address;
        var at = bare.IndexOf('@');
        return at < 0 ? "vispl-hr.local" : bare[(at + 1)..];
    }
}

public sealed class ImapOptions
{
    public const string SectionName = "Imap";

    /// <summary>1143 rather than 143: the privileged port needs root and is often taken.</summary>
    public int Port { get; set; } = 1143;

    /// <summary>
    /// Loopback by default. Any password opens the one mailbox, so exposing this to a network
    /// is not something to do by accident.
    /// </summary>
    public bool ListenOnAllInterfaces { get; set; }
}
