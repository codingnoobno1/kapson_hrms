using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Vispl.HRManagement.MailReceiver.Smtp;

/// <summary>
/// The accept loop shared by the three protocol servers.
///
/// SMTP, POP3 and IMAP are all line-oriented text over TCP, and all three need the same
/// things: bind, accept, converse on a task each, survive a client that misbehaves, and stop
/// cleanly. Written once so a fix — such as the byte-order mark that broke every SMTP
/// conversation — is fixed everywhere rather than in whichever copy somebody remembered.
/// </summary>
public abstract class LineProtocolListener(
    string protocol,
    Func<int> port,
    Func<bool> listenOnAllInterfaces,
    ILogger logger) : BackgroundService
{
    /// <summary>
    /// UTF-8 with no byte-order mark. <see cref="Encoding.UTF8"/> emits one, and three stray
    /// bytes in front of a greeting make clients report a syntax error before the conversation
    /// has begun. These are line protocols, not file formats.
    /// </summary>
    protected static readonly Encoding Wire = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    protected ILogger Log { get; } = logger;

    /// <summary>One client, from greeting to goodbye.</summary>
    protected abstract Task ConverseAsync(StreamReader reader, StreamWriter writer, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var address = listenOnAllInterfaces() ? IPAddress.Any : IPAddress.Loopback;
        var listener = new TcpListener(address, port());

        try
        {
            listener.Start();
        }
        catch (SocketException exception)
        {
            logger.LogError(exception,
                "{Protocol} could not listen on {Address}:{Port}. Another server may hold it.",
                protocol, address, port());
            return;
        }

        logger.LogInformation("{Protocol} listening on {Address}:{Port}.", protocol, address, port());

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stoppingToken);

                // One conversation must not hold up the next.
                _ = Task.Run(() => HandleAsync(client, stoppingToken), CancellationToken.None);
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

    private async Task HandleAsync(TcpClient client, CancellationToken ct)
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

                await ConverseAsync(reader, writer, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A malformed conversation is this listener's problem, not the process's.
                Log.LogWarning(exception, "A {Protocol} conversation ended badly.", protocol);
            }
        }
    }
}
