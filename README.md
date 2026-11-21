# VInternships

A personal ASP.NET Core MVC portfolio demo for connecting students to companies and tracking internship placements, hours and supervisor feedback.

**Validation:** the personal version and mobile-card revision built successfully on Windows and passed all 25 regression groups on 21 November 2026. Supplied desktop/mobile captures were reviewed. The production container built and passed HTTP redirects, forwarded HTTPS, secure cookies, antiforgery, session separation and primary-page checks. GitHub Actions repeats the checks on Windows/Linux and in a Linux production container.

**Hosting:** Render deployment and hosted browser acceptance are the next step. A public URL will be recorded here only after the deployed service is verified.

## Try it locally on Windows

Requires the .NET 10 SDK. From this folder in PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Check-PersonalWorkspace.ps1
if ($LASTEXITCODE -eq 0) {
    powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-PersonalWorkspace.ps1
}
```

Open **http://127.0.0.1:5099** after the listening message. The initial personal-workspace installer runs these steps automatically. No database connection, account, password or email provider is required.

Add a fictional student and company, create a placement, then open its details to add hours and feedback. Every visitor can explore all demo features. This is a password-free demonstration, with no user roles or authentication.

## Workflows

- Student and company creation, details, editing and guarded deletion.
- Placements connected to saved student/company identities, with optional directory filters.
- Date validation, duplicate detection and editable placement details.
- Add 1–24 whole hours per submission, preserving the existing total. The cumulative limit is the smaller of 2,000 hours or the inclusive date range in hours.
- Plain-text feedback, up to 1,000 characters; blank input clears it.
- Version checks prevent stale updates and duplicate hour submissions from overwriting newer data.
- Every state-changing form requires antiforgery validation. User content is rendered with Razor encoding.
- Referenced students and companies cannot be deleted until their placements are removed.

## Disposable demo records

Records live in process memory, separately per browser session. A workspace expires 45 minutes after creation, even if active; seed identities are regenerated. Up to 20 live workspaces are supported; each is capped at 50 students, 25 companies and 75 placements. Reset replaces only that browser's records. Restarting the app discards all records.

Use fictional information only. Seed emails use `example.invalid` and nothing is emailed or submitted. This is not a recruitment service or a student records system. Sharing a browser cookie shares its workspace. Hosting logs are separate from the temporary app records.

Only cookie protection keys are written to `App_Data/PersonalDemo/Keys` locally, outside public assets. Windows keys use DPAPI; container keys stay in its temporary filesystem and rely on operating-system access permissions. Local scripts use a separate folder and port from other personal demos.

## Validation

The console checker contains **25 groups** covering rendering, validation, version conflicts, additive hours, parent references, deletion, isolation, reset, caps and expiry. It runs against in-process HTTP with independent cookie jars and temporary fixtures. Production-style HTTP-to-HTTPS redirection and secure-cookie form rendering are included. Test content-root configuration is supplied when the application builder is created.

Windows build, all 25 regression groups and the production container checks passed in user-supplied output. Static preparation additionally checked C# and PowerShell syntax, project XML, local asset references, original source hashes and packaging. Hosted acceptance and visual review of expanded navigation, forms and placement details remain separate from the supplied overview/list screenshots.

## Deploy the public demo

See [docs/DEPLOY-DEMO.md](docs/DEPLOY-DEMO.md) for Render setup. The root `render.yaml` defines one Free Docker web service, `vinternships-demo-st10174227`, with `/health` probes and deploys after CI checks pass. It does not create a database.

`Dockerfile.demo` runs as the unprivileged `app` user on port 8080. Render terminates HTTPS, and the service-specific forwarded-header setting lets ASP.NET issue secure cookies behind that managed ingress. The setting is scoped to the Render service and the loopback-only production test, rather than globally enabled in the image. Do not reuse it on an unrestricted server with a directly exposed app port.

Build and check the production container locally:

```powershell
docker build --file Dockerfile.demo --tag vinternships-demo:publication .
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-ProductionContainer.ps1
```

Sample records and cookie keys disappear when the service restarts; visitors can start a new sample workspace. Free hosting has idle spin-down and shared usage limits; consult the [Render Free documentation](https://render.com/docs/free). Generated records, keys, build output and private configuration are excluded from publication.

## Origin

See [PERSONAL_PROJECT_ORIGIN.md](PERSONAL_PROJECT_ORIGIN.md) for the original workflows and the personal additions. Original source hashes are recorded in `docs/SOURCE-MANIFEST.json`. Bootstrap and jQuery assets retain their upstream license files.
