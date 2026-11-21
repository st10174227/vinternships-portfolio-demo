# Deploy VInternships on Render

## Before deployment

The publication script creates only `st10174227/vinternships-portfolio-demo`, after checking a separate personal source copy. Wait for the Windows, Linux and production-container jobs in GitHub Actions to pass. Those checks run again on the exact source being published.

The supplied Windows output confirms a Release build, 25 regression groups and a production container check. Supplied desktop overview/list and mobile-card captures were reviewed. Expanded navigation, placement forms/details and hosted behavior still need manual browser acceptance.

## Create the service

1. In Render, choose **New → Blueprint**.
2. Connect `st10174227/vinternships-portfolio-demo` and choose branch `main`.
3. Enter Blueprint Name `vinternships-portfolio-demo`. Keep Blueprint Path `render.yaml`.
4. The configuration should create one Docker web service, `vinternships-demo-st10174227`, on the **Free** instance plan. It should not create a database or paid disk.
5. Deploy the Blueprint and wait for the service to report Live.
6. Copy the actual service URL. If its hostname differs from `vinternships-demo-st10174227.onrender.com`, update the `AllowedHosts` value in `render.yaml` to the actual hostname and resync. Never widen it to `*` as a workaround.

Blueprint fields were checked against [Render's current YAML reference](https://render.com/docs/blueprint-spec) on 21 November 2026. `autoDeployTrigger: checksPass` makes later deploys wait for CI checks. The service uses `/health`, Docker context `.`, Dockerfile `Dockerfile.demo`, internal port 8080 and standard external HTTPS.

## Test the live demo

- Open `/health`: expect `status: ok` and `application: vinternships-demo`.
- Open the homepage in an incognito window. All features should work without passwords or connection strings.
- Create a fictional student/company/placement. Add 6 hours, then 4; confirm 10. Save and clear fictional feedback. Check dates, duplicate handling and linked deletion errors.
- Use a second independent browser session and confirm custom records are absent. Reset one workspace and confirm the other retains its records.
- Review the mobile navigation, cards, placement forms/details and desktop tables. Do not enter real student information.
- After a restart, refresh the page before submitting a form. Old cookies and form tokens can expire when temporary keys are replaced.
- Record the verified HTTPS URL in the README only after hosted acceptance passes. Do not label a proposed URL as live.

Render Free services spin down after 15 minutes without traffic and wake on the next request; startup can take about a minute. Free instance hours are shared across services in a workspace, with 750 per calendar month. Local files are discarded on restarts, redeploys and spin-down. See [Render's Free service documentation](https://render.com/docs/free) and monitor the existing workspace's usage alongside the CMCS and ABC Retail demos.

No LinkedIn or CV changes are included in publication.
