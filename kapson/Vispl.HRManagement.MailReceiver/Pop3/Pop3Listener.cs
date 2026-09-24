using System.Text;
using Microsoft.Extensions.Options;
using Vispl.HRManagement.MailReceiver.Smtp;

namespace Vispl.HRManagement.MailReceiver.Pop3;

/// <summary>
/// POP3 over the captured mailbox, so a real mail client can download what this server caught.
///
/// Enough of RFC 1939 for any client to complete a session: USER, PASS, STAT, LIST, UIDL, TOP,
/// RETR, DELE, RSET, NOOP, QUIT. **Any credentials are accepted** — this is a development sink
/// with one shared mailbox, and pretending otherwise would only mean inventing accounts nobody
/// asked for. That is also why it binds to loopback by default.
///
/// Deletions are marked during a session and applied at QUIT, which is what the protocol
/// specifies and what clients rely on when they are set to "remove from server".
/// </summary>
public sealed class Pop3Listener(
    Mailbox mailbox,
    IOptions<Pop3Options> options,
    ILogger<Pop3Listener> logger)
    : LineProtocolListener("POP3",
        () => options.Value.Port,
        () => options.Value.ListenOnAllInterfaces,
        logger)
{
    protected override async Task ConverseAsync(
        StreamReader reader, StreamWriter writer, CancellationToken ct)
    {
        await writer.WriteLineAsync("+OK Vispl HR captured mail ready");

        // The snapshot a POP3 session works against. The protocol numbers messages 1..n and
        // those numbers must not shift underneath the client, so the list is taken once at
        // the start rather than re-read per command.
        var session = mailbox.Ordered().ToList();
        var deleted = new HashSet<string>();

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
            {
                return;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var verb = parts.Length == 0 ? string.Empty : parts[0].ToUpperInvariant();

            switch (verb)
            {
                case "USER":
                case "PASS":
                    // Accepted whatever they are. One mailbox, no accounts.
                    await writer.WriteLineAsync("+OK");
                    break;

                case "CAPA":
                    await writer.WriteLineAsync("+OK Capability list follows");
                    await writer.WriteLineAsync("TOP");
                    await writer.WriteLineAsync("UIDL");
                    await writer.WriteLineAsync("USER");
                    await writer.WriteLineAsync(".");
                    break;

                case "STAT":
                    var live = session.Where(item => !deleted.Contains(item.Id)).ToList();
                    await writer.WriteLineAsync(
                        $"+OK {live.Count} {live.Sum(item => Bytes(item))}");
                    break;

                case "LIST":
                    await ListAsync(writer, session, deleted, parts, uid: false);
                    break;

                case "UIDL":
                    await ListAsync(writer, session, deleted, parts, uid: true);
                    break;

                case "RETR":
                    if (Resolve(session, deleted, parts, out var toSend))
                    {
                        await writer.WriteLineAsync($"+OK {Bytes(toSend!)} octets");
                        await WriteBodyAsync(writer, toSend!.Raw, int.MaxValue);
                        mailbox.MarkSeen(toSend.Id);
                    }
                    else
                    {
                        await writer.WriteLineAsync("-ERR No such message");
                    }
                    break;

                case "TOP":
                    // TOP <n> <lines>: headers plus the first n lines of the body. Clients use
                    // it to preview without downloading everything.
                    if (Resolve(session, deleted, parts, out var toPeek) &&
                        parts.Length > 2 && int.TryParse(parts[2], out var lines))
                    {
                        await writer.WriteLineAsync("+OK");
                        await WriteBodyAsync(writer, toPeek!.Raw, lines);
                    }
                    else
                    {
                        await writer.WriteLineAsync("-ERR No such message");
                    }
                    break;

                case "DELE":
                    if (Resolve(session, deleted, parts, out var toDelete))
                    {
                        // Marked now, removed at QUIT. A client that disconnects without
                        // quitting expects to find the message still there.
                        deleted.Add(toDelete!.Id);
                        await writer.WriteLineAsync("+OK Marked for deletion");
                    }
                    else
                    {
                        await writer.WriteLineAsync("-ERR No such message");
                    }
                    break;

                case "RSET":
                    deleted.Clear();
                    await writer.WriteLineAsync("+OK Deletions undone");
                    break;

                case "NOOP":
                    await writer.WriteLineAsync("+OK");
                    break;

                case "QUIT":
                    foreach (var id in deleted)
                    {
                        mailbox.Remove(id);
                    }

                    if (deleted.Count > 0)
                    {
                        Log.LogInformation("POP3 session removed {Count} message(s).", deleted.Count);
                    }

                    await writer.WriteLineAsync("+OK Bye");
                    return;

                default:
                    Log.LogWarning("Refused unsupported POP3 command {Verb}.", verb);
                    await writer.WriteLineAsync($"-ERR {verb} not supported");
                    break;
            }
        }
    }

    private static async Task ListAsync(
        StreamWriter writer,
        List<CapturedMessage> session,
        HashSet<string> deleted,
        string[] parts,
        bool uid)
    {
        // With an argument it describes one message; without, it lists them all.
        if (parts.Length > 1)
        {
            if (Resolve(session, deleted, parts, out var one))
            {
                var number = session.IndexOf(one!) + 1;
                await writer.WriteLineAsync($"+OK {number} {(uid ? one!.Id : Bytes(one!).ToString())}");
            }
            else
            {
                await writer.WriteLineAsync("-ERR No such message");
            }

            return;
        }

        await writer.WriteLineAsync("+OK");

        for (var index = 0; index < session.Count; index++)
        {
            var message = session[index];
            if (deleted.Contains(message.Id))
            {
                continue;
            }

            await writer.WriteLineAsync($"{index + 1} {(uid ? message.Id : Bytes(message).ToString())}");
        }

        await writer.WriteLineAsync(".");
    }

    private static bool Resolve(
        List<CapturedMessage> session,
        HashSet<string> deleted,
        string[] parts,
        out CapturedMessage? message)
    {
        message = null;

        if (parts.Length < 2 || !int.TryParse(parts[1], out var number) ||
            number < 1 || number > session.Count)
        {
            return false;
        }

        var found = session[number - 1];
        if (deleted.Contains(found.Id))
        {
            return false;
        }

        message = found;
        return true;
    }

    /// <summary>
    /// Writes a message body and terminates it with a lone dot, escaping any line that starts
    /// with one so the client does not read it as the end.
    /// </summary>
    private static async Task WriteBodyAsync(StreamWriter writer, string raw, int bodyLines)
    {
        var all = raw.Replace("\r\n", "\n").Split('\n');
        var written = 0;
        var inBody = false;

        foreach (var line in all)
        {
            if (!inBody && line.Length == 0)
            {
                inBody = true;
            }
            else if (inBody)
            {
                if (written >= bodyLines)
                {
                    break;
                }

                written++;
            }

            await writer.WriteLineAsync(line.StartsWith('.') ? "." + line : line);
        }

        await writer.WriteLineAsync(".");
    }

    private static int Bytes(CapturedMessage message) => Encoding.UTF8.GetByteCount(message.Raw);
}

public sealed class Pop3Options
{
    public const string SectionName = "Pop3";

    /// <summary>1110 rather than 110: the privileged port needs root and is often taken.</summary>
    public int Port { get; set; } = 1110;

    /// <summary>
    /// Loopback by default. This server hands every captured message to anyone who connects
    /// and accepts any password, so putting it on a network is not a thing to do by accident.
    /// </summary>
    public bool ListenOnAllInterfaces { get; set; }
}
