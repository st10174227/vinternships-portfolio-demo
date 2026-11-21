# Validation status — 21 November 2026

User-supplied Windows results:

- Initial Release build and all 25 regression groups passed.
- Mobile-card update applied eight personal files with backups. Its Release build and all 25 regression groups passed again.
- The .NET 10 Linux Docker image built successfully.
- Plain HTTP probes redirected to HTTPS before cookies or forms were issued.
- Forwarded HTTPS served forms with Secure/HttpOnly session and antiforgery cookies.
- Tokenless submissions were rejected; a valid student was saved only in its own session.
- Primary production pages loaded. The temporary production test container was stopped.

Visual evidence reviewed: desktop overview and placement-list PDFs, initial mobile overview captures and revised mobile placement cards. The revised cards show student, status, position, company, dates, hours and action buttons without requiring table scrolling.

Not yet established: hosted Render readiness or behavior, GitHub Actions results for this new repository, and manual visual acceptance of expanded navigation, forms and placement details. Preparation of this publication package does not claim those results.

The preparation environment has no .NET SDK or Docker. Publication scripts rerun the build, regression groups and production container on the user's PC before creating the public repository. CI subsequently checks Windows/Linux and the production image.
