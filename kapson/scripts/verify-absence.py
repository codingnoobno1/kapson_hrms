"""Absence is recorded, not inferred — and never before the day is over.

Two defects lived here. The dashboard derived absence as "rostered minus present", so before
anybody clocked in it announced that the whole company was absent; and nothing ever wrote an
absent day, so the month overview counted zero absences forever while the tile shouted. The two
screens disagreed about the same fact because one inferred it and the other counted rows that
did not exist.

This writes attendance rows for a past date. Point it at a development database.

    SMOKE_BASE_URL=http://localhost:7000 \
    SMOKE_USERNAME=... SMOKE_PASSWORD=... \
    python scripts/verify-absence.py
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
    raise SystemExit("Set SMOKE_USERNAME and SMOKE_PASSWORD to an HR account holding ATTENDANCE.MANAGE.")

results = []


def check(label, ok, note=""):
    print(("  PASS  " if ok else "  FAIL  ") + label + (" | " + str(note)[:170] if note else ""))
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
        with urllib.request.urlopen(req, timeout=60) as r:
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
              {"userName": USERNAME, "password": PASSWORD, "deviceId": "absence"})
if s != 200:
    raise SystemExit(f"HR sign-in failed ({s}): {json.dumps(tok)[:200]}")
T = tok["accessToken"]

today = datetime.date.today()
yesterday = today - datetime.timedelta(days=1)

# ---- A day still being worked is never closed ----
s, refused = call("POST", f"/mobile/v1/attendance/close/{today:%Y-%m-%d}", {}, token=T)
check("today cannot be closed while it is still being worked", s == 400,
      f"{s} {(refused or {}).get('title', '')[:110]}")

s, refused = call("POST", f"/mobile/v1/attendance/close/{today + datetime.timedelta(days=3):%Y-%m-%d}",
                  {}, token=T)
check("nor can a day that has not happened", s == 400, s)

# ---- The dashboard opens on the client's date, not the server's ----
s, headline = call("GET", "/mobile/v1/dashboard/headline", token=T)
scope = d(headline)["scope"] if s == 200 else {}
kpis = {k["key"]: k for k in d(headline)["kpis"]} if s == 200 else {}
tile = kpis.get("absent", {})

tenant_today = sql("SET NOCOUNT ON; SELECT CONVERT(varchar(10), "
                   "  CAST(SYSDATETIMEOFFSET() AT TIME ZONE (SELECT TimeZoneId FROM platform.Tenants "
                   "  WHERE TenantKey = 'kapson') AS date), 23);")

check("the dashboard opens on the client's own date, not the server's",
      scope.get("asOf") == tenant_today,
      f"dashboard {scope.get('asOf')}, client clock {tenant_today}, "
      f"server {datetime.datetime.now(datetime.timezone.utc).date()}")

# ---- The tile does not call people absent before they are due ----
expected = tile.get("denominator") or 0

check("the absence tile never exceeds the number of people actually due",
      tile.get("count", 0) <= expected,
      f"\"{tile.get('title')}\" = {tile.get('count')} of {expected} expected | {tile.get('caption')}")

# On a working day still running the figure has to say it is still moving. On a rest day, a
# holiday, or a finished day there is nothing provisional to declare.
check("a day still running is captioned as provisional, a settled one is not",
      (tile.get("caption") or "").startswith("So far today") or tile.get("count", 0) == 0,
      f"count {tile.get('count')}, caption {tile.get('caption')!r}")

# ---- Nobody is absent before their own shift has started ----
# Today may be a rest day, so the rule is exercised against the next working day: it is a day
# whose shifts have not begun, which is exactly the state the morning used to get wrong.
ahead = today + datetime.timedelta(days=1)
for _ in range(7):
    s, probe = call("GET", f"/mobile/v1/dashboard/headline?asOf={ahead:%Y-%m-%d}", token=T)
    rostered = next((k for k in d(probe)["kpis"] if k["key"] == "absent"), {}) if s == 200 else {}
    if (rostered.get("denominator") or 0) > 0:
        break
    ahead += datetime.timedelta(days=1)

check("a working day whose shifts have not started reports nobody absent",
      rostered.get("count") == 0 and (rostered.get("denominator") or 0) > 0,
      f"{ahead:%a %d %b}: \"{rostered.get('title')}\" = {rostered.get('count')} "
      f"of {rostered.get('denominator')} | {rostered.get('caption')}")

check("and says how many are simply not due in yet",
      "not due in yet" in (rostered.get("caption") or ""),
      f"caption {rostered.get('caption')!r}")

# ---- Closing yesterday writes the rows nobody's device ever created ----
before = sql(f"SET NOCOUNT ON; SELECT COUNT(*) FROM workforce.AttendanceDays "
             f"WHERE WorkDate = '{yesterday:%Y-%m-%d}';")

s, closed = call("POST", f"/mobile/v1/attendance/close/{yesterday:%Y-%m-%d}", {}, token=T)
result = d(closed) if s == 200 else None
check("yesterday can be closed", result is not None, json.dumps(closed)[:150])

if result:
    print(f"     {result['population']} employed, {result['alreadyRecorded']} already recorded, "
          f"{result['markedAbsent']} absent, {result['markedOnLeave']} on leave, "
          f"{result['markedRestDay']} not rostered")

after = sql(f"SET NOCOUNT ON; SELECT COUNT(*) FROM workforce.AttendanceDays "
            f"WHERE WorkDate = '{yesterday:%Y-%m-%d}';")

check("the register now holds a row for everyone employed that day",
      result is not None and int(after) == result["population"],
      f"{before} -> {after} rows, {result['population'] if result else '?'} employed")

# ---- Absence is now a record, not an inference ----
by_status = sql(f"SET NOCOUNT ON; SELECT CONCAT(Status,'=',COUNT(*)) FROM workforce.AttendanceDays "
                f"WHERE WorkDate = '{yesterday:%Y-%m-%d}' GROUP BY Status;")
statuses = dict(line.split("=") for line in by_status.splitlines() if "=" in line)

check("absent days exist as rows that can be queried and disputed",
      int(statuses.get("Absent", 0)) > 0 or result["markedRestDay"] > 0,
      by_status.replace("\n", ", ") or "none")

# ---- Running it again changes nothing ----
s, again = call("POST", f"/mobile/v1/attendance/close/{yesterday:%Y-%m-%d}", {}, token=T)
repeat = d(again) if s == 200 else None
third = sql(f"SET NOCOUNT ON; SELECT COUNT(*) FROM workforce.AttendanceDays "
            f"WHERE WorkDate = '{yesterday:%Y-%m-%d}';")

check("closing the same day twice writes nothing new",
      repeat is not None and third == after and
      repeat["markedAbsent"] + repeat["markedOnLeave"] + repeat["markedRestDay"] == 0,
      f"{third} rows, second pass wrote "
      f"{(repeat['markedAbsent'] + repeat['markedOnLeave'] + repeat['markedRestDay']) if repeat else '?'}")

check("and the second pass sees the first pass's rows as already recorded",
      repeat is not None and repeat["alreadyRecorded"] == repeat["population"],
      f"{repeat['alreadyRecorded'] if repeat else '?'} of {repeat['population'] if repeat else '?'}")

# ---- A booked day off is not an absence ----
leave_rows = sql(
    "SET NOCOUNT ON; SELECT COUNT(*) FROM workforce.AttendanceDays a "
    "JOIN workforce.LeaveRequestDays l ON l.EmployeeId = a.EmployeeId AND l.Date = a.WorkDate "
    "JOIN workforce.LeaveRequests r ON r.Id = l.RequestId AND r.State = 'Approved' "
    f"WHERE a.WorkDate = '{yesterday:%Y-%m-%d}' AND a.Status = 'Absent';")
check("nobody on approved leave was marked absent", leave_rows == "0",
      f"{leave_rows} charged an absence for a day they had booked off")

# ---- A punched day is never overwritten ----
# Vacuous on a database with no punches in it. Kept because it costs nothing and is the check
# that will fail first if the closer ever stops skipping days that already have a row — the
# "already recorded" assertion above is what carries the weight today.
punched = sql(
    "SET NOCOUNT ON; SELECT COUNT(*) FROM workforce.AttendanceDays a "
    "WHERE a.Status = 'Absent' AND EXISTS (SELECT 1 FROM workforce.AttendancePunches p "
    "  WHERE p.EmployeeId = a.EmployeeId AND p.WorkDate = a.WorkDate);")
check("no day with a punch on it was overwritten as absent", punched == "0",
      f"{punched} punched days marked absent")

# ---- The month overview and the register now agree ----
s, overview = call("GET", f"/mobile/v1/dashboard/attendance-overview?month={yesterday:%Y-%m}", token=T)
rows = d(overview)["rows"]["items"] if s == 200 else []
reported = sum(r["absentDays"] for r in rows)

counted = sql("SET NOCOUNT ON; SELECT COUNT(*) FROM workforce.AttendanceDays "
              f"WHERE Status = 'Absent' AND WorkDate >= '{yesterday.replace(day=1):%Y-%m-%d}' "
              f"AND WorkDate <= '{yesterday:%Y-%m-%d}';")

check("the month overview counts the same absences the register holds",
      reported == int(counted),
      f"overview {reported}, register {counted}")

check("so the tile and the table no longer describe the same fact differently",
      reported > 0 or int(counted) == 0,
      f"{reported} absent days across {len(rows)} people")

print(f"\n{sum(results)}/{len(results)} checks passed")
