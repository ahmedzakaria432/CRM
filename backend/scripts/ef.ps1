#requires -version 5.1
<#
.SYNOPSIS
    Wraps `dotnet ef` for one module of the modular monolith. Every module owns its own DbContext,
    SQL Server schema, and migrations, so every EF command names the module it acts on. Fixes the
    --project / --startup-project pair so it is not retyped (and mistyped) in every future story.
    Run from anywhere; paths are resolved relative to this script's location.

.PARAMETER Module
    PascalCase module name, matching the Crm.Modules.<Module>.* project names (e.g. Tickets).

.EXAMPLE
    ./scripts/ef.ps1 -Module Tickets migrations add AddTicketPriority
.EXAMPLE
    ./scripts/ef.ps1 -Module Tickets database update
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Module,

    [Parameter(Mandatory = $true, ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

$backendRoot = Split-Path -Parent $PSScriptRoot
$infrastructureProject = Join-Path $backendRoot "src/Modules/$Module/Crm.Modules.$Module.Infrastructure/Crm.Modules.$Module.Infrastructure.csproj"
$apiProject = Join-Path $backendRoot "src/Crm.Api/Crm.Api.csproj"

if (-not (Test-Path $infrastructureProject)) {
    Write-Error "No module named '$Module': $infrastructureProject does not exist."
    exit 1
}

# Migrations always land in the module's own Persistence/Migrations folder.
if ($Arguments.Length -ge 2 -and $Arguments[0] -eq 'migrations' -and $Arguments[1] -eq 'add' -and ($Arguments -notcontains '--output-dir')) {
    $Arguments += @('--output-dir', 'Persistence/Migrations')
}

& dotnet ef @Arguments --project $infrastructureProject --startup-project $apiProject
exit $LASTEXITCODE
