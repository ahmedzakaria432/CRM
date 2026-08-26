#requires -version 5.1
<#
.SYNOPSIS
    Wraps `dotnet ef` with this repo's fixed --project / --startup-project pair, so it is not
    retyped (and mistyped) in every future story. Run from anywhere; paths are resolved relative
    to this script's location.

.EXAMPLE
    ./scripts/ef.ps1 migrations add AddCustomer
.EXAMPLE
    ./scripts/ef.ps1 database update
#>
param(
    [Parameter(Mandatory = $true, ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

$backendRoot = Split-Path -Parent $PSScriptRoot
$infrastructureProject = Join-Path $backendRoot "src/Crm.Infrastructure/Crm.Infrastructure.csproj"
$apiProject = Join-Path $backendRoot "src/Crm.Api/Crm.Api.csproj"

& dotnet ef @Arguments --project $infrastructureProject --startup-project $apiProject
exit $LASTEXITCODE
