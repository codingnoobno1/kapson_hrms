using Microsoft.Extensions.Options;
using Vispl.HRManagement.MailReceiver.Imap;
using Vispl.HRManagement.MailReceiver.Pop3;
using Vispl.HRManagement.MailReceiver.Smtp;
using Vispl.HRManagement.MailReceiver.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// 7002 by convention across this repo: 7000 is the HR host, 7001 sends, 7002 receives.
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://localhost:7002");
}

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<Pop3Options>(builder.Configuration.GetSection(Pop3Options.SectionName));
builder.Services.Configure<ImapOptions>(builder.Configuration.GetSection(ImapOptions.SectionName));
builder.Services.Configure<ReceiverOptions>(builder.Configuration.GetSection(ReceiverOptions.SectionName));

builder.Services.AddSingleton<Mailbox>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<SmtpListener>();
builder.Services.AddHostedService<Pop3Listener>();
builder.Services.AddHostedService<ImapListener>();

var app = builder.Build();

// ---- The mail client -------------------------------------------------------

app.MapGet("/", () => Results.Content(MailApp.Page(), "text/html"));

// Connection details are fetched, not baked into the page. The panel that tells somebody which
// port to type is worth nothing unless it is the port that is actually open, and the only thing
// that knows that is the options the listeners bound with.
app.MapGet("/api/inbox/v1/settings", (
        IOptions<SmtpOptions> smtp,
        IOptions<Pop3Options> pop,
        IOptions<ImapOptions> imap,
        IOptions<ReceiverOptions> receiver) =>
    Results.Ok(InboxSettings.From(smtp, pop, imap, receiver)));

// ---- The API the client and the tests share --------------------------------

app.MapGet("/api/inbox/v1/messages", (Mailbox mailbox, string? to, string? q) =>
{
    IEnumerable<CapturedMessage> found = mailbox.All();

    // `to` is what a test filters by; `q` is what somebody types in the search box. Kept
    // separate because a test asking for one address should never match a body that mentions it.
    if (!string.IsNullOrWhiteSpace(to))
    {
        found = found.Where(message => message.To.Any(
            recipient => recipient.Contains(to, StringComparison.OrdinalIgnoreCase)));
    }

    if (!string.IsNullOrWhiteSpace(q))
    {
        found = found.Where(message =>
            message.Subject.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            message.From.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            message.To.Any(recipient => recipient.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
            (message.TextBody ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    // Bodies are left out of the list. The client fetches one when it opens it, and shipping
    // every HTML body on every poll would make the list slow for no benefit.
    return Results.Ok(found.Select(message => new
    {
        message.Id,
        message.ReceivedUtc,
        message.From,
        message.To,
        message.Subject,
        message.Links,
        Seen = mailbox.IsSeen(message.Id)
    }));
});

app.MapGet("/api/inbox/v1/messages/{id}", (Mailbox mailbox, string id) =>
    mailbox.Find(id) is { } message ? Results.Ok(message) : Results.NotFound());

// The newest message to an address. What a test asks for after triggering something, without
// having to poll a list and sort it.
app.MapGet("/api/inbox/v1/latest", (Mailbox mailbox, string to) =>
    mailbox.Latest(to) is { } message ? Results.Ok(message) : Results.NotFound());

app.MapPost("/api/inbox/v1/messages/{id}/read", (Mailbox mailbox, string id) =>
{
    mailbox.MarkSeen(id);
    return Results.NoContent();
});

app.MapPost("/api/inbox/v1/messages/{id}/unread", (Mailbox mailbox, string id) =>
{
    mailbox.MarkUnseen(id);
    return Results.NoContent();
});

app.MapDelete("/api/inbox/v1/messages", (Mailbox mailbox) =>
    Results.Ok(new { cleared = mailbox.Clear() }));

app.MapDelete("/api/inbox/v1/messages/{id}", (Mailbox mailbox, string id) =>
    mailbox.Remove(id) ? Results.Ok(new { removed = id }) : Results.NotFound());

// Polled by the client so it refreshes only when something actually arrived, rather than
// pulling a message out from under somebody who is reading it.
app.MapGet("/api/inbox/v1/state", (Mailbox mailbox) =>
    Results.Ok(new { count = mailbox.Count, unseen = mailbox.UnseenCount }));

/// <summary>
/// Compose posts through here rather than straight to the sender on 7001.
///
/// One origin means no CORS to configure and no second host for somebody to get wrong — and it
/// keeps the mail app one app, which is the entire point of putting Compose in the client
/// rather than on a separate page.
/// </summary>
app.MapPost("/api/inbox/v1/compose", async (
    ComposeRequest request,
    IHttpClientFactory clients,
    IOptions<ReceiverOptions> options,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var settings = options.Value;

    using var client = clients.CreateClient();
    client.Timeout = TimeSpan.FromSeconds(20);

    using var outbound = new HttpRequestMessage(
        HttpMethod.Post, new Uri(new Uri(settings.SenderBaseUrl), "api/mail/v1/send"))
    {
        Content = JsonContent.Create(new
        {
            to = request.To,
            toName = request.ToName,
            subject = request.Subject,
            text = request.Text,
            html = request.Html
        })
    };

    if (!string.IsNullOrWhiteSpace(settings.SenderApiKey))
    {
        outbound.Headers.TryAddWithoutValidation("X-Mail-Key", settings.SenderApiKey);
    }

    try
    {
        using var response = await client.SendAsync(outbound, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        logger.LogError(exception, "Could not reach the sender at {Url}.", settings.SenderBaseUrl);

        return Results.Json(
            new { failure = $"Could not reach the sender at {settings.SenderBaseUrl}." },
            statusCode: 502);
    }
});

app.Run();

/// <summary>What the compose window posts. Validated by the sender, which owns those rules.</summary>
public sealed record ComposeRequest(
    string To, string Subject, string Text, string? Html = null, string? ToName = null);
