"""Nobody is reported on before the day they start.

A future joiner used to be counted in headcount and then, because absence is derived as
rostered minus present, marked absent from a job they had not begun. Somebody who joined
mid-month was measured against the whole month and came out as the worst attender in the
company for their first weeks.

This writes nothing. It needs a running host and a client with at least one employee whose
joining date is in the future — the check says so rather than passing vacuously if there is
none.

    SMOKE_BASE_URL=http://localhost:7000 \
    SMOKE_USERNAME=... SMOKE_PASSWORD=... \
    python scripts/verify-joining-dates.py
"""
import datetime
import json
import os
import subprocess
import urllib.error
import urllib.request

BASE = os.environ.get("SMOKE_BASE_URL", "http://localhost:7000").rstrip("/")
API = BASE + "/api"
SQL_SERVER = os.environ.get("SMOKE_SQL_SERVER", r"localhost\SQLEXPRESS")
SQL_DATABASE = os.environ.get("SMOKE_SQL_DATABASE", "KapsonHRManagement")

USERNAME = os.environ.get("SMOKE_USERNAME")
PASSWORD = os.environ.get("SMOKE_PASSWORD")

if not USERNAME or not PASSWORD:
    raise SystemExit("Set SMOKE_USERNAME and SMOKE_PASSWORD to an HR account.")

results = []


def check(label, ok, note=""):
    print(("  PASS  " if ok else "  FAIL  ") + label + (" | " + str(note)[:160] if note else ""))
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

s, tok = call("POST", "/mobile/v1/auth/token",
              {"userName": USERNAME, "password": PASSWORD, "deviceId": "joining"})
if s != 200:
    raise SystemExit(f"HR sign-in failed ({s}): {json.dumps(tok)[:200]}")
T = tok["accessToken"]

today = datetime.date.today()

# ---- Who has not started yet ----
rows = sql("SET NOCOUNT ON; SELECT CONCAT(Id,'|',EmployeeNumber,'|',JoinedOn) "
           "FROM employee.Employees WHERE IsActive = 1 AND JoinedOn > CAST(GETUTCDATE() AS date);")
future = [line.split("|") for line in rows.splitlines() if "|" in line]

check("the client has somebody who has not started yet", len(future) > 0,
      ", ".join(f"{number} joins {date}" for _, number, date in future) or
      "nothing to test — add an employee with a future joining date")

if not future:
    print(f"\n{sum(results)}/{len(results)} checks passed")
    raise SystemExit(1)

joiner_ids = {row[0] for row in future}
joiner_numbers = {row[1] for row in future}
active = int(sql("SET NOCOUNT ON; SELECT COUNT(*) FROM employee.Employees WHERE IsActive = 1;"))

# ---- The headline counts who is employed today, not who will be ----
s, headline = call("GET", "/mobile/v1/dashboard/headline", token=T)
kpis = {k["key"]: k for k in d(headline)["kpis"]} if s == 200 else {}

check("headcount leaves out anyone who has not started",
      kpis and kpis["total-employees"]["count"] == active - len(future),
      f"{kpis['total-employees']['count'] if kpis else '?'} counted, "
      f"{active} active rows, {len(future)} not started")

check("and nobody is marked absent from a job they have not begun",
      kpis and kpis["absent"]["count"] <= kpis["total-employees"]["count"],
      f"absent {kpis['absent']['count'] if kpis else '?'} of "
      f"{kpis['absent']['denominator'] if kpis else '?'} rostered")

# ---- The scope picker counts the same people the tiles do ----
s, scopes = call("GET", "/mobile/v1/dashboard/scopes", token=T)
check("the scope picker agrees with the headcount tile",
      s == 200 and d(scopes)["totalEmployees"] == kpis["total-employees"]["count"],
      f"picker {d(scopes)['totalEmployees'] if s == 200 else '?'} vs "
      f"tile {kpis['total-employees']['count'] if kpis else '?'}")

# ---- The month overview does not grade a month somebody was not here for ----
s, overview = call("GET", f"/mobile/v1/dashboard/attendance-overview?month={today:%Y-%m}", token=T)
overview_rows = d(overview)["rows"]["items"] if s == 200 else []
graded = [r for r in overview_rows if r["employeeId"] in joiner_ids]

check("a future joiner has no row in this month's overview", len(graded) == 0,
      f"{len(overview_rows)} rows, {len(graded)} of them people who have not started")

# ---- Somebody who joined this month is measured from their joining date ----
first = today.replace(day=1)
mid = sql("SET NOCOUNT ON; SELECT TOP 1 CONCAT(Id,'|',EmployeeNumber,'|',JoinedOn) "
          "FROM employee.Employees WHERE IsActive = 1 "
          "AND JoinedOn > DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1) "
          "AND JoinedOn <= CAST(GETUTCDATE() AS date) ORDER BY JoinedOn DESC;")

if "|" in mid:
    employee_id, number, joined = mid.split("|")
    row = next((r for r in overview_rows if r["employeeId"] == employee_id), None)
    days_in_month = (first.replace(day=28) + datetime.timedelta(days=4)).replace(day=1) - first

    check("somebody who joined mid-month owes fewer days than the whole month",
          row is not None and 0 < row["expectedDays"] < days_in_month.days,
          f"{number} joined {joined}: expected {row['expectedDays'] if row else '?'} days "
          f"of {days_in_month.days} in the month")
else:
    print("     (nobody joined mid-month, so the partial-month count is not exercised here)")

# ---- Nothing expected is not the same as attending nothing ----
unmeasured = [r for r in overview_rows if r["expectedDays"] == 0]
check("nobody with nothing expected of them is graded Poor",
      all(r["statusBand"] == "Not measured" for r in unmeasured),
      f"{len(unmeasured)} with no expected days: "
      f"{sorted({r['statusBand'] for r in unmeasured}) or 'none'}")

# ---- The reports read the same population ----
for label, path in [("late arrivals", "/mobile/v1/attendance/reports/late-arrivals"),
                    ("missing punches", "/mobile/v1/attendance/reports/miss-punch")]:
    s, report = call("GET", path, token=T)
    named = {r["employeeId"] for r in d(report)["rows"]["items"]} if s == 200 else set()
    check(f"{label} names nobody who has not started", not (named & joiner_ids),
          f"population {d(report)['scope']['populationCount'] if s == 200 else '?'}")

# ---- The directory still shows them: they are real records ----
s, page = call("GET", "/mobile/v1/employees?pageSize=100", token=T)
listed = {p["employeeNumber"] for p in d(page).get("items", [])} if s == 200 else set()
check("but the directory still lists them", joiner_numbers <= listed,
      f"{len(listed)} listed; missing {sorted(joiner_numbers - listed) or 'none'}")

print(f"\n{sum(results)}/{len(results)} checks passed")
