"""Talk to the captured mailbox the way a real mail client does.

Uses Python's own poplib and imaplib rather than hand-rolled sockets: if those two libraries
are satisfied, Thunderbird and Outlook will be too. Anything the servers get subtly wrong shows
up here as a protocol error rather than as a client that silently shows an empty folder.

    python scripts/verify-mail-protocols.py
"""
import email
import imaplib
import json
import os
import poplib
import urllib.request
from email.header import decode_header


def readable(raw):
    """What a mail client shows: the decoded subject and the decoded body parts.

    Asserting against the raw MIME would be asserting against an encoding — .NET base64s or
    quoted-printable-encodes the parts, so the plain text is not in there verbatim. Parsing is
    what proves a client can actually read what these servers hand it.
    """
    message = email.message_from_string(raw)

    subject = "".join(
        piece.decode(charset or "utf-8", errors="replace") if isinstance(piece, bytes) else piece
        for piece, charset in decode_header(message.get("Subject", "")))

    bodies = []
    for part in message.walk():
        if part.get_content_maintype() == "text":
            payload = part.get_payload(decode=True)
            if payload:
                bodies.append(payload.decode(part.get_content_charset() or "utf-8", errors="replace"))

    return subject, "\n".join(bodies)

SENDER = os.environ.get("MAIL_SENDER_URL", "http://localhost:7001").rstrip("/")
RECEIVER = os.environ.get("MAIL_RECEIVER_URL", "http://localhost:7002").rstrip("/")
HOST = os.environ.get("MAIL_HOST", "127.0.0.1")
POP_PORT = int(os.environ.get("MAIL_POP_PORT", "1110"))
IMAP_PORT = int(os.environ.get("MAIL_IMAP_PORT", "1143"))

results = []


def check(label, ok, note=""):
    print(("  PASS  " if ok else "  FAIL  ") + label + (" | " + str(note)[:150] if note else ""))
    results.append(bool(ok))


def post(url, body):
    request = urllib.request.Request(
        url, data=json.dumps(body).encode(), method="POST",
        headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.loads(response.read().decode())


def get(url):
    with urllib.request.urlopen(url, timeout=30) as response:
        return json.loads(response.read().decode())


# ---- Put something known in the mailbox ----
subject = "Protocol check " + os.urandom(4).hex()
link = "http://localhost:7000/onboarding?tenant=kapson&token=protocolcheck"

sent = post(SENDER + "/api/mail/v1/send", {
    "to": "protocol.check@example.com",
    "toName": "Protocol Check",
    "subject": subject,
    "text": f"Hello,\n\nOpen this:\n\n{link}\n\nKapsons",
    "html": f'<p>Hello,</p><p><a href="{link}">Open this</a></p>',
})

check("the sender relays a message", sent["sent"] is True, sent.get("failure") or sent["relay"])

captured = get(f"{RECEIVER}/api/inbox/v1/latest?to=protocol.check@example.com")
check("the receiver captured it", captured["subject"] == subject, captured["subject"])

# ---- POP3, as a mail client speaks it ----
try:
    pop = poplib.POP3(HOST, POP_PORT, timeout=20)
    greeting = pop.getwelcome().decode()
    check("POP3 greets", greeting.startswith("+OK"), greeting)

    pop.user("anyone")
    pop.pass_("anything")
    check("POP3 accepts any credentials", True, "one shared mailbox, by design")

    count, size = pop.stat()
    check("POP3 STAT reports the mailbox", count > 0, f"{count} messages, {size} bytes")

    listing = pop.list()[1]
    check("POP3 LIST enumerates them", len(listing) == count, f"{len(listing)} entries")

    uidl = pop.uidl()[1]
    check("POP3 UIDL gives each a stable id", len(uidl) == count, uidl[-1].decode() if uidl else "-")

    # Rejoined with CRLF: poplib hands back lines with the terminators stripped, and MIME
    # boundaries only parse against the line endings the message was written with.
    raw = b"\r\n".join(pop.retr(count)[1]).decode(errors="replace")
    got_subject, got_body = readable(raw)
    check("POP3 RETR returns a message a client can read",
          got_subject == subject and link in got_body,
          f'{len(raw)} bytes -> "{got_subject}", link {"found" if link in got_body else "MISSING"}')

    top = b"\r\n".join(pop.top(count, 0)[1]).decode(errors="replace")
    top_subject, top_body = readable(top)
    check("POP3 TOP returns the headers without the body",
          top_subject == subject and link not in top_body,
          f'{len(top)} bytes, body {"empty" if not top_body.strip() else "present"}')

    pop.quit()
    check("POP3 closes cleanly", True)
except Exception as failure:
    check("POP3 works end to end", False, repr(failure))

# ---- IMAP, as a mail client speaks it ----
try:
    imap = imaplib.IMAP4(HOST, IMAP_PORT)
    check("IMAP greets", True, imap.welcome.decode()[:60])

    imap.login("anyone", "anything")
    check("IMAP accepts any credentials", True, "one shared mailbox, by design")

    status, folders = imap.list()
    check("IMAP lists a folder", status == "OK" and any(b"INBOX" in f for f in folders),
          folders[0].decode() if folders else "-")

    status, counts = imap.select("INBOX")
    check("IMAP selects INBOX", status == "OK", f"{counts[0].decode()} messages")

    status, found = imap.search(None, "ALL")
    numbers = found[0].split()
    check("IMAP SEARCH ALL returns every message", status == "OK" and len(numbers) > 0,
          f"{len(numbers)} results")

    status, data = imap.fetch(numbers[-1], "(RFC822)")
    fetched = b"".join(part[1] for part in data if isinstance(part, tuple)).decode(errors="replace")
    fetched_subject, fetched_body = readable(fetched)
    check("IMAP FETCH returns a message a client can read",
          fetched_subject == subject and link in fetched_body,
          f"{len(fetched)} bytes -> \"{fetched_subject}\"")

    status, data = imap.uid("FETCH", numbers[-1], "(RFC822)")
    uid_fetched = b"".join(p[1] for p in data if isinstance(p, tuple)).decode(errors="replace")
    uid_subject, _ = readable(uid_fetched)
    check("IMAP UID FETCH works the same way", uid_subject == subject, f"{len(uid_fetched)} bytes")

    imap.logout()
    check("IMAP closes cleanly", True)
except Exception as failure:
    check("IMAP works end to end", False, repr(failure))

# ---- Reading it marks it read, wherever it was read ----
state = get(RECEIVER + "/api/inbox/v1/state")
check("reading over a protocol marks the message seen in the viewer too",
      state["unseen"] < state["count"],
      f"{state['unseen']} unread of {state['count']}")

print(f"\n{sum(results)}/{len(results)} checks passed")
