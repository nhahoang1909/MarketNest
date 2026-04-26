param(
    [string]$Title,
    [string]$Status = "Completed",
    [string]$Description = "",
    [string]$Pr = "",
    [string]$TargetFile = ""
)

# Default target file: ../docs/project_notes/issues.md relative to script location
if ([string]::IsNullOrWhiteSpace($TargetFile)) {
    $TargetFile = Join-Path -Path $PSScriptRoot -ChildPath "..\docs\project_notes\issues.md"
}

$TargetFile = Resolve-Path -Path $TargetFile | Select-Object -ExpandProperty Path

if (-not (Test-Path $TargetFile)) {
    Write-Error "Target file not found: $TargetFile"
    exit 2
}

# Determine date and title line
$date = Get-Date -Format yyyy-MM-dd
if ([string]::IsNullOrWhiteSpace($Title)) {
    $titleLine = "### $date - Unspecified change"
} else {
    # If Title already starts with a date, keep as-is; otherwise prefix with date
    if ($Title -match '^[0-9]{4}-[0-9]{2}-[0-9]{2}') {
        $titleLine = "### $Title"
    } else {
        $titleLine = "### $date - $Title"
    }
}

$lines = @()
$lines += $titleLine
$lines += "- **Status**: $Status"
if (-not [string]::IsNullOrWhiteSpace($Description)) { $lines += "- **Description**: $Description" }
if (-not [string]::IsNullOrWhiteSpace($Pr)) { $lines += "- **PR/Issue**: $Pr" }
$lines += "- **Notes**: Logged by assistant via scripts/log_project_memory.ps1"
$lines += ""

# Append an entry separator if file does not already end with a blank line
[void]$null
Add-Content -Path $TargetFile -Value "---" -Encoding UTF8
Add-Content -Path $TargetFile -Value $lines -Encoding UTF8

Write-Host ("Appended project note to {0}`n" -f $TargetFile) -ForegroundColor Green
foreach ($line in $lines) { Write-Host $line }

