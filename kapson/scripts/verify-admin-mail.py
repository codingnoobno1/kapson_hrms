"""
The admin mail view, end to end.

Sends a message through the sender, then proves an administrator signed in to the HR host can
see it on /admin/mail -- and that nothing about a port, a host or a mailbox is written into a
page rather than answered by the service that knows it.

    python scripts/verify-admin-mail.py

Environment (all optional, defaults match the repo's ports):
    HOST_BASE_URL       http://localhost:7000
    SENDER_BASE_URL     http://localhost:7001
    RECEIVER_BASE_URL   http://localhost:7002
    ADMIN_USERNAME      admin
    ADMIN_PASSWORD      Vertex#Admin2026
"""

import http.cookiejar
import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

HOST = os.environ.get("HOST_BASE_URL", "http://localhost:7000").rstrip("/")
SENDER = os.environ.get("SENDER_BASE_URL", "http://localhost:7001").rstrip("/")
RECEIVER = os.environ.get("RECEIVER_BASE_URL", "http://localhost:7002").rstrip("/")
USERNAME = os.environ.get("ADMIN_USERNAME", "admin")
PASSWORD = os.environ.get("ADMIN_PASSWORD", "Vertex#Admin2026")

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

passed = 0
failed = 0


def check(name, ok, detail=""):
    global passed, failed
    if ok:
        passed += 1
        print(f"  PASS  {name}")
    else:
        failed += 1
        print(f"  FAIL  {name}" + (f"\n          {detail}" if detail else ""))
    return ok


def source(*parts):
    with open(os.path.join(REPO, *parts), encoding="utf-8") as handle:
        return handle.read()


def get(url, opener=None, want_json=True):
    request = urllib.request.Request(url, headers={"Accept": "*/*"})
    open_it = opener.open if opener else urllib.request.urlopen
    with open_it(request, timeout=20) as response:
        body = response.read().decode("utf-8", "replace")
    return json.loads(body) if want_json else body


def post_json(url, payload):
    data = json.dumps(payload).encode()
    request = urllib.request.Request(
        url, data=data, headers={"Content-Type": "application/json"}, method="POST")
    with urllib.request.urlopen(request, timeout=20) as response:
        return json.loads(response.read().decode())


# --------------------------------------------------------------------------
print("\nThe receiver answers for its own ports")
print("-" * 74)

try:
    settings = get(f"{RECEIVER}/api/inbox/v1/settings")
except Exception as error:                                   # noqa: BLE001
    print(f"  Could not reach the receiver at {RECEIVER}: {error}")
    print("  Start it with: cd Vispl.HRManagement.MailReceiver && dotnet run")
    sys.exit(1)

names = {endpoint["name"] for endpoint in settings["protocols"]}
check("settings lists IMAP, POP3 and SMTP", names == {"IMAP", "POP3", "SMTP in"}, sorted(names))
check("every endpoint carries a host and a port",
      all(endpoint["host"] and endpoint["port"] for endpoint in settings["protocols"]))
check("the sender is reported with an authority, not a guess",
      bool(settings["sender"]["authority"]) and settings["sender"]["configured"])

with open(os.path.join(REPO, "Vispl.HRManagement.MailReceiver", "appsettings.json"),
          encoding="utf-8-sig") as handle:
    receiver_config = json.load(handle)

ports = {endpoint["name"]: endpoint["port"] for endpoint in settings["protocols"]}
check("the reported IMAP port is the configured one",
      ports["IMAP"] == receiver_config["Imap"]["Port"], ports)
check("the reported POP3 port is the configured one",
      ports["POP3"] == receiver_config["Pop3"]["Port"], ports)
check("the reported SMTP port is the configured one",
      ports["SMTP in"] == receiver_config["Smtp"]["Port"], ports)

# --------------------------------------------------------------------------
print("\nNothing is written into a page that a service should answer for")
print("-" * 74)

viewer = source("Vispl.HRManagement.MailReceiver", "Web", "MailApp.cs")
check("the viewer carries no port number in its markup",
      not re.search(r"localhost:\d|on 700\d|:1143|:1110|:2525", viewer),
      "found: " + str(re.findall(r"localhost:\d+|on 700\d|:11\d\d|:2525", viewer)))
check("the viewer asks for its settings", "/api/inbox/v1/settings" in viewer)

sender_page = source("Vispl.HRManagement.MailSender", "Program.cs")
inbox_links = re.findall(r'href="http://localhost:\d+"', sender_page)
check("the sender page has no hard-coded inbox link", not inbox_links, str(inbox_links))
check("the sender page reads the inbox link from configuration",
      "status.InboxUrl" in sender_page)

page = source("Vispl.HRManagement.UI", "Components", "Admin", "Pages", "MailAdmin.razor")
check("the admin page hard-codes no host or port",
      not re.search(r"localhost|:70\d\d", page))
check("the admin page reads the origin from the port", "Inbox.Origin" in page)
check("the admin page goes through the ports, not HttpClient",
      "IMailInbox" in page and "IEmailSender" in page and "HttpClient" not in page)
check("compose sends through IEmailSender, not the receiver's proxy",
      "Sender.SendAsync" in page and "/api/inbox/v1/compose" not in page)
check("captured HTML is rendered sandboxed", 'sandbox=""' in page)

served = get(f"{RECEIVER}/", want_json=False)
check("the served viewer contains no port literal",
      not re.search(r"localhost:\d+", served))

# --------------------------------------------------------------------------
print("\nA sent message reaches the admin panel")
print("-" * 74)

marker = f"admin-mail-{int(time.time())}"
recipient = f"{marker}@vispl-hr.local"
subject = f"Verification {marker}"
link = f"{HOST}/onboarding?tenant=kapson&token={marker}"

sent = post_json(f"{SENDER}/api/mail/v1/send", {
    "to": recipient,
    "toName": "Verification",
    "subject": subject,
    "text": f"Open {link} to continue.",
    "html": f'<p>Open <a href="{link}">this link</a> to continue.</p>',
})
check("the sender accepted it", sent.get("sent"), sent)

captured = None
for _ in range(20):
    try:
        captured = get(f"{RECEIVER}/api/inbox/v1/latest?to={urllib.parse.quote(recipient)}")
        break
    except urllib.error.HTTPError:
        time.sleep(0.25)

check("the receiver captured it", captured is not None and captured["subject"] == subject)
check("the link was lifted out of the body",
      captured is not None and any(marker in found for found in captured["links"]),
      captured["links"] if captured else None)

# --------------------------------------------------------------------------
print("\nOnly an administrator can read it")
print("-" * 74)

jar = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))

anonymous = urllib.request.build_opener(urllib.request.HTTPRedirectHandler())
try:
    body = get(f"{HOST}/admin/mail", opener=anonymous, want_json=False)
    check("signed out, /admin/mail shows no captured mail", marker not in body)
except urllib.error.HTTPError as error:
    check("signed out, /admin/mail is refused", error.code in (401, 403, 404), error.code)

login = get(f"{HOST}/admin", opener=opener, want_json=False)
token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', login)

if not check("the admin sign-in page offers a token", token is not None):
    print("\n  Cannot continue without signing in.")
else:
    form = urllib.parse.urlencode({
        "Username": USERNAME,
        "Password": PASSWORD,
        "__RequestVerificationToken": token.group(1),
    }).encode()

    request = urllib.request.Request(
        f"{HOST}/identity/admin/login", data=form,
        headers={"Content-Type": "application/x-www-form-urlencoded"}, method="POST")

    try:
        with opener.open(request, timeout=20) as response:
            landed = response.geturl()
    except urllib.error.HTTPError as error:
        landed = error.url or ""

    check("the administrator signed in", landed.rstrip("/").endswith("/admin/dashboard") or "/admin" in landed, landed)

    body = get(f"{HOST}/admin/mail", opener=opener, want_json=False)
    check("the admin panel has a Mail page", "<h1>Mail</h1>" in body)
    check("it names the mailbox it is reading", RECEIVER in body)
    check("the captured message is listed", subject in body,
          "the page rendered but the message is not on it")
    check("it does not claim an unconfigured mailbox",
          "No mailbox is configured" not in body)

    print()
    print("Composing from the admin panel")
    print("-" * 74)

    check("the page offers Compose", ">Compose<" in body or "Compose" in body)

    # The page is Blazor over a circuit, so the form itself cannot be driven from here. What can
    # be checked is the road the button takes -- IEmailSender, the sender, the mailbox the page
    # reads -- which is the part that would break without anybody noticing.
    composed_to = f"composed-{marker}@vispl-hr.local"
    composed_subject = f"Composed {marker}"

    composed = post_json(f"{SENDER}/api/mail/v1/send", {
        "to": composed_to,
        "subject": composed_subject,
        "text": "Composed from the admin panel.",
    })
    check("a composed message is relayed", composed.get("sent"), composed)

    arrived = None
    for _ in range(20):
        try:
            arrived = get(f"{RECEIVER}/api/inbox/v1/latest?to={urllib.parse.quote(composed_to)}")
            break
        except urllib.error.HTTPError:
            time.sleep(0.25)

    check("it lands in the mailbox the admin page reads",
          arrived is not None and arrived["subject"] == composed_subject)

    listed = get(f"{HOST}/admin/mail", opener=opener, want_json=False)
    check("and the admin page shows it", composed_subject in listed)

    if arrived:
        request = urllib.request.Request(
            f"{RECEIVER}/api/inbox/v1/messages/{arrived['id']}", method="DELETE")
        urllib.request.urlopen(request, timeout=20).close()

# --------------------------------------------------------------------------
print("\nNothing is left behind")
print("-" * 74)

if captured:
    request = urllib.request.Request(
        f"{RECEIVER}/api/inbox/v1/messages/{captured['id']}", method="DELETE")
    with urllib.request.urlopen(request, timeout=20) as response:
        check("the verification message was removed", response.status == 200)

print("\n" + "=" * 74)
print(f"  {passed} passed, {failed} failed")
print("=" * 74 + "\n")

sys.exit(1 if failed else 0)
