# Mail

Two standalone services, plus a thin client in the HR application. Ports follow the repo's
convention: **7000 is the HR host, 7001 sends, 7002 receives.**

```
HR host (7000) ──HTTP──▶ MailSender (7001) ──SMTP──▶ MailReceiver (7002)
      │                                                 ├─ web inbox
      └──────────────HTTP, read────────────────────────▶└─ /admin/mail
```

## Why it is split this way

The HR application does not speak SMTP. It posts a rendered message to the sender and forgets
about it. Relays, credentials, TLS, timeouts and whatever a client's provider demands live in
one small service that knows nothing about HR — no templates, no candidates, no merge fields.
That is what makes `Vispl.HRManagement.MailSender` liftable: point it at a real relay, or lift
the folder into another product, and nothing about it needs to change.

The receiver exists because real providers are the wrong thing to develop against. Gmail and
the rest reject or spam-bin machine-generated mail from an unknown host, so pointing development
at them means either no mail or somebody's real inbox filling with test messages. The receiver
accepts everything, delivers nothing, and shows it all on a page.

## Running them

```bash
cd Vispl.HRManagement.MailReceiver && dotnet run    # 7002, and SMTP on 2525
cd Vispl.HRManagement.MailSender   && dotnet run    # 7001
cd Vispl.HRManagement.Host         && dotnet run    # 7000
```

Order does not matter. A send while the receiver is down fails, is logged, and never takes the
thing it was about down with it.

## MailSender (7001)

| | |
|---|---|
| `GET /` | a compose form — the quickest way to prove mail works is to send one and go and look at it |
| `POST /api/mail/v1/send` | `{ to, toName, subject, text, html, fromAddress?, fromName? }` |
| `GET /api/mail/v1/status` | relay, from-address, sent and failed counts, last failure |

`text` is required and `html` is not. A message with only HTML is a message some readers cannot
read, and for an activation mail the link *is* the message.

A failed relay answers **502**, not 500: the caller's request was fine, the machine downstream
was not, and those deserve different reactions.

Configured under `Relay` — host, port, SSL, credentials, from-address, and `ApiKey`. **The
service refuses to start outside Development without a key**, because a mail service reachable
without one is an open relay and somebody else's spam problem within the hour.

### One look across both

The sender's page uses the receiver client's stylesheet — the same tokens, the same blue top
bar, the same cards, inputs and result notes. Two services that both handle mail should not
look like two unrelated tools.

The rule both follow: **blue chrome is the product, teal means this sends mail.** The receiver
already used teal on its compose window and its Send button; the sender's Send is the same
teal.

It is **copied, not shared**. A stylesheet package between the two would be a dependency bought
for one page each, and the whole point of `MailSender` is that it can be lifted into another
product without bringing anything with it.

## MailReceiver (7002)

Three protocol servers over one shared mailbox, plus a web viewer.

| Protocol | Port | What it is for |
|---|---|---|
| **SMTP** | 2525 | where the sender delivers |
| **POP3** | 1110 | download into a mail client |
| **IMAP** | 1143 | browse from a mail client without downloading |
| HTTP | 7002 | the viewer, and the API a test drives |

Any username and password opens the one mailbox called `INBOX`. This is a development sink;
inventing accounts nobody asked for would add ceremony without adding safety. That is also why
all three bind to **loopback** unless told otherwise — each hands every captured message to
anyone who connects.

Point Thunderbird or Outlook at `localhost:1143` (IMAP) or `localhost:1110` (POP3), no
encryption, any credentials. The viewer's *Connect a mail client* panel shows the same
settings.

### What each speaks

- **SMTP** — EHLO, MAIL FROM, RCPT TO, DATA, RSET, NOOP, QUIT. No TLS, no auth, no forwarding;
  anything it accepted going anywhere would defeat the point.
- **POP3** — USER, PASS, CAPA, STAT, LIST, UIDL, TOP, RETR, DELE, RSET, NOOP, QUIT. Deletions
  are marked during a session and applied at QUIT, which is what clients set to "remove from
  server" rely on.
- **IMAP4rev1** — CAPABILITY, LOGIN, LIST, LSUB, STATUS, SELECT, EXAMINE, FETCH, UID FETCH,
  SEARCH, STORE, EXPUNGE, CLOSE, NOOP, LOGOUT. IDLE, APPEND, COPY, MOVE, sub-mailboxes and TLS
  are **refused with a clear NO** rather than faked — a client that asks gets an answer instead
  of a silence it has to time out on.

`\Seen` is shared across all of them: opening a message in the browser marks it read in
Thunderbird, and vice versa. One set of read state beats three that disagree.

The accept loop lives once, in `LineProtocolListener`. All three are line-oriented text over
TCP and need the same handling, so a fix — such as the byte-order mark below — is fixed
everywhere rather than in whichever copy somebody remembered.

### The client

A two-pane mail client, not a list of debug output: message list on the left, reading pane on
the right, and **Compose** in the header. It talks to the JSON API below, so opening a message
never reloads the window — selecting mail in a mail client does not throw away your search and
your scroll position.

- **Senders read as names.** `"Vispl HR" <no-reply@…>` becomes *Vispl HR*, with a coloured
  avatar whose hue is hashed from the address so the same correspondent always looks the same.
- **Times read as times.** `14:22` today, `Yesterday`, a weekday this week, `5 Sep` beyond.
  A full timestamp is unreadable at a glance and the year is never the point.
- **Unread** is bold with a dot and a count in the browser tab title. Opening marks read;
  ✉︎ puts it back to unread.
- **HTML / Text / Source** tabs per message. HTML renders in a sandboxed iframe — captured mail
  is untrusted content and this client must not run it.
- **Links are lifted out** and shown as a chip, because an activation mail exists to carry one.
- **Compose** posts through `/api/inbox/v1/compose`, which forwards to the sender. One origin
  means no CORS to configure and no second host to get wrong — and it keeps the mail app one
  app. Sending keeps whatever you were reading open.
- **Search** filters subject, addresses and body as you type.
- One pane at a time under 820px, with a back arrow, which is what every mail app does.

It polls `/api/inbox/v1/state` every four seconds and refreshes the list *only when the count
actually changes*, so a message somebody is reading is never pulled out from under them.

| | |
|---|---|
| `GET /` | the mail client |
| `GET /api/inbox/v1/messages?to=&q=` | list, without bodies |
| `GET /api/inbox/v1/latest?to=` | the newest to an address — what a test asks for |
| `GET /api/inbox/v1/messages/{id}` | one message, parsed |
| `DELETE /api/inbox/v1/messages` | empty it |
| `DELETE /api/inbox/v1/messages/{id}` | remove one |
| `POST /api/inbox/v1/messages/{id}/read` | mark read |
| `POST /api/inbox/v1/messages/{id}/unread` | mark unread |
| `POST /api/inbox/v1/compose` | send, forwarded to the sender |
| `GET /api/inbox/v1/state` | count and unread, for polling |
| `GET /api/inbox/v1/settings` | hosts, ports and the sender — so no page has to guess |

Every link found in a message is extracted and shown, because an activation mail exists to
carry one and clicking it from the inbox is the reason to run this locally.

Messages are held **in memory only**, capped at 200, newest first. Persisting captured mail
would mean writing real addresses to disk on a laptop; losing it on restart is correct, not a
limitation.

### Two things that cost an hour each

**A UTF-8 BOM in front of the SMTP greeting.** `Encoding.UTF8` emits one, and three stray bytes
before `220` make every client report a syntax error before the conversation begins. SMTP is a
line protocol, not a file format — the writer uses `new UTF8Encoding(false)`.

**Entity-encoded links.** The same URL in the text and HTML parts appeared twice, once with a
literal `&amp;`, and the encoded one was not clickable. Links are extracted after decoding.

**`[hidden]` losing to `display:flex`.** The compose window carries its own `display`, which
outranks the browser's own `[hidden]` rule — so it sat open the moment the page loaded. An
explicit `[hidden] { display:none !important }` settles it.

## The client side

`IEmailSender` lives in `BM.ITF/Shared`. `MailServiceEmailSender` implements it by posting to
the sender. It **never throws**: every send is a side effect of something more important — an
application was accepted, an account was created — and losing that because a mail server was
unreachable would be the wrong trade every time. Callers get a result and decide.

Configured under `Mail`: `SenderBaseUrl`, `ApiKey`, `PublicBaseUrl` and `Enabled`.
`PublicBaseUrl` is what turns `/onboarding` into something clickable — mail is read outside the
application, where a relative path is useless.

`Enabled: false` stops delivery without removing the configuration, which is what a test run or
a restored backup wants: no mail leaves, and the reason is a setting somebody can see rather
than an unreachable host.

Message bodies are in `CandidateMail`; every URL is built in `CandidateLinks`. One place,
because a link in an email cannot be corrected once it is sent — and because two callers
composing the same URL by hand is how one ends up pointing at a route that was renamed. That
is exactly what the old logged `/career/{tenant}/activate` link did: it named a page that was
never built.

## The admin view (`/admin/mail`)

The same mailbox, read from inside the admin panel by somebody who is signed in. Message list,
reading pane, Text / HTML / Source, the links a message carried, mark-unread and delete — plus a
link out to the full client on the receiver for composing and for the protocol settings.

It reads through `IMailInbox`, a port beside `IEmailSender`: one hands a message over and
forgets it, the other goes and looks. Both are ports, so the page has no idea whether it is
reading a development catcher or an archive, which is what stops the admin panel growing a
dependency on a development tool. Like the sender, **nothing in it throws** — the inbox being
down is a normal Tuesday and gets a sentence, not a stack trace.

Configured by `Mail:ReceiverBaseUrl`. **Blank turns the page off** rather than pointing it at
nothing, which is the right answer once a real provider is relaying: the mail left and there is
no mailbox of ours to browse. A page showing an empty list would be lying about that.

Captured HTML renders in an `iframe sandbox=""`. Mail is untrusted content and the admin panel
is the last place that should run it.

## Nothing is written into a page that a service can answer for

The viewer used to carry `localhost:1143` in its markup — a guess that happened to be right on
the machine it was written on. A panel telling somebody which port to type is worth nothing
unless it is the port that is actually open, so:

- `GET /api/inbox/v1/settings` reports the protocols, their hosts and ports, and the sender's
  authority, **read off the options the listeners bound with**. The viewer fetches it on load
  and fills the connection panel and the composer's footer from the answer.
- The host is resolved rather than assumed: loopback when the listeners are on loopback, this
  machine's name when they are on the network. `Receiver:ClientHost` overrides it for a
  container or a port forward, where the process cannot know the name it is reached by.
- The sender's page links to the inbox from `Relay:InboxUrl`. **Blank hides the links**, which
  is right for a real provider — there is no page to send anybody to, and a link to one that
  does not exist is worse than none.
- The admin page takes its origin from `IMailInbox.Origin`.

## Verifying

With all three running:

```bash
SMOKE_BASE_URL=http://localhost:7000 SMOKE_USERNAME=... SMOKE_PASSWORD=... \
  python scripts/verify-candidate-onboarding.py
```

It applies, reads the activation link **out of the captured mail**, sets a password, signs in,
accepts an offer and completes the checklist — the same path a candidate walks, with nothing
scraped from a log.

The protocol servers have their own check:

```bash
python scripts/verify-mail-protocols.py
```

It drives POP3 and IMAP with Python's own `poplib` and `imaplib` rather than hand-rolled
sockets: if those are satisfied, Thunderbird and Outlook will be. It parses what comes back
rather than grepping the raw MIME — asserting against raw bytes is asserting against an
encoding, and .NET base64s the parts.

And the admin view:

```bash
python scripts/verify-admin-mail.py
```

It sends a message, signs an administrator in, and finds it on `/admin/mail` — then checks that
the ports the settings endpoint reports are the ones `appsettings.json` configured, and that no
page carries a host or a port in its markup. Both halves matter: a viewer that shows the right
mail through the wrong connection details still sends somebody to a port that is not open.
