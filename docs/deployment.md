# Deployment

This document covers the path from a working local dev environment to a production install.

## What goes where

| Component        | Runtime                       | Where                                         |
|------------------|-------------------------------|-----------------------------------------------|
| UI               | Nginx in container            | AWS (ECS / App Runner) behind your ingress    |
| API              | ASP.NET in container          | AWS (ECS / App Runner) behind your ingress    |
| Database         | PostgreSQL                    | Managed (RDS) or existing on-prem cluster     |
| AWS SQS          | AWS managed                   | AWS account that hosts UI + API               |
| Polling Service  | Windows Service               | On-prem Windows host that stores the .exes    |
| Console apps     | Plain .exe                    | `C:\ToolboxApps\<AppName>\` on the same host  |
| Logs             | New Relic                     | Account already used by Listrak               |

## Prerequisites

- AWS account with permission to create: SQS queue, ECR repository, ECS cluster (or App Runner service), IAM role for OIDC from GitHub.
- An on-prem Windows host. Server 2019 or newer. Joined to the AD domain.
- A gMSA pre-provisioned by AD admins. See [gmsa-setup.md](gmsa-setup.md).
- New Relic license key in the secrets vault.

## One-time AWS setup

1. **SQS queue + DLQ**

   ```bash
   aws sqs create-queue --queue-name toolbox-run-requests-dlq \
       --attributes MessageRetentionPeriod=1209600

   DLQ_ARN=$(aws sqs get-queue-attributes \
       --queue-url $(aws sqs get-queue-url --queue-name toolbox-run-requests-dlq --query QueueUrl --output text) \
       --attribute-names QueueArn --query Attributes.QueueArn --output text)

   aws sqs create-queue --queue-name toolbox-run-requests \
       --attributes VisibilityTimeout=600,RedrivePolicy="{\"deadLetterTargetArn\":\"$DLQ_ARN\",\"maxReceiveCount\":\"3\"}"
   ```

2. **IAM role for GitHub Actions (OIDC)** — trust GitHub's OIDC issuer, attach permissions for ECR push and ECS update-service. The role ARN is set as the `AWS_DEPLOY_ROLE_ARN` secret.

3. **ECR repos**: `toolbox-manager-api`, `toolbox-manager-ui`.

4. **ECS cluster + services** (or App Runner). Set the following environment on the API task:

   ```
   ASPNETCORE_ENVIRONMENT=Production
   ConnectionStrings__Default=<your prod DSN>
   Aws__Region=us-east-1
   Aws__SqsQueueName=toolbox-run-requests
   Aws__SqsServiceUrl=                                     # empty in prod
   NEW_RELIC_LICENSE_KEY=<from secrets manager>
   NEW_RELIC_APP_NAME=toolbox-manager-api
   ```

   For SQS access, give the task role `sqs:SendMessage` on the queue ARN. No static credentials.

5. **GitHub repository variables / secrets**:

   | Variable / Secret           | Type     | Used by                       |
   |-----------------------------|----------|-------------------------------|
   | `AWS_DEPLOY_ROLE_ARN`       | secret   | api.yml, ui.yml                |
   | `AWS_REGION`                | variable | api.yml, ui.yml                |
   | `ECS_CLUSTER`               | variable | api.yml, ui.yml                |
   | `ECS_API_SERVICE`           | variable | api.yml                        |
   | `ECS_UI_SERVICE`            | variable | ui.yml                         |
   | `API_BASE_URL`              | variable | ui.yml (baked at build time)   |

## Deploying

### UI + API

Both deploy automatically on push to `main` when their respective paths change. See:

- [.github/workflows/api.yml](../.github/workflows/api.yml)
- [.github/workflows/ui.yml](../.github/workflows/ui.yml)

To force a redeploy with no code change, click **Run workflow** in the Actions tab.

### Polling Service

The polling service is built into a Windows artifact by [.github/workflows/polling-service.yml](../.github/workflows/polling-service.yml) on every push to `main`. Tag-pushes (`polling-v*`) create a GitHub Release with the zip attached.

To deploy:

1. Download the latest `polling-service-<sha>.zip` from the workflow run or release.
2. On the on-prem Windows host:

   ```powershell
   # Stop & uninstall the existing service
   sc.exe stop  ToolboxManagerPollingService
   sc.exe delete ToolboxManagerPollingService

   # Unzip into Program Files
   Expand-Archive -Path .\polling-service-*.zip `
                  -DestinationPath 'C:\Program Files\ToolboxManager\PollingService' `
                  -Force

   # Set the production config — see appsettings.json keys to override
   $env:Aws__SqsServiceUrl = ''               # use real AWS, not LocalStack
   # … or edit appsettings.Production.json placed alongside the EXE

   # Re-install under the gMSA
   sc.exe create ToolboxManagerPollingService `
       binPath= "`"C:\Program Files\ToolboxManager\PollingService\ToolboxManager.PollingService.exe`"" `
       obj=     "DOMAIN\gMSA-Toolbox$" `
       password= "" `
       start=   auto

   sc.exe start ToolboxManagerPollingService
   ```

3. Verify in Seq / New Relic that the service logged `Polling Service started…`.

This deployment is manual because it touches AD-bound infrastructure outside the GitHub Actions blast radius. If you'd rather automate it, a self-hosted GitHub runner on the same host could run a deploy workflow that does the same `sc.exe` dance.

## New Relic logging

`NLog.NewRelic` is not wired into the scaffold because it's not needed locally; production picks up structured stdout via the New Relic .NET agent. Install the agent on each container / host:

- **API container**: see Dockerfile — add the New Relic .NET agent layer when ready.
- **Polling Service host**: run the New Relic infrastructure + .NET agent installer; point it at the EXE.

The same NLog config produces the events; the agent forwards them. No code change.

## Database migrations

`MigrationRunner` in the API applies migrations on startup. The migration SQL files are baked into the API container (see csproj `<Content Include="..\..\..\database\migrations\*.sql">`). To add a migration in production, ship a new image — same path as any other API change.

## Smoke test after deploy

1. `GET https://<api>/health` — expect `Healthy`.
2. Open the UI, register a test app (you can leave the executable path pointing at something that doesn't exist for this check), click Run, and look for the run to land in `Failed` with `Executable not found at …` in the error message. That confirms: UI → API → SQS → Polling Service → API callback is wired up end-to-end.
3. Then point at a real executable.
