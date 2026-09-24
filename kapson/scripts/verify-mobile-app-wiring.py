"""Every call the hybrid mobile app's screens make, exercised against a running host.

The app shares the server's Contracts project, so response *shapes* cannot drift — the
compiler catches that. What can drift, and did, is the URL: a screen pointing at a path with
no route fails only at runtime, on a phone, in front of somebody.

So this checks the thing the compiler cannot: that every path the app calls exists, answers,
and carries the envelope the client unwraps.

    SMOKE_BASE_URL=http://localhost:7000 \
    SMOKE_USERNAME=... SMOKE_PASSWORD=... \
    python scripts/verify-mobile-app-wiring.py
"""
import json
import os
import urllib.error
import urllib.request

BASE = os.environ.get("SMOKE_BASE_URL", "http://localhost:7000").rstrip("/") + "/"
USERNAME = os.environ.get("SMOKE_USERNAME")
PASSWORD = os.environ.get("SMOKE_PASSWORD")

if not USERNAME or not PASSWORD:
    raise SystemExit("Set SMOKE_USERNAME and SMOKE_PASSWORD to an HR account.")

results = []


def check(label, ok, note=""):
    print(("  PASS  " if ok else "  FAIL  ") + label + (" | " + str(note)[:150] if note else ""))
    results.append(bool(ok))


def call(method, path, body=None, token=None):
    data = json.dumps(body, default=str).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    # The app sends these on every request; a header the server rejects would break it.
    req.add_header("X-Correlation-ID", "0123456789abcdef0123456789abcdef")
    req.add_header("X-App-Version", "0.1.0")
    req.add_header("X-App-Platform", "android")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=40) as r:
            txt = r.read().decode()
            return r.status, (json.loads(txt) if txt else None)
    except urllib.error.HTTPError as e:
        txt = e.read().decode()
        try:
            return e.code, json.loads(txt)
        except Exception:
            return e.code, txt


# ---- SignIn.razor: the app posts the token request without an envelope ----
status, grant = call("POST", "api/mobile/v1/auth/token",
                     {"userName": USERNAME, "password": PASSWORD, "deviceId": "wiring-check"})
check("SignIn signs a staff user in", status == 200 and "accessToken" in (grant or {}),
      f"portal={grant.get('portal') if status == 200 else json.dumps(grant)[:90]}")

if status != 200:
    print(f"\n{sum(results)}/{len(results)} checks passed")
    raise SystemExit(1)

T = grant["accessToken"]

# The token response is deliberately NOT enveloped; everything else is. The client has two
# code paths for that, and getting it backwards fails only at runtime.
check("the token response is not enveloped", "data" not in grant,
      "PostRawAsync is the right client method for it")


def screen(label, method, path, body=None, expect_envelope=True):
    status, payload = call(method, path, body, T)

    if status != 200:
        title = payload.get("title") if isinstance(payload, dict) else str(payload)[:80]
        check(label, False, f"{status} {title}")
        return None

    if expect_envelope and not (isinstance(payload, dict) and "data" in payload):
        check(label, False, "answered 200 but without the ApiEnvelope the client unwraps")
        return None

    body = payload.get("data") if expect_envelope else payload
    shape = (f"{len(body['items'])} items" if isinstance(body, dict) and "items" in body
             else f"{len(body)} rows" if isinstance(body, list)
             else "object")
    check(label, True, shape)
    return body


print("\n-- Dashboard.razor --")
screen("HR home KPIs", "GET", "api/mobile/v1/home/hr")
screen("upcoming interviews strip", "GET", "api/mobile/v1/recruitment/interviews?pageSize=5")
screen("job analytics", "GET", "api/mobile/v1/recruitment/jobs?pageSize=50")

print("\n-- ManageJobs.razor --")
jobs = screen("job list", "GET", "api/mobile/v1/recruitment/jobs?pageSize=25")
if jobs and jobs["items"]:
    screen("job detail", "GET", f"api/mobile/v1/recruitment/jobs/{jobs['items'][0]['id']}")

print("\n-- Applicants.razor --")
apps = screen("application list", "GET", "api/mobile/v1/recruitment/applications?pageSize=25")
if apps and apps["items"]:
    screen("application detail", "GET", f"api/mobile/v1/recruitment/applications/{apps['items'][0]['id']}")

print("\n-- Interviews.razor --")
screen("interview list", "GET", "api/mobile/v1/recruitment/interviews?pageSize=25")

print("\n-- Notifications.razor --")
screen("notification list", "GET", "api/mobile/v1/notifications?pageSize=25")
screen("mark all read", "POST", "api/mobile/v1/notifications/read", {"all": True})

print("\n-- Masters.razor --")
screen("master data bundle", "GET", "api/mobile/v1/masters")

print("\n-- People.razor (new) --")
people = screen("employee directory", "GET", "api/mobile/v1/employees?pageSize=25")
if people and people["items"]:
    screen("employee record", "GET", f"api/mobile/v1/employees/{people['items'][0]['id']}")
screen("app access list", "GET", "api/mobile/v1/employees/mobile-access")

print("\n-- LeaveApprovals.razor (new) --")
screen("approval queue", "GET", "api/mobile/v1/leave/approvals")
screen("leave policies", "GET", "api/mobile/v1/leave/policies")

print("\n-- Attendance.razor (new) --")
screen("attendance dashboard", "GET", "api/mobile/v1/attendance/dashboard")
screen("late arrivals", "GET", "api/mobile/v1/attendance/reports/late-arrivals?pageSize=25")
screen("missing punches", "GET", "api/mobile/v1/attendance/reports/miss-punch?pageSize=25")
screen("movement board", "GET", "api/mobile/v1/movement/out")

print("\n-- Settings.razor / Connect.razor --")
screen("session bootstrap", "GET", "api/mobile/v1/session/bootstrap")
screen("discovery metadata", "GET", "api/mobile/v1/discovery/metadata")

print("\n-- The writes: what somebody opens the app to actually do --")

# Applicants.razor moves an application through the stage machine. The screen only offers the
# transitions the server allows, so the check is that an allowed one is accepted and a
# disallowed one is refused rather than silently doing nothing.
applied = None
if apps:
    applied = next((a for a in apps["items"] if a["stage"] == "Applied"), None)

if applied:
    status, moved = call("POST", f"api/mobile/v1/recruitment/applications/{applied['id']}/stage",
                         {"toStage": "Screening", "reason": "Moved from the HR app."}, T)
    check("an applicant can be moved to the next stage", status == 200,
          f"{applied['candidateName']}: Applied -> Screening ({status})")

    # Applied -> Offered is not a legal hop; the stage machine has to say so.
    status, refused = call("POST", f"api/mobile/v1/recruitment/applications/{applied['id']}/stage",
                           {"toStage": "Offered", "reason": "Should be refused."}, T)
    check("an illegal stage jump is refused", status == 400,
          f"{status} {(refused or {}).get('title', '')[:90]}")
else:
    print("     (no application sits at Applied, so the stage move is not exercised here)")

# Applicants.razor also decides onboarding tasks. Find an application that has a checklist.
onboarding_id = None
if apps:
    for candidate in apps["items"]:
        if candidate["stage"] in ("OfferAccepted", "Onboarding", "Hired"):
            status, found = call(
                "GET", f"api/mobile/v1/onboarding/by-application/{candidate['id']}", token=T)
            if status == 200:
                onboarding_id = found["data"]
                break

if onboarding_id:
    tasks = onboarding_id["tasks"]
    submitted = next((t for t in tasks if t["status"] == "Submitted"), None)
    check("an onboarding checklist can be opened from the applicant sheet", True,
          f"{len(tasks)} tasks, {onboarding_id['completionPercent']:.0f}% complete")

    if submitted:
        status, decided = call(
            "POST",
            f"api/mobile/v1/onboarding/{onboarding_id['id']}/tasks/{submitted['id']}/decision",
            {"decision": "approve"}, T)
        check("a submitted onboarding task can be approved", status == 200,
              f"{submitted['title']} ({status})")
    else:
        print("     (no task is awaiting a decision, so the approval is not exercised here)")
else:
    print("     (no application has reached onboarding, so the checklist is not exercised here)")

# Interviews.razor cancels. Only a live interview may be cancelled.
status, live = call("GET", "api/mobile/v1/recruitment/interviews?pageSize=25", token=T)
scheduled = None
if status == 200:
    scheduled = next((i for i in live["data"]["items"]
                      if i["status"]["code"] in ("Scheduled", "Rescheduled")), None)

if scheduled:
    status, cancelled = call(
        "POST", f"api/mobile/v1/recruitment/interviews/{scheduled['id']}/cancel",
        {"reasonCode": "SCHEDULE_CONFLICT", "comment": "Cancelled from the HR app."}, T)
    check("a scheduled interview can be cancelled", status == 200,
          f"{scheduled['candidate']['displayName']} ({status})")
else:
    print("     (nothing is scheduled, so the cancellation is not exercised here)")

print("\n-- ModuleStatus.razor: every catalogued module resolves --")
# The readiness screen probes each catalogued path. A 404 there means the catalogue points at
# a route that does not exist, which reads as "missing" for modules that are in fact built.
catalog = os.path.join(
    os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
    "Vispl.HRManagement.MobileApp", "Services", "ApiCatalog.cs")

missing = []
if os.path.exists(catalog):
    import re
    source = open(catalog, encoding="utf-8-sig").read()
    for path in sorted({m for m in re.findall(r'"(api/mobile/v1/[^"?]+)"', source)}):
        if "auth" in path or "resolve-tenant" in path:
            continue
        code, _ = call("GET", path, token=T)
        if code == 404:
            missing.append(path)

check("no catalogued module points at a route that does not exist", not missing,
      ", ".join(missing) or "all resolve")

print(f"\n{sum(results)}/{len(results)} checks passed")
