"""Apply on the tenant -> HR sees it against the position -> hire -> grant a mobile login.

The whole arc, end to end, against the running host:
  applying earns a candidate login -> the opening board shows the applicant against
  the position -> HR moves them to an offer -> the candidate accepts and finishes
  onboarding -> that provisions an employee -> HR approves the employee's mobile login.

This writes real rows. Point it at a development database, never a client's.

    SMOKE_BASE_URL=http://localhost:7000 \
    SMOKE_USERNAME=... SMOKE_PASSWORD=... \
    python scripts/verify-hiring-chain.py

The activation link is read out of the mail receiver (7002), which is where a real candidate
would get it. Both mail services have to be running; without them the script cannot sign the
candidate in and the run stops at the offer.
"""
import datetime
import json
import os
import re
import subprocess
import tempfile
import urllib.error
import urllib.request

BASE = os.environ.get("SMOKE_BASE_URL", "http://localhost:7000").rstrip("/")
API = BASE + "/api"
RECEIVER = os.environ.get("MAIL_RECEIVER_URL", "http://localhost:7002").rstrip("/")
TENANT = os.environ.get("SMOKE_TENANT", "kapson")
SQL_SERVER = os.environ.get("SMOKE_SQL_SERVER", r"localhost\SQLEXPRESS")
SQL_DATABASE = os.environ.get("SMOKE_SQL_DATABASE", "KapsonHRManagement")

HR_USERNAME = os.environ.get("SMOKE_USERNAME")
HR_PASSWORD = os.environ.get("SMOKE_PASSWORD")

if not HR_USERNAME or not HR_PASSWORD:
    raise SystemExit(
        "Set SMOKE_USERNAME and SMOKE_PASSWORD to an HR account that holds RECRUITMENT.VIEW, "
        "EMPLOYEES.CREATE and ACCOUNTS.MANAGE.")

results = []


def check(label, ok, note=""):
    print(("  PASS  " if ok else "  FAIL  ") + label + (" | " + str(note)[:150] if note else ""))
    results.append(bool(ok))


def sql(q):
    return subprocess.run(
        ["sqlcmd", "-S", SQL_SERVER, "-d", SQL_DATABASE,
         "-E", "-C", "-I", "-h", "-1", "-W", "-Q", q],
        capture_output=True, text=True).stdout.strip()


def call(method, path, body=None, token=None):
    data = json.dumps(body, default=str).encode() if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
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


d = lambda r: r.get("data", r) if isinstance(r, dict) else r


def hr_token():
    s, t = call("POST", "/mobile/v1/auth/token",
                {"userName": HR_USERNAME, "password": HR_PASSWORD, "deviceId": "chain"})
    if s != 200:
        raise SystemExit(f"HR sign-in failed ({s}): {json.dumps(t)[:200]}")
    return t["accessToken"]


T = hr_token()

# ---- 1. Someone applies to a specific opening ----
s, jobs = call("GET", "/mobile/v1/recruitment/jobs", token=T)
listings = d(jobs).get("items", []) if s == 200 else []
opening = listings[0]
slug, title = opening["slug"], opening["title"]

stamp = datetime.datetime.now().strftime("%H%M%S")
email = f"nikhil.rao.{stamp}@example.com"
password = "Nikhil#Rao2026"

cv = os.path.join(tempfile.gettempdir(), "cv.pdf")
with open(cv, "wb") as handle:
    handle.write(b"%PDF-1.4\n% CV\n%%EOF\n")

payload = json.dumps({
    "fullName": "Nikhil Rao", "email": email, "phone": "+919820011223",
    "totalExperienceYears": 10, "currentLocation": "Chandigarh",
    "noticePeriodDays": 30, "expectedCtc": 900000, "answers": {},
    "consentGranted": True, "consentPolicyVersion": "2026.1"
})

out = subprocess.run(
    ["curl", "-s", "-m", "40", "-w", "\n%{http_code}",
     "-X", "POST", f"{API}/public/v1/openings/{slug}/apply?tenant={TENANT}",
     "-F", f"payload={payload}", "-F", f"Resume=@{cv};type=application/pdf"],
    capture_output=True, text=True).stdout.strip()

body, _, status = out.rpartition("\n")
application_id = str(json.loads(body)["applicationId"]) if status.startswith("2") else None
check(f"a candidate applies to '{title}'", application_id is not None, f"{status} {body[:90]}")

# ---- 2. The HR board shows the applicant against that position ----
s, pipeline = call("GET", "/mobile/v1/recruitment/openings/pipeline", token=T)
check("the openings board answers", s == 200, json.dumps(pipeline)[:120] if s != 200 else "")

row = None
if s == 200:
    board = d(pipeline)
    row = next((o for o in board["openings"] if o["title"] == title), None)
    print(f"     board: {len(board['openings'])} openings, {board['totalApplicants']} applicants, "
          f"{board['openingsWithNoApplicants']} vacancies with nobody applied")

check("the applicant shows against the position applied to",
      row is not None and row["totalApplicants"] >= 1,
      f"{title}: total={row['totalApplicants'] if row else '?'} new={row['newApplicants'] if row else '?'}")

check("the new applicant counts as new, not in progress",
      row is not None and row["newApplicants"] >= 1,
      f"stages: {[(x['stage'], x['count']) for x in row['byStage']] if row else '-'}")

# ---- 3. Applying is what earns a candidate login ----
# The link is taken from the captured mail, which is where a real candidate would get it.
token_value = None
message = None

try:
    with urllib.request.urlopen(f"{RECEIVER}/api/inbox/v1/latest?to={email}", timeout=20) as reader:
        message = json.loads(reader.read().decode())
except Exception:
    message = None

if message:
    link = next((item for item in message["links"] if "/onboarding" in item), None)
    if link and "token=" in link:
        token_value = link.split("token=")[1].split("&")[0]

check("applying emails the candidate an activation link", token_value is not None,
      message["subject"] if message else
      f"nothing captured at {RECEIVER} — are the mail sender and receiver running?")

s, _ = call("POST", "/public/v1/candidate/activate",
            {"tenant": TENANT, "token": token_value or "none", "password": password})
check("the candidate can claim the account and set a password", s == 204, s)

s, tok = call("POST", "/mobile/v1/auth/token",
              {"tenant": TENANT, "email": email, "password": password, "deviceId": "candidate-phone"})
candidate_token = tok.get("accessToken") if s == 200 else None
check("the candidate signs in on their own tenant", candidate_token is not None,
      f"portal={tok.get('portal') if s == 200 else json.dumps(tok)[:90]}")

# ---- 4. HR walks the application to an offer ----
reached = []
for stage in ["Screening", "Shortlisted", "Interviewing", "Offered"]:
    s, moved = call("POST", f"/mobile/v1/recruitment/applications/{application_id}/stage",
                    {"toStage": stage, "reason": f"Chain check: {stage}"}, token=T)
    if s in (200, 201):
        reached.append(stage)
    else:
        print(f"     refused at {stage}: {json.dumps(moved)[:130]}")
        break

check("HR can walk the application to Offered", "Offered" in reached, " -> ".join(reached) or "none")

# ---- 5. Only the candidate can accept the offer ----
joining = (datetime.date.today() - datetime.timedelta(days=1)).isoformat()
s, accepted = call("POST", f"/candidate/v1/applications/{application_id}/offer",
                   {"accept": True, "joiningOn": joining}, token=candidate_token)
onboarding_id = d(accepted).get("resourceId") if s == 200 else None
check("the candidate accepts the offer from their own login", onboarding_id is not None,
      json.dumps(accepted)[:130] if s != 200 else f"onboarding {onboarding_id}")

# An offer belongs to one person: an HR token must not be able to accept it for them.
s, stolen = call("POST", f"/candidate/v1/applications/{application_id}/offer",
                 {"accept": True}, token=T)
check("nobody else can accept an offer on the candidate's behalf", s in (400, 401, 403),
      f"{s} {json.dumps(stolen)[:80]}")

# ---- 6. The candidate works through their onboarding checklist ----
form_values = {
    "PERSONAL_DETAILS": {"legalName": "Nikhil Rao", "dateOfBirth": "1990-04-12",
                         "gender": "Male", "maritalStatus": "Married"},
    "ADDRESS_EMERGENCY": {"address": "14 Mall Road, Chandigarh",
                          "emergencyContact": "Meera Rao", "emergencyPhone": "+919820011224"},
    "BANK_ACCOUNT": {"bank": "HDFC Bank", "accountNumber": "50100234567890", "ifsc": "HDFC0000123"},
}

scan = os.path.join(tempfile.gettempdir(), "scan.pdf")
with open(scan, "wb") as handle:
    handle.write(b"%PDF-1.4\n% scanned document\n%%EOF\n")

s, checklist = call("GET", f"/candidate/v1/onboarding/{onboarding_id}", token=candidate_token)
tasks = d(checklist)["tasks"] if s == 200 else []
check("the candidate sees their onboarding checklist", len(tasks) > 0,
      f"{len(tasks)} tasks, {d(checklist)['outstandingRequiredCount'] if s == 200 else '?'} required outstanding")

mine_required = [t for t in tasks if t["ownerKind"] == "Candidate" and t["isRequired"]]
submitted = 0
for task in mine_required:
    if task["taskType"] == "Form":
        s, why = call("POST", f"/candidate/v1/onboarding/{onboarding_id}/tasks/{task['id']}/form",
                      {"values": form_values.get(task["code"], {"answer": "Provided"})},
                      token=candidate_token)
    else:
        code = subprocess.run(
            ["curl", "-s", "-o", os.devnull, "-w", "%{http_code}", "-m", "40",
             "-X", "POST",
             f"{API}/candidate/v1/onboarding/{onboarding_id}/tasks/{task['id']}/document",
             "-H", f"Authorization: Bearer {candidate_token}",
             "-F", f"File=@{scan};type=application/pdf"],
            capture_output=True, text=True).stdout.strip()
        s, why = int(code or 0), code

    if s in (200, 201, 204):
        submitted += 1
    else:
        print(f"     {task['code']} refused: {json.dumps(why)[:110]}")

check("the candidate completes every required task of their own",
      submitted == len(mine_required), f"{submitted}/{len(mine_required)} submitted")

# ---- 7. HR reviews, and the last approval is what makes the hire ----
s, hr_view = call("GET", f"/mobile/v1/onboarding/{onboarding_id}", token=T)
hr_tasks = d(hr_view)["tasks"] if s == 200 else []
check("HR sees the submitted checklist", any(t["status"] == "Submitted" for t in hr_tasks),
      f"{sum(1 for t in hr_tasks if t['status'] == 'Submitted')} awaiting review")

approved = 0
for task in hr_tasks:
    if task["status"] != "Submitted":
        continue
    s, why = call("POST", f"/mobile/v1/onboarding/{onboarding_id}/tasks/{task['id']}/decision",
                  {"decision": "approve"}, token=T)
    if s == 200:
        approved += 1
    else:
        print(f"     approve {task['code']} refused: {json.dumps(why)[:110]}")

check("HR approves the candidate's evidence", approved == sum(
    1 for t in hr_tasks if t["status"] == "Submitted"), f"{approved} approved")

for task in hr_tasks:
    if task["ownerKind"] == "Hr" and task["isRequired"]:
        s, why = call("POST", f"/mobile/v1/onboarding/{onboarding_id}/tasks/{task['id']}/complete",
                      {"values": {"outcome": "Clear", "verifiedBy": "Priya Sharma"}}, token=T)
        check(f"HR records its own required task ({task['code']})", s == 200, json.dumps(why)[:110])

# ---- 8. Hiring provisions an employee, without anyone touching the database ----
linked = sql(f"SET NOCOUNT ON; SELECT ISNULL(MAX(e.Id),0) FROM employee.Employees e "
             f"JOIN recruitment.Candidates c ON c.Id=e.CandidateId WHERE c.Email='{email}';")
check("finishing onboarding provisions an employee record", linked not in ("", "0"),
      f"employee id {linked}")

# ---- 9. The board now shows the hire against the position ----
s, pipeline = call("GET", "/mobile/v1/recruitment/openings/pipeline", token=T)
row = next((o for o in d(pipeline)["openings"] if o["title"] == title), None) if s == 200 else None
check("the board shows the hire against the position",
      row is not None and row["hired"] >= 1, f"hired={row['hired'] if row else '?'}")

# ---- 10. A new employee has no mobile login until HR grants one ----
s, access = call("GET", "/mobile/v1/employees/mobile-access", token=T)
rows = d(access) if s == 200 else []
mine = next((r for r in rows if str(r["employeeId"]) == str(linked)), None)
check("the new employee appears on the mobile-access list", mine is not None,
      f"{len(rows)} employees listed")
check("and starts with no mobile login",
      bool(mine) and mine["state"] in ("NotGranted", "Blocked"),
      f"state={mine['state'] if mine else '?'} - {mine['explanation'] if mine else ''}")

# ---- 11. HR approves it ----
s, granted = call("POST", f"/mobile/v1/employees/mobile-access/{linked}/grant", {}, token=T)
credentials = d(granted) if s == 200 else None
check("HR can approve the employee's mobile login", credentials is not None,
      json.dumps(granted)[:130] if s != 200 else "")

if credentials:
    print(f"     issued: {credentials['userName']} / {credentials['temporaryPassword']}")

# ---- 12. The employee can now sign in to the mobile app ----
if credentials:
    s, tok = call("POST", "/mobile/v1/auth/token", {
        "userName": credentials["userName"],
        "password": credentials["temporaryPassword"],
        "deviceId": "employee-phone"
    })
    check("the employee signs in to the mobile app", s == 200 and "accessToken" in (tok or {}),
          json.dumps(tok)[:120] if s != 200 else "token issued")
    check("and is forced to replace the temporary password",
          s == 200 and tok.get("mustChangePassword") is True,
          f"mustChangePassword={tok.get('mustChangePassword') if s == 200 else '?'}")
    check("the employee login is scoped to the employee portal",
          s == 200 and (tok.get("portal") or "").lower() == "employee",
          f"portal={tok.get('portal') if s == 200 else '?'}")

    # ---- 12b. One person, one login ----
    # They applied with this address, so the grant moved that same account onto the Employee
    # portal rather than making a second one. The candidate login must not still be live.
    count = sql(f"SET NOCOUNT ON; SELECT COUNT(*) FROM [identity].Users WHERE Email='{email}';")
    check("the person still has exactly one account", count == "1", f"{count} accounts hold {email}")

    s4, _ = call("POST", "/mobile/v1/auth/token", {
        "tenant": TENANT, "email": email, "password": password, "deviceId": "candidate-phone"})
    check("their old candidate login no longer works", s4 >= 400, f"{s4}")

    portal = sql(f"SET NOCOUNT ON; SELECT CONCAT(Portal,'/',UserName) FROM [identity].Users WHERE Email='{email}';")
    check("the account now sits on the employee portal", portal.startswith("2/"), portal)

    # ---- 13. Granting twice must not quietly issue a second account ----
    s, again = call("POST", f"/mobile/v1/employees/mobile-access/{linked}/grant", {}, token=T)
    check("granting a second time is refused", s >= 400, f"{s} {json.dumps(again)[:90]}")

    # ---- 14. Suspending takes the login away, restoring gives it back ----
    call("POST", f"/mobile/v1/employees/mobile-access/{linked}/suspend", {}, token=T)
    s2, _ = call("POST", "/mobile/v1/auth/token", {
        "userName": credentials["userName"], "password": credentials["temporaryPassword"],
        "deviceId": "employee-phone"})
    check("suspending mobile access stops the login", s2 >= 400, f"{s2}")

    call("POST", f"/mobile/v1/employees/mobile-access/{linked}/restore", {}, token=T)
    s3, _ = call("POST", "/mobile/v1/auth/token", {
        "userName": credentials["userName"], "password": credentials["temporaryPassword"],
        "deviceId": "employee-phone"})
    check("restoring it gives the login back", s3 == 200, f"{s3}")

print(f"\n{sum(results)}/{len(results)} checks passed")
