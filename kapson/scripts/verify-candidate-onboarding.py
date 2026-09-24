"""Apply, receive the mail, and complete the joining checklist as the candidate would.

Needs three services running: the HR host (7000), the mail sender (7001) and the mail
receiver (7002). Nothing here reads a log file — the activation link is taken out of the
captured message, which is the same place a real candidate would get it from.

    SMOKE_BASE_URL=http://localhost:7000 \
    SMOKE_USERNAME=... SMOKE_PASSWORD=... \
    python scripts/verify-candidate-onboarding.py
"""
import datetime
import json
import os
import subprocess
import tempfile
import urllib.error
import urllib.request

HOST = os.environ.get("SMOKE_BASE_URL", "http://localhost:7000").rstrip("/")
SENDER = os.environ.get("MAIL_SENDER_URL", "http://localhost:7001").rstrip("/")
RECEIVER = os.environ.get("MAIL_RECEIVER_URL", "http://localhost:7002").rstrip("/")
TENANT = os.environ.get("SMOKE_TENANT", "kapson")

HR_USERNAME = os.environ.get("SMOKE_USERNAME")
HR_PASSWORD = os.environ.get("SMOKE_PASSWORD")

if not HR_USERNAME or not HR_PASSWORD:
    raise SystemExit("Set SMOKE_USERNAME and SMOKE_PASSWORD to an HR account.")

results = []


def check(label, ok, note=""):
    print(("  PASS  " if ok else "  FAIL  ") + label + (" | " + str(note)[:150] if note else ""))
    results.append(bool(ok))


def call(method, url, body=None, token=None):
    data = json.dumps(body, default=str).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=40) as r:
            txt = r.read().decode()
            try:
                return r.status, json.loads(txt) if txt else None
            except ValueError:
                # An HTML page — the onboarding page itself answers this way, and "it
                # rendered" is exactly what that check wants to know.
                return r.status, txt
    except urllib.error.HTTPError as e:
        txt = e.read().decode()
        try:
            return e.code, json.loads(txt)
        except Exception:
            return e.code, txt


d = lambda r: r.get("data", r) if isinstance(r, dict) else r

# ---- The three services are up ----
for label, url in [("the HR host", HOST + "/onboarding"),
                   ("the mail sender", SENDER + "/api/mail/v1/status"),
                   ("the mail receiver", RECEIVER + "/api/inbox/v1/messages")]:
    status, _ = call("GET", url)
    check(f"{label} answers", status == 200, status)

status, sender = call("GET", SENDER + "/api/mail/v1/status")
check("the sender is pointed at the receiver",
      status == 200 and sender["relayPort"] == 2525,
      f"{sender['relayHost']}:{sender['relayPort']}" if status == 200 else "-")

# ---- Somebody applies ----
status, tok = call("POST", HOST + "/api/mobile/v1/auth/token",
                   {"userName": HR_USERNAME, "password": HR_PASSWORD, "deviceId": "mailcheck"})
T = tok["accessToken"]

status, jobs = call("GET", HOST + "/api/mobile/v1/recruitment/jobs", token=T)
opening = d(jobs)["items"][0]

stamp = datetime.datetime.now().strftime("%H%M%S")
email = f"asha.menon.{stamp}@example.com"
password = "Asha#Menon2026"

cv = os.path.join(tempfile.gettempdir(), "cv.pdf")
open(cv, "wb").write(b"%PDF-1.4\n% CV\n%%EOF\n")

payload = json.dumps({
    "fullName": "Asha Menon", "email": email, "phone": "+919820044556",
    "totalExperienceYears": 5, "currentLocation": "Mohali", "noticePeriodDays": 30,
    "expectedCtc": 650000, "answers": {}, "consentGranted": True,
    "consentPolicyVersion": "2026.1"})

out = subprocess.run(
    ["curl", "-s", "-m", "40", "-w", "\n%{http_code}", "-X", "POST",
     f"{HOST}/api/public/v1/openings/{opening['slug']}/apply?tenant={TENANT}",
     "-F", f"payload={payload}", "-F", f"Resume=@{cv};type=application/pdf"],
    capture_output=True, text=True).stdout.strip()

body, _, code = out.rpartition("\n")
application_id = json.loads(body)["applicationId"] if code.startswith("2") else None
check("a candidate applies", application_id is not None, f"{code} {body[:80]}")

# ---- The mail actually arrives ----
status, message = call("GET", f"{RECEIVER}/api/inbox/v1/latest?to={email}")
check("applying sends them an email", status == 200,
      message.get("subject") if status == 200 else "nothing captured for that address")

if status != 200:
    print(f"\n{sum(results)}/{len(results)} checks passed")
    raise SystemExit(1)

check("the email carries a link to the onboarding page",
      any("/onboarding" in link for link in message["links"]),
      ", ".join(message["links"])[:120])

check("it is sent as both text and HTML",
      bool(message["textBody"]) and bool(message["htmlBody"]),
      f"text {len(message['textBody'] or '')} chars, html {len(message['htmlBody'] or '')} chars")

link = next(l for l in message["links"] if "/onboarding" in l)
token = link.split("token=")[1].split("&")[0]

# ---- They set a password from the link, exactly as the page does ----
status, _ = call("POST", HOST + "/api/public/v1/candidate/activate",
                 {"tenant": TENANT, "token": token, "password": password})
check("the link sets their password", status == 204, status)

status, grant = call("POST", HOST + "/api/mobile/v1/auth/token",
                     {"tenant": TENANT, "email": email, "password": password,
                      "deviceId": "onboarding-web"})
C = grant["accessToken"] if status == 200 else None
check("they can sign in on the onboarding page", C is not None, status)

# ---- The page's own read: everything they may see about themselves ----
status, me = call("GET", HOST + "/api/candidate/v1/me", token=C)
progress = d(me) if status == 200 else None
check("the page can load their applications without knowing any ids",
      progress is not None and len(progress["applications"]) > 0,
      f"{progress['candidateName']}: {len(progress['applications'])} application(s)" if progress else status)

check("each application is explained in plain words",
      progress and all(a["stageExplanation"] for a in progress["applications"]),
      progress["applications"][0]["stageExplanation"] if progress else "-")

check("no checklist exists before an offer is accepted",
      progress is not None and progress["onboarding"] is None,
      "onboarding is null, as it should be at Applied")

# ---- HR walks them to an offer ----
for stage in ["Screening", "Shortlisted", "Interviewing", "Offered"]:
    call("POST", f"{HOST}/api/mobile/v1/recruitment/applications/{application_id}/stage",
         {"toStage": stage, "reason": "Onboarding page check"}, token=T)

status, me = call("GET", HOST + "/api/candidate/v1/me", token=C)
offered = d(me)["applications"][0] if status == 200 else None
check("the page shows the offer as theirs to answer",
      offered is not None and offered["awaitingMyDecision"] is True,
      f"{offered['stage']}: {offered['stageExplanation']}" if offered else status)

# ---- They accept, from the page ----
status, accepted = call("POST", f"{HOST}/api/candidate/v1/applications/{application_id}/offer",
                        {"accept": True, "joiningOn": datetime.date.today().isoformat()}, token=C)
check("they can accept the offer", status == 200, json.dumps(accepted)[:110])

status, me = call("GET", HOST + "/api/candidate/v1/me", token=C)
progress = d(me) if status == 200 else None
checklist = progress["onboarding"] if progress else None
check("the checklist appears on the page", checklist is not None,
      f"{len(checklist['tasks'])} tasks, {checklist['outstandingRequiredCount']} required outstanding"
      if checklist else status)

mine = [t for t in checklist["tasks"] if t["ownerKind"] == "Candidate"] if checklist else []
check("it shows only the items they own",
      len(mine) > 0 and all(t["ownerKind"] == "Candidate" for t in mine),
      f"{len(mine)} of {len(checklist['tasks'])} tasks are theirs" if checklist else "-")

# ---- They complete their items the way the page does ----
scan = os.path.join(tempfile.gettempdir(), "scan.pdf")
open(scan, "wb").write(b"%PDF-1.4\n% scan\n%%EOF\n")

submitted = 0
required = [t for t in mine if t["isRequired"]]

for task in required:
    if task["taskType"] == "Form":
        status, _ = call(
            "POST", f"{HOST}/api/candidate/v1/onboarding/{checklist['id']}/tasks/{task['id']}/form",
            {"values": {task["code"]: "Provided from the onboarding page"}}, token=C)
    else:
        code = subprocess.run(
            ["curl", "-s", "-o", os.devnull, "-w", "%{http_code}", "-m", "40", "-X", "POST",
             f"{HOST}/api/candidate/v1/onboarding/{checklist['id']}/tasks/{task['id']}/document",
             "-H", f"Authorization: Bearer {C}",
             "-F", f"File=@{scan};type=application/pdf"],
            capture_output=True, text=True).stdout.strip()
        status = int(code or 0)

    submitted += 1 if status in (200, 201, 204) else 0

check("they can complete every item they own", submitted == len(required),
      f"{submitted}/{len(required)} submitted")

status, me = call("GET", HOST + "/api/candidate/v1/me", token=C)
after = d(me)["onboarding"] if status == 200 else None

# The page counts the candidate's own outstanding items, not the approved ones. Approval is
# HR's progress; showing it here reads 0% to somebody who has just uploaded everything.
theirs = [t for t in (after["tasks"] if after else [])
          if t["ownerKind"] == "Candidate" and t["isRequired"]]
still_on_them = [t for t in theirs if t["status"] in ("Pending", "Rejected")]

check("nothing is left on the candidate once they have sent everything",
      after is not None and len(still_on_them) == 0,
      f"{len(theirs) - len(still_on_them)}/{len(theirs)} sent, "
      f"{after['outstandingRequiredCount']} still awaiting HR approval" if after else status)

check("but the checklist is not called finished until HR has approved it",
      after is not None and after["completionPercent"] == 0,
      f"{after['completionPercent']:.0f}% approved — the two numbers answer different questions"
      if after else status)

# ---- An application with no identity on it is refused ----
# The career site's apply page calls the service directly and gets no model validation, so
# these guards have to live in the service or one of the two ways in accepts a candidate
# record nobody can ever contact.
for label, broken in [
    ("no name", {"fullName": "", "email": f"noname.{stamp}@example.com", "phone": "+919820011111"}),
    ("no email", {"fullName": "No Email", "email": "", "phone": "+919820011111"}),
    ("a malformed email", {"fullName": "Bad Email", "email": "not-an-address", "phone": "+919820011111"}),
    ("no phone", {"fullName": "No Phone", "email": f"nophone.{stamp}@example.com", "phone": ""}),
]:
    bad = json.dumps({
        "totalExperienceYears": 3, "currentLocation": "Mohali", "noticePeriodDays": 30,
        "expectedCtc": 500000, "answers": {}, "consentGranted": True,
        "consentPolicyVersion": "2026.1", **broken})

    out = subprocess.run(
        ["curl", "-s", "-m", "40", "-w", "\n%{http_code}", "-X", "POST",
         f"{HOST}/api/public/v1/openings/{opening['slug']}/apply?tenant={TENANT}",
         "-F", f"payload={bad}", "-F", f"Resume=@{cv};type=application/pdf"],
        capture_output=True, text=True).stdout.strip()

    body, _, code = out.rpartition("\n")
    check(f"an application with {label} is refused", code.startswith("4"), f"{code} {body[:90]}")

# ---- The candidate sees nobody else ----
status, other = call("GET", f"{HOST}/api/mobile/v1/employees?pageSize=5", token=C)
check("a candidate token reaches no employee data", status in (401, 403), status)

status, other = call("GET", f"{HOST}/api/mobile/v1/recruitment/applications?pageSize=5", token=C)
check("nor anybody else's applications", status in (401, 403), status)

print(f"\n{sum(results)}/{len(results)} checks passed")
