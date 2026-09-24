using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Vispl.HRManagement.MailReceiver.Smtp;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>2525 rather than 25: the privileged port needs root and is usually taken.</summary>
    public int Port { get; set; } = 2525;

    /// <summary>
    /// Loopback by default. This server accepts every message it is offered and shows them to
    /// anyone who opens the inbox, so binding it to a network is not a thing to do by accident.
    /// </summary>
    public bool ListenOnAllInterfaces { get; set; }

    public int MaxMessageBytes { get; set; } = 5 * 1024 * 1024;
}

/// <summary>
/// A deliberately small SMTP server that accepts everything and delivers nothing.
///
/// It exists so mail can be seen during development without involving a real provider —
/// Gmail and the rest reject or spam-bin machine-generated mail from an unknown host, and
/// pointing development at them means either no mail or somebody's real inbox. This speaks
/// enough of RFC 5321 for a client to complete a conversation: EHLO, MAIL FROM, RCPT TO, DATA,
/// RSET, NOOP, QUIT. No TLS, no authentication, no forwarding — anything it accepted going
/// anywhere would defeat the point.
/// </summary>
public sealed class SmtpListener(
    Mailbox mailbox,
    Microsoft.Extensions.Options.IOptions<SmtpOptions> options,
    ILogger<SmtpListener> logger) : BackgroundService
{
    private readonly SmtpOptions settings = options.Value;

    /// <summary>
    /// UTF-8 with no byte-order mark. <see cref="Encoding.UTF8"/> emits one, and three stray
    /// bytes in front of the 220 greeting make every client report a syntax error before the
    /// conversation has begun — SMTP is a line protocol, not a file format.
    /// </summary>
    private static readonly Encoding Wire = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var address = settings.ListenOnAllInterfaces ? IPAddress.Any : IPAddress.Loopback;
        var listener = new TcpListener(address, settings.Port);

        try
        {
            listener.Start();
        }
        catch (SocketException exception)
        {
            logger.LogError(exception,
                "Could not listen on {Address}:{Port}. Another mail catcher may already be running.",
                address, settings.Port);
            return;
        }

        logger.LogInformation("SMTP listening on {Address}:{Port}.", address, settings.Port);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stoppingToken);

                // Each conversation runs on its own; one client that stalls must not stop the
                // next from being served.
                _ = Task.Run(() => ConverseAsync(client, stoppingToken), CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task ConverseAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            try
            {
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Wire, false, 1024, true);
                await using var writer = new StreamWriter(stream, Wire, 1024, true)
                {
                    AutoFlush = true,
                    NewLine = "\r\n"
                };

                await writer.WriteLineAsync("220 vispl-hr-maildev ready");

                var from = string.Empty;
                var recipients = new List<string>();

                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line is null)
                    {
                        return;
                    }

                    var verb = line.Split(' ', 2)[0].ToUpperInvariant();

                    switch (verb)
                    {
                        case "EHLO":
                        case "HELO":
                            // No extensions advertised beyond the size limit: offering AUTH or
                            // STARTTLS we do not implement makes clients fail in odd ways.
                            await writer.WriteLineAsync("250-vispl-hr-maildev");
                            await writer.WriteLineAsync($"250 SIZE {settings.MaxMessageBytes}");
                            break;

                        case "MAIL":
                            from = Address(line);
                            recipients.Clear();
                            await writer.WriteLineAsync("250 OK");
                            break;

                        case "RCPT":
                            recipients.Add(Address(line));
                            await writer.WriteLineAsync("250 OK");
                            break;

                        case "DATA":
                            await writer.WriteLineAsync("354 Send data, end with <CRLF>.<CRLF>");
                            var body = await ReadDataAsync(reader, ct);
                            var id = Guid.NewGuid().ToString("n");

                            mailbox.Add(MessageParser.Parse(id, from, recipients.ToList(), body));

                            logger.LogInformation(
                                "Captured {MessageId} from {From} to {To}.",
                                id, from, string.Join(", ", recipients));

                            await writer.WriteLineAsync($"250 OK queued as {id}");
                            break;

                        case "RSET":
                            from = string.Empty;
                            recipients.Clear();
                            await writer.WriteLineAsync("250 OK");
                            break;

                        case "NOOP":
                            await writer.WriteLineAsync("250 OK");
                            break;

                        case "QUIT":
                            await writer.WriteLineAsync("221 Bye");
                            return;

                        default:
                            // Logged rather than silently refused: when a client walks away
                            // mid-conversation, the verb it tried is the only clue as to why.
                            logger.LogWarning("Refused unsupported verb {Verb}.", verb);
                            await writer.WriteLineAsync($"502 {verb} not implemented");
                            break;
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A malformed conversation is this server's problem, not the process's.
                logger.LogWarning(exception, "An SMTP conversation ended badly.");
            }
        }
    }

    /// <summary>
    /// Reads until the lone dot that ends DATA, undoing the transparency dot the sender adds
    /// to any line that legitimately begins with one.
    /// </summary>
    private async Task<string> ReadDataAsync(StreamReader reader, CancellationToken ct)
    {
        var body = new StringBuilder();

        while (true)
        {
            var line = await reader.ReadLineAsync(ct);

            if (line is null || line == ".")
            {
                return body.ToString();
            }

            body.Append(line.StartsWith("..", StringComparison.Ordinal) ? line[1..] : line)
                .Append("\r\n");

            if (body.Length > settings.MaxMessageBytes)
            {
                logger.LogWarning("A message exceeded {Limit} bytes and was truncated.",
                    settings.MaxMessageBytes);
                return body.ToString();
            }
        }
    }

    /// <summary>Pulls the address out of <c>MAIL FROM:&lt;someone@example.com&gt;</c>.</summary>
    private static string Address(string line)
    {
        var open = line.IndexOf('<');
        var close = line.LastIndexOf('>');

        if (open >= 0 && close > open)
        {
            return line[(open + 1)..close];
        }

        var colon = line.IndexOf(':');
        return colon >= 0 ? line[(colon + 1)..].Trim() : string.Empty;
    }
}
