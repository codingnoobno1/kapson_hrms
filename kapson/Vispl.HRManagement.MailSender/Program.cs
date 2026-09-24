using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Vispl.HRManagement.MailSender;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// 7001 by convention across this repo: 7000 is the HR host, 7001 sends, 7002 receives.
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://localhost:7001");
}

builder.Services.Configure<RelayOptions>(builder.Configuration.GetSection(RelayOptions.SectionName));
builder.Services.AddSingleton<MailRelay>();

var app = builder.Build();

var relay = app.Services.GetRequiredService<IOptions<RelayOptions>>().Value;

// An open mail relay on a real network is somebody else's spam problem within the hour. On a
// developer's machine a key is friction for nothing, so the rule is by environment rather than
// a setting somebody can forget.
if (!app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(relay.ApiKey))
{
    throw new InvalidOperationException(
        "Relay:ApiKey must be set outside Development. A mail service reachable without one " +
        "is an open relay.");
}

/// <summary>
/// Checks the shared secret when one is configured. Compared in full rather than short-
/// circuiting on the first wrong character, so the comparison time says nothing about the key.
/// </summary>
static bool Authorised(HttpRequest request, RelayOptions settings)
{
    if (string.IsNullOrWhiteSpace(settings.ApiKey))
    {
        return true;
    }

    var presented = request.Headers["X-Mail-Key"].ToString();

    return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
        System.Text.Encoding.UTF8.GetBytes(presented),
        System.Text.Encoding.UTF8.GetBytes(settings.ApiKey));
}

app.MapPost("/api/mail/v1/send", async (
    [FromBody] SendMailRequest request,
    HttpRequest http,
    MailRelay mail,
    IOptions<RelayOptions> options,
    CancellationToken ct) =>
{
    if (!Authorised(http, options.Value))
    {
        return Results.Json(new { title = "A valid X-Mail-Key is required." }, statusCode: 401);
    }

    var errors = new List<ValidationResult>();
    if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
    {
        return Results.ValidationProblem(errors
            .GroupBy(error => error.MemberNames.FirstOrDefault() ?? "request")
            .ToDictionary(group => group.Key, group => group.Select(e => e.ErrorMessage ?? "Invalid.").ToArray()));
    }

    var result = await mail.SendAsync(request, ct);

    // 502 rather than 500 when the relay refused us: the caller's request was fine, the
    // machine downstream was not, and those deserve different reactions.
    return result.Sent ? Results.Ok(result) : Results.Json(result, statusCode: 502);
});

app.MapGet("/api/mail/v1/status", (MailRelay mail) => Results.Ok(mail.Status()));

// ---- Compose -----------------------------------------------------------------
// A form, because the quickest way to find out whether mail works end to end is to send one
// and go and look at it. Posting JSON with curl proves the same thing and nobody does it.

app.MapGet("/", (MailRelay mail) => Results.Content(Page(mail.Status(), null, null), "text/html"));

app.MapPost("/", async (HttpRequest http, MailRelay mail, CancellationToken ct) =>
{
    var form = await http.ReadFormAsync(ct);

    var request = new SendMailRequest
    {
        To = form["to"].ToString().Trim(),
        ToName = Blank(form["toName"]),
        Subject = form["subject"].ToString().Trim(),
        Text = form["text"].ToString(),
        Html = Blank(form["html"])
    };

    var errors = new List<ValidationResult>();
    if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
    {
        return Results.Content(
            Page(mail.Status(), null, string.Join(" ", errors.Select(e => e.ErrorMessage)), request),
            "text/html");
    }

    var result = await mail.SendAsync(request, ct);

    return Results.Content(
        result.Sent
            ? Page(mail.Status(), $"Sent to {request.To}. It is waiting in the inbox.", null)
            : Page(mail.Status(), null, $"The relay refused it: {result.Failure}", request),
        "text/html");

    static string? Blank(Microsoft.Extensions.Primitives.StringValues value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.ToString();
});

app.Run();

// ---- Rendering ---------------------------------------------------------------

// Copied from the receiver's client so the two look like one product rather than two
// unrelated tools that happen to both mention mail. Copied rather than shared: this service
// stays liftable, and a shared stylesheet package would be a dependency bought for one page.
//
// The rule both follow: blue chrome is the product, teal means this sends mail. The receiver
// uses the same teal on its compose window and its Send button.
//
// Held apart from the interpolated markup: raw string literals have no brace escape, so a
// stylesheet inside one would need every rule doubled.
const string Styles = """
    :root {
      color-scheme: light;
      --line:#e3e9f2; --ink:#16202f; --dim:#64748b; --accent:#2563eb; --bg:#f6f8fc;
      --send:#0f766e;
    }
    * { box-sizing:border-box; }
    body { margin:0; background:var(--bg); color:var(--ink);
           font:14px/1.5 system-ui,-apple-system,"Segoe UI",sans-serif; }

    .top { display:flex; align-items:center; gap:14px; padding:10px 16px;
           background:var(--accent); color:#fff; }
    .brand { font-weight:700; letter-spacing:.01em; white-space:nowrap; }
    .spacer { flex:1; }
    .top a { padding:8px 16px; border-radius:999px; background:rgba(255,255,255,.16);
             color:#fff; text-decoration:none; font-size:13px; font-weight:600;
             white-space:nowrap; }
    .top a:hover { background:rgba(255,255,255,.26); }

    .wrap { max-width:760px; margin:0 auto; padding:22px 22px 60px; }
    .card { background:#fff; border:1px solid var(--line); border-radius:12px;
            padding:20px 22px; margin-bottom:14px; }
    h1 { margin:0 0 3px; font-size:17px; }
    .sub { margin:0 0 16px; color:var(--dim); font-size:12.5px; }

    label { display:block; margin-bottom:11px; }
    label span { display:block; margin-bottom:4px; font-size:12px; font-weight:600;
                 color:#334155; }
    input, textarea { width:100%; padding:9px 11px; border:1px solid var(--line);
                      border-radius:8px; font:inherit; background:#fff; }
    input:focus, textarea:focus { outline:2px solid rgba(37,99,235,.35); border-color:var(--accent); }
    textarea { min-height:120px; font-family:ui-monospace,Consolas,monospace; font-size:13px; }
    .row { display:grid; grid-template-columns:1fr 1fr; gap:0 14px; }

    .actions { display:flex; align-items:center; gap:10px; margin-top:4px; }
    .send { padding:9px 20px; border:none; border-radius:9px; background:var(--send);
            color:#fff; font:inherit; font-weight:700; cursor:pointer; }
    .send:hover { filter:brightness(1.08); }
    .quiet { padding:9px 18px; border:1px solid var(--line); border-radius:9px; background:#fff;
             color:#475569; font:inherit; font-weight:600; text-decoration:none;
             display:inline-block; }
    .quiet:hover { background:#f8fafd; }

    /* The receiver states a result the same way, in the same two colours. */
    .note { padding:11px 14px; border-radius:10px; font-size:13px; margin-bottom:14px;
            border:1px solid var(--line); background:#fff; }
    .note.ok { border-color:#b6e2c8; background:#f2fbf6; color:#15803d; }
    .note.bad { border-color:#f0bdc4; background:#fff2f3; color:#b91c1c; }

    /* Lifted from the receiver's "Connect a mail client" panel, so a fact about the plumbing
       is presented the same way on both. */
    .settings { display:flex; gap:12px; flex-wrap:wrap; }
    .settings div { flex:1 1 160px; background:#fff; border:1px solid var(--line);
                    border-radius:9px; padding:9px 13px; }
    .settings strong { display:block; font-size:11px; text-transform:uppercase;
                       letter-spacing:.04em; color:var(--dim); font-weight:700; }
    .settings code, code { font:13px ui-monospace,Consolas,monospace; }
    .settings span { font-size:13px; word-break:break-all; }

    @media (max-width: 620px) {
      .row { grid-template-columns:1fr; }
      .top a { padding:8px 12px; }
    }
    """;

string Page(MailSenderStatus status, string? sent, string? failed, SendMailRequest? keep = null) => $"""
    <!doctype html>
    <html lang="en">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width,initial-scale=1" />
    <title>Mail sender — Vispl HR</title>
    <style>{Styles}</style>
    <body>
      <header class="top">
        <span class="brand">📤 Mail sender</span>
        <span class="spacer"></span>
        {(status.InboxUrl is null ? "" : $"<a href=\"{Html(status.InboxUrl)}\">Open the inbox →</a>")}
      </header>

      <div class="wrap">
        {(sent is null ? "" : $"<div class=\"note ok\">{Html(sent)}</div>")}
        {(failed is null ? "" : $"<div class=\"note bad\">{Html(failed)}</div>")}

        <form class="card" method="post" action="/">
          <h1>Send a message</h1>
          <p class="sub">
            Relayed by SMTP to {Html(status.RelayHost)}:{status.RelayPort}. Nothing is rendered or
            stored here — this service only puts messages on the wire.
          </p>

          <div class="row">
            <label><span>To</span>
              <input type="email" name="to" required placeholder="someone@example.com"
                     value="{Html(keep?.To ?? "")}" /></label>
            <label><span>Name (optional)</span>
              <input type="text" name="toName" placeholder="Someone"
                     value="{Html(keep?.ToName ?? "")}" /></label>
          </div>

          <label><span>Subject</span>
            <input type="text" name="subject" required value="{Html(keep?.Subject ?? "")}" /></label>

          <label><span>Message — required, because a message only some readers can read is not
            a message</span>
            <textarea name="text" required>{Html(keep?.Text ?? "")}</textarea></label>

          <label><span>HTML body (optional)</span>
            <textarea name="html">{Html(keep?.Html ?? "")}</textarea></label>

          <div class="actions">
            <button class="send" type="submit">Send</button>
            {(status.InboxUrl is null ? "" : $"<a class=\"quiet\" href=\"{Html(status.InboxUrl)}\">See what arrived</a>")}
          </div>
        </form>

        <div class="card">
          <h1>Status</h1>
          <p class="sub">Post to <code>/api/mail/v1/send</code> to do this from code.</p>
          <div class="settings">
            <div><strong>Relay</strong><code>{Html(status.RelayHost)}:{status.RelayPort}{(status.UseSsl ? " SSL" : "")}</code></div>
            <div><strong>From</strong><span>{Html(status.FromAddress)}</span></div>
            <div><strong>Sent</strong><span>{status.Sent}</span></div>
            <div><strong>Failed</strong><span>{status.Failed}</span></div>
            <div><strong>Last try</strong><span>{status.LastAttemptUtc?.ToLocalTime().ToString("dd MMM HH:mm:ss") ?? "never"}</span></div>
          </div>
          {(status.LastFailure is null ? "" : $"<div class=\"note bad\" style=\"margin:14px 0 0\">{Html(status.LastFailure)}</div>")}
        </div>
      </div>
    </body>
    </html>
    """;

string Html(string value) => System.Net.WebUtility.HtmlEncode(value);
