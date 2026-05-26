#requires -Version 5.1
<#
.SYNOPSIS
    Provisions the SQS queue inside the running LocalStack container.

.DESCRIPTION
    docker-compose launches LocalStack but does not seed any queues. This script
    creates the queue that the API publishes to and the Polling Service consumes
    from. Safe to re-run — `create-queue` is idempotent on a fixed name.

.PARAMETER QueueName
    Name of the SQS queue. Defaults to "toolbox-run-requests" to match .env.example.

.PARAMETER Endpoint
    LocalStack endpoint URL. Defaults to http://localhost:4566.
#>
[CmdletBinding()]
param(
    [string]$QueueName = "toolbox-run-requests",
    [string]$Endpoint  = "http://localhost:4566"
)

$ErrorActionPreference = "Stop"

Write-Host "Creating SQS queue '$QueueName' on $Endpoint..." -ForegroundColor Cyan

# LocalStack accepts any credentials but requires them to be set.
$env:AWS_ACCESS_KEY_ID     = "test"
$env:AWS_SECRET_ACCESS_KEY = "test"
$env:AWS_DEFAULT_REGION    = "us-east-1"

$awsCli = Get-Command aws -ErrorAction SilentlyContinue
if (-not $awsCli) {
    Write-Host "AWS CLI not found locally — using docker exec into the LocalStack container." -ForegroundColor Yellow
    docker exec toolbox-localstack `
        awslocal sqs create-queue --queue-name $QueueName | Out-Host
} else {
    aws --endpoint-url $Endpoint sqs create-queue --queue-name $QueueName | Out-Host
}

Write-Host "Queue ready." -ForegroundColor Green
Write-Host "List existing queues to verify:" -ForegroundColor DarkGray
if (-not $awsCli) {
    docker exec toolbox-localstack awslocal sqs list-queues
} else {
    aws --endpoint-url $Endpoint sqs list-queues
}
