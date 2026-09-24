# Vispl HR Management — Solution Structure

## 1. Objective

Build a new, general-purpose, multi-tenant HR product. It is independent of the Crew Management product; only its layered naming convention is reused.

The first release runs through one executable, one process, and one Kestrel port: `Vispl.HRManagement.Host` on port `7000`. Module, identity, worker, routing, and data boundaries must nevertheless allow Admin, Recruitment, Candidate, and Employee to be deployed independently later without changing public URLs.

## 2. Architecture Style

- Modular monolith first; independently deployable modules later.
- One server startup project and one release initially.
- Modules are aligned across every layer.
- Public URLs remain stable when deployments are split.
- Public subdomain routing is performed by deployment infrastructure such as IIS, NGINX, or a cloud load balancer; YARP is not used.
- A central OpenID Connect identity module provides single sign-on. It runs inside Host initially and remains logically extractable.
- Each web portal owns a separate local session cookie.

## 3. Proposed Solution

```text
Kapson.HRManagement.sln
│
├── Vispl.HRManagement.Host
│   ├── Routing
│   ├── HealthChecks
│   ├── Configuration
│   └── Program.cs
│
├── Vispl.HRManagement.Composition
│   ├── Shared
│   │   └── HrManagementComposition.cs
│   ├── Admin
│   │   └── AdminModule.cs
│   ├── Recruitment
│   │   └── RecruitmentModule.cs
│   ├── Candidate
│   │   └── CandidateModule.cs
│   └── Employee
│       └── EmployeeModule.cs
│
├── Vispl.HRManagement.Identity
│   ├── Shared
│   │   ├── Abstractions
│   │   ├── DataProtection
│   │   └── Common
│   ├── Admin
│   │   ├── Authentication
│   │   ├── Authorization
│   │   ├── Tokens
│   │   ├── Cookies
│   │   └── AdminIdentityModule.cs
│   ├── Recruitment
│   │   └── RecruitmentIdentityModule.cs
│   ├── Candidate
│   │   └── CandidateIdentityModule.cs
│   └── Employee
│       └── EmployeeIdentityModule.cs
│
├── Vispl.HRManagement.Contracts
│   ├── Shared
│   ├── Admin
│   ├── Recruitment
│   ├── Candidate
│   └── Employee
│
├── Vispl.HRManagement.UI
│   ├── Components
│   │   ├── Shared
│   │   ├── Admin
│   │   ├── Recruitment
│   │   ├── Candidate
│   │   └── Employee
│   ├── Layout
│   └── wwwroot
│
├── Vispl.HRManagement.Mobile
│   ├── Shared
│   ├── Admin
│   ├── Recruitment
│   ├── Candidate
│   └── Employee
│
├── Vispl.HRManagement.UIVM
│   ├── Shared
│   ├── Admin
│   ├── Recruitment
│   ├── Candidate
│   └── Employee
│
├── Vispl.HRManagement.BM.ITF
│   ├── Shared
│   ├── Admin
│   ├── Recruitment
│   ├── Candidate
│   └── Employee
│
├── Vispl.HRManagement.BM
│   ├── Shared
│   ├── Admin
│   ├── Recruitment
│   ├── Candidate
│   └── Employee
│
├── Vispl.HRManagement.DL.ITF
│   ├── Shared
│   ├── Admin
│   ├── Recruitment
│   ├── Candidate
│   └── Employee
│
├── Vispl.HRManagement.DL
│   ├── Shared
│   ├── Admin
│   ├── Recruitment
│   ├── Candidate
│   └── Employee
│
├── Vispl.HRManagement.VO
│   ├── Shared
│   ├── Admin
│   ├── Recruitment
│   ├── Candidate
│   └── Employee
│
├── Vispl.HRManagement.Resx
├── Vispl.HRManagement.SeedExport
└── Vispl.HRManagement.Worker
    ├── Shared
    ├── Admin
    ├── Recruitment
    ├── Candidate
    └── Employee
```

`Host` is the only server executable in the initial solution. `Identity`, `Composition`, `UI`, `Worker`, BM, DL, and the other layers compile as DLLs loaded by Host; they are not separate servers. `Composition` owns module wiring so future thin hosts can reuse exactly the same registrations. Actual hostname-to-destination routing belongs to IIS, NGINX, a cloud load balancer, or equivalent infrastructure. Mobile is a separate API client and SeedExport is an offline tool.

Initial startup is always:

```bash
dotnet run --project Vispl.HRManagement.Host
```

This starts the complete server at `http://localhost:7000`; no second `dotnet run` is required for Identity or Worker.

## 4. Module Responsibilities

| Module | Responsibility |
|---|---|
| Admin | Tenant provisioning, licensing, themes, domains, master configuration, roles and platform settings |
| Recruitment | Requisitions, job openings, approvals, screening, interviews, offers and hiring workflow |
| Candidate | Public career site, candidate profile, applications, documents and application tracking |
| Employee | Employee master, organization, onboarding and later HR operations |
| Shared | Technical primitives and truly cross-cutting contracts only; no module-specific business logic |

Each module should contain its own pages, view models, business services, interfaces, persistence implementation, endpoints, authorization policies, and registrations.

### Future server roles

The future servers represent complete business slices, not technical layers:

| Server | Complete responsibility |
|---|---|
| Admin `:7101` | Vertex platform administration, tenant/company setup, subscriptions, feature entitlements, domains, themes, users, roles, permissions, masters, workflow configuration and audit access |
| Recruitment `:7102` | Internal hiring operations: requisitions, vacancies, publishing, screening, interview stages, evaluations, approvals, offers and recruiter dashboards |
| Candidate `:7103` | Public careers pages and candidate services: registration, profiles, CV/documents, job search, applications, application status, communication and offer response |
| Employee `:7104` | Post-hire HR operations: employee master, onboarding, documents, organization data, requests, approvals and later leave/attendance/payroll integrations |

For example, Recruitment Server is the full Recruitment slice:

```text
UI/Recruitment
UIVM/Recruitment
BM.ITF/Recruitment
BM/Recruitment
DL.ITF/Recruitment
DL/Recruitment
VO/Recruitment
Contracts/Recruitment
Identity/Recruitment
Worker/Recruitment
```

Ports therefore never map to `UI`, `BM`, `DL`, or database layers. Each separated host composes all the layers required by one business module.

## 5. Dependency Direction

```text
Web UI ----> UIVM ----> BM.ITF <---- BM ----> DL.ITF <---- DL
Mobile ----------------> HTTP API       |                    |
                                         v                    v
                                    Contracts                DB
```

Rules:

1. UI calls business interfaces; it does not access a `DbContext` or repository.
2. BM contains use cases and business rules.
3. DL implements persistence interfaces owned by the relevant module.
4. VO contains transport/value models and must not contain persistence behavior.
5. A module must not use another module's repository or `DbContext`.
6. Cross-module work uses an explicit application contract or domain/integration event.
7. Prefer focused use-case interfaces such as `IPublishJobOpening` over one very large service interface.
8. `Mobile` calls HTTP APIs only; it must never reference BM, DL, a `DbContext`, or SQL directly.
9. `Contracts` may be referenced across modules; another module's BM, DL, UI, UIVM, or internal VO implementation may not be referenced.
10. Shared folders must not become a dumping ground. Code belongs in Shared only when it is domain-neutral and used by multiple modules.

### Contracts project

`Vispl.HRManagement.Contracts` is the stable boundary used between modules and, later, between servers. It may contain:

- request and response DTOs;
- integration-event records;
- public service/client interfaces;
- shared identifiers and explicitly public enums;
- versioned API contract models.

It must not contain business-service implementations, repositories, EF entities, `DbContext` types, UI components, authentication handlers, or infrastructure code.

```text
Recruitment BM
    -> Contracts.Employee.IEmployeeOnboardingClient
        -> today: in-process Employee adapter
        -> later: authenticated HTTP Employee client
```

The business use case depends on the contract, so changing an in-process adapter to an HTTP adapter does not rewrite Recruitment logic.

Example cross-module flow:

```text
Recruitment publishes CandidateHired
                -> Employee handles CandidateHired
                -> Employee creates its own employee record
```

## 6. Route Composition

The Host calls the shared Composition entry point:

```csharp
builder.Services.AddHrManagement(builder.Configuration);
app.MapHrManagement();
```

Composition internally registers modules explicitly:

```csharp
builder.Services
    .AddAdminModule(configuration)
    .AddRecruitmentModule(configuration)
    .AddCandidateModule(configuration)
    .AddEmployeeModule(configuration);

app.MapAdmin();
app.MapRecruitment();
app.MapCandidate();
app.MapEmployee();
```

Suggested internal route groups:

```text
/admin/*
/recruitment/*
/candidate/*
/employee/*
/api/admin/*
/api/recruitment/*
/api/candidate/*
/api/employee/*
```

Route extensions live with their owning modules. This lets a future module host register only the module it serves.

A future Recruitment-only host should be as small as:

```csharp
builder.Services.AddRecruitmentModule(configuration);

app.MapRecruitment();
```

Host is a thin runtime entry point. Reusable composition lives in `Vispl.HRManagement.Composition`, not in Host, and Host must not contain recruitment, candidate, employee, admin, identity, or worker business implementations.

## 7. Stable Public Domains

Use subdomains from the first release, even when they resolve to the same server.

| Public domain | Surface |
|---|---|
| `identity.hr.example.com` | Central sign-in and OpenID Connect authority |
| `admin.hr.example.com` | Vertex/platform and tenant administration |
| `app.hr.example.com` | Recruiter and HR workspace |
| `candidate.hr.example.com` | Signed-in candidate portal |
| `jobs.hr.example.com` | Default public careers site |
| `{tenant}.jobs.hr.example.com` | Tenant-branded careers site |
| `api.hr.example.com` | Mobile and external APIs |

Production may use the product's purchased domain; these names are placeholders.

## 8. Deployment Plan

### Initial deployment: one physical server

```text
Internet
   |
IIS / NGINX / cloud edge
   |---- identity.hr.example.com  --┐
   |---- admin.hr.example.com     ---|
   |---- app.hr.example.com       ---|
   |---- candidate.hr.example.com ---|--> HR Host :7000
   |---- jobs.hr.example.com      ---|
   `---- api.hr.example.com       --┘
```

There is one application process: `Vispl.HRManagement.Host.exe`. Identity endpoints, all four modules, Razor UI/API endpoints, and hosted worker services execute inside that process. SQL Server remains an external dependency even when installed on the same physical machine.

Example internal ports:

| Process | Initial port |
|---|---:|
| Infrastructure edge | Public HTTP/HTTPS bindings |
| `Vispl.HRManagement.Host.exe` | 7000 |

Identity and Worker have no separate initial port or process.

### Later deployment: separated servers

Only infrastructure routing destinations and deployment settings change:

| Destination | Example port |
|---|---:|
| Admin Host | 7101 |
| Recruitment Host | 7102 |
| Candidate/Careers Host | 7103 |
| Employee Host | 7104 |
| API Host | 7105 |

The browser continues using the same public subdomains.

### Deployment ownership

```text
Today
IIS / NGINX / cloud edge
   └── HR Host :7000
       ├── Identity library
       ├── Admin module
       ├── Recruitment module
       ├── Candidate module
       ├── Employee module
       └── Worker hosted services

Later
IIS / NGINX / cloud edge
   ├── Identity Host (only if intentionally extracted)
   ├── Admin Host :7101
   ├── Recruitment Host :7102
   ├── Candidate Host :7103
   └── Employee Host :7104
```

Initially one application process runs on one physical machine. Later, thin module host executables can be generated and moved to different VMs, container services, or clusters without moving the underlying module source or changing client-facing URLs.

## 9. Authentication and Cookie Rules

YARP is deliberately excluded. IIS, NGINX, DNS/load-balancer rules, or equivalent infrastructure map each public hostname to its current host. None of these routing components may be used to share an application cookie among portals.

Use this model:

1. `identity.hr.example.com` is the single OpenID Connect/OAuth authority. Initially its endpoints are implemented by the Identity library but executed by Host on port `7000`.
2. Web portals use Authorization Code flow and create their own local `HttpOnly`, `Secure` cookie after sign-in.
3. Every portal uses a unique cookie name, authentication scheme, callback path, and data-protection application name.
4. Cookies are host-only. Do not configure a shared `.hr.example.com` cookie domain.
5. APIs and mobile clients use short-lived access tokens; they do not use portal cookies.
6. Signing into another portal reuses the Identity SSO session, but that portal still creates its own local cookie.
7. Edge routing forwards requests and standard proxy headers only. It does not issue, decrypt, or rewrite portal sessions.

Example cookie isolation:

| Portal | Cookie name |
|---|---|
| Admin | `__Host-Vispl.HR.Admin` |
| Recruitment | `__Host-Vispl.HR.Recruitment` |
| Candidate | `__Host-Vispl.HR.Candidate` |
| Employee | `__Host-Vispl.HR.Employee` |

Inside `Vispl.HRManagement.Identity`, keep portal registration separate:

```text
Identity
├── Shared
│   ├── Contracts
│   ├── Models
│   └── DataProtection
├── Admin
│   ├── Authentication
│   ├── Authorization
│   ├── Tokens
│   └── AdminIdentityModule.cs
├── Recruitment
├── Candidate
└── Employee
```

Use explicit registrations such as `AddAdminIdentity()` and `AddCandidateIdentity()`. Prefer specific contracts such as `IAdminTokenService` and `ICandidateTokenService`; do not register multiple ambiguous implementations of a flat `ITokenService`.

### Web versus mobile sessions

```text
Web browser -> OIDC login -> portal-specific local cookie
Mobile app  -> OIDC login -> access token + refresh token
```

Mobile requirements:

- use Authorization Code flow with PKCE;
- open authentication in the system browser, not an embedded password form;
- store refresh tokens only in platform secure storage;
- send access tokens to `api.hr.example.com` using the Bearer scheme;
- request module/API scopes, for example `candidate.read`, `candidate.write`, or `employee.leave`;
- support token revocation, device/session logout, expiry and refresh-token rotation;
- never copy web cookies into the mobile application.

Candidate, Employee and Recruitment are expected to be the main mobile areas. Admin should expose only carefully selected mobile use cases rather than reproducing the full platform console.

## 10. Data Boundaries

Start with one SQL Server and one physical database, using a schema and `DbContext` per module:

```text
platform.*      -> Admin
recruitment.*   -> Recruitment
candidate.*     -> Candidate
employee.*      -> Employee
identity.*      -> Identity
```

Rules:

- Each module owns its schema, migrations, entities, and repository implementations.
- Do not create ORM navigation properties across module boundaries; store scalar identifiers instead.
- Every tenant-owned record includes `TenantId`.
- Resolve tenant context on the server and apply global query filters where appropriate.
- Never trust a client-supplied `TenantId` without validating it against the authenticated principal/domain.
- A later database split should require connection-string and migration changes, not business-layer rewrites.

### Transactions and consistency

- A module may use a local database transaction only within its own schema/ownership boundary.
- Do not build a cross-module transaction that updates Recruitment and Employee tables together.
- Persist integration events using an outbox pattern when an operation and its event must succeed together.
- Consumers must be idempotent because a queued event may be delivered more than once.
- Use an inbox/processed-message record for handlers where duplicate processing would cause harm.
- Treat cross-module workflows as eventually consistent and expose processing status where users need visibility.

## 11. Multi-Tenant Theme and Careers Site

Admin owns a versioned tenant-site configuration containing:

- selected enterprise theme;
- logo, favicon, colors, typography and images;
- hero, content boxes and ordered page sections;
- tenant domain/subdomain mapping;
- publish status and version;
- feature/license entitlements.

The public jobs site renders only published configuration and published openings. Uploaded assets should be stored in object/file storage through an abstraction, not embedded in application binaries.

Suggested tenant resolution order:

1. validated custom domain or tenant jobs subdomain;
2. authenticated user's tenant claim for private portals;
3. explicit route value only for approved platform-admin operations.

Theme content should be draftable, previewable, versioned, publishable and reversible. A tenant must never be able to read or overwrite another tenant's configuration or assets.

## 12. Mobile Application Boundary

The mobile app is a client surface, not a fifth business server. It reuses the same APIs and business modules as the web portals.

```text
Android/iOS App
      |
      | HTTPS + OAuth access token
      v
Infrastructure edge / API
      |
      ├── /api/recruitment/*
      ├── /api/candidate/*
      └── /api/employee/*
            |
            v
        BM -> DL -> Database
```

Example mobile APIs:

```text
Candidate:   /api/candidate/profile
             /api/candidate/jobs
             /api/candidate/applications
             /api/candidate/documents

Employee:    /api/employee/profile
             /api/employee/leave
             /api/employee/documents
             /api/employee/requests

Recruitment: /api/recruitment/candidates
             /api/recruitment/interviews
             /api/recruitment/approvals
```

API responses should use versioned Contracts models and must not serialize database entities. Large files should use controlled upload/download endpoints or short-lived signed storage URLs.

## 13. Worker and Background Processing

Worker code follows the same Shared/Admin/Recruitment/Candidate/Employee division. Typical jobs include email/SMS delivery, document processing, career-site publication, interview reminders, exports, imports, event dispatch and integration synchronization.

Initially `Vispl.HRManagement.Worker` is a class library. Its `AddHrWorkers()` registration adds hosted services to Host, so jobs execute in the same process and require no separate startup command. Later, a thin Worker Host can reuse the same registrations if operational scaling requires it.

Rules:

- a job belongs to one owning module;
- job payloads use stable identifiers/contracts, not serialized EF entities;
- every job carries `TenantId`, correlation ID and initiating-user/system context where applicable;
- retries use bounded backoff and failed work moves to a dead-letter/error queue;
- handlers are idempotent and observable;
- splitting a module later permits its worker jobs to move with it.

## 14. Observability and Operational Rules

Every request, event and background job should carry a correlation/trace ID across the infrastructure edge, Host, Identity and Worker. Logs must include module and tenant context while excluding passwords, tokens and sensitive document content.

The initial Host executable exposes liveness and readiness checks for the combined application. Future extracted executables expose their own checks and verify only their required dependencies. Audit events for security and HR actions should be append-oriented and include actor, tenant, action, target, time and outcome.

Configuration and secrets must come from environment-specific configuration/secret storage. They must not be embedded in source code, mobile binaries, themes or tenant configuration records.

Data Protection keys must persist across restarts. The single-server development default uses Host's ignored `.data-protection-keys` directory. Production must use a protected durable key store; future multi-host deployments require an appropriately shared store only where cookie or token interoperability is intentionally required.

## 15. Extraction Rule

A module is ready to move to another server when:

- its endpoints are registered through one module extension;
- its services are registered through one module extension;
- it does not reference another module's DL project or `DbContext`;
- cross-module calls use contracts/events;
- it owns its database schema and migrations;
- it owns its authorization policies and local portal cookie configuration;
- its public route is already behind a stable subdomain;
- it can be started in a host that registers only that module.
- its worker jobs can run independently;
- its external and integration contracts are versioned;
- its health checks, configuration and observability can operate independently.

## 16. Recommended Build Order

1. Create the solution, layer projects, module folders, and dependency rules.
2. Implement Identity, tenant resolution, licensing, and portal-specific authentication.
3. Implement Admin theme/domain/master configuration.
4. Implement Recruitment job-opening management.
5. Implement public careers site and Candidate applications.
6. Add Employee onboarding through the `CandidateHired` event.
7. Add mobile Candidate/Employee flows against the same versioned APIs.
8. Add workers, notifications, exports, auditing, and integrations.
9. Prove extraction by running one module in a standalone test host before production separation is needed.

## 17. Non-Negotiable Decisions

- This is a new HR product; it has no Crew project references.
- It begins as a modular monolith, not as premature microservices.
- Public subdomains and module boundaries exist from day one.
- Infrastructure routing and authentication are separate concerns; YARP is not part of this solution.
- Host is the only initial server executable and listens on port `7000`.
- Identity and Worker are libraries hosted inside Host initially; neither requires a separate `dotnet run`.
- Reusable module wiring belongs to Composition, allowing thin module hosts to be added later.
- Portal cookies are isolated; SSO comes from the central Identity authority.
- Modules communicate through declared contracts/events, never through another module's persistence layer.
- Mobile is an API client, not a copy of server business logic.
- Each future server contains one complete business slice across all technical layers.
