<#
.SYNOPSIS
    Full backup of SQL Server databases, verified, with retention and a copy off the machine. Meant for a clinic
    server on SQL Server Express, which has no SQL Agent to schedule backups.

.DESCRIPTION
    For each database: BACKUP DATABASE ... WITH CHECKSUM, then RESTORE VERIFYONLY. The .bak files go to the instance's
    default backup folder: SQL Server's service account writes them, not the account running this script, and that
    folder is one it can write. Each run adds <database>_<yyyyMMdd_HHmmss>.bak; once a database's new backup is
    verified, its files older than -KeepDays are deleted (in that folder and in -CopyTo, never the newest one). Only
    file names this script creates are touched.

    A database that fails doesn't stop the others, but the script then exits with 1, so Task Scheduler shows the run
    as failed. Every run appends to the log (-LogPath).

    Needs sqlcmd (installed with SQL Server). SQL Server Express can't compress backups, so they aren't compressed.

.PARAMETER Databases
    One or more database names. "A,B" in one argument also works (powershell.exe -File passes it as one string).

.PARAMETER BackupFolder
    Where SQL Server writes the .bak files. Default: the instance's default backup folder.

.PARAMETER CopyTo
    A second copy, away from this disk: a USB drive or a cloud-synced folder.

.EXAMPLE
    .\backup-databases.ps1 -Databases ClinicDb -CopyTo 'E:\Backups'

.EXAMPLE
    A daily task as SYSTEM, registered once from an elevated PowerShell. -StartWhenAvailable runs a missed backup at
    the next start when the PC was off at that time.

    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -ExecutionPolicy Bypass -File "C:\Backups\backup-databases.ps1" -Databases ClinicDb -CopyTo "E:\Backups"'
    $trigger = New-ScheduledTaskTrigger -Daily -At 13:00
    $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 1)
    Register-ScheduledTask -TaskName 'SQL backups' -Action $action -Trigger $trigger -Settings $settings -User 'SYSTEM' -RunLevel Highest

.NOTES
    The account that runs it needs BACKUP DATABASE in each database and CREATE DATABASE (RESTORE VERIFYONLY asks for
    it). For SYSTEM, which isn't sysadmin on current SQL Server versions, an administrator runs once:

    IF SUSER_ID(N'NT AUTHORITY\SYSTEM') IS NULL CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS;
    GRANT CREATE ANY DATABASE TO [NT AUTHORITY\SYSTEM];
    USE [ClinicDb]; CREATE USER [NT AUTHORITY\SYSTEM]; ALTER ROLE db_backupoperator ADD MEMBER [NT AUTHORITY\SYSTEM];

    (the last line again for each database added later). A backup only counts once it has been restored: restore one
    under another name now and then and check the data.
#>
[CmdletBinding()]
param(
    [string]$Instance = 'localhost\SQLEXPRESS',
    [Parameter(Mandatory = $true)][string[]]$Databases,
    [string]$BackupFolder,
    [string]$CopyTo,
    [ValidateRange(1, 3650)][int]$KeepDays = 30,
    [string]$LogPath = (Join-Path $PSScriptRoot 'backup-databases.log')
)

$ErrorActionPreference = 'Stop'

function Write-Log([string]$Message) {
    $line = '{0:yyyy-MM-dd HH:mm:ss} {1}' -f (Get-Date), $Message
    Write-Output $line
    Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
}

# Runs T-SQL with Windows authentication and returns its output; throws with sqlcmd's message on an error.
# -C trusts the server's certificate: the instance is on this machine, recent sqlcmd versions encrypt by default and
# SQL Server Express's certificate is self-signed. -x: no sqlcmd variables, so a "$(" in a name stays literal.
function Invoke-Sql([string]$Query) {
    $ErrorActionPreference = 'Continue'
    $output = & sqlcmd -S $Instance -E -C -b -x -h -1 -W -Q "SET NOCOUNT ON; $Query" 2>&1
    if ($LASTEXITCODE -ne 0) { throw (($output | Out-String).Trim()) }
    return $output
}

# Deletes this script's backups of the database older than -KeepDays (by the date in the name), never the newest.
function Remove-OldBackups([string]$Folder, [string]$Database) {
    $pattern = '^' + [regex]::Escape($Database) + '_(\d{8}_\d{6})\.bak$'
    $limit = (Get-Date).AddDays(-$KeepDays)
    $files = @(Get-ChildItem -LiteralPath $Folder -Filter '*.bak' -File | Where-Object { $_.Name -match $pattern } | Sort-Object Name)
    foreach ($file in ($files | Select-Object -SkipLast 1)) {
        $null = $file.Name -match $pattern
        if ([datetime]::ParseExact($Matches[1], 'yyyyMMdd_HHmmss', $null) -lt $limit) {
            Remove-Item -LiteralPath $file.FullName
            Write-Log "  Deleted $($file.FullName)"
        }
    }
}

$Databases = @($Databases | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$logFolder = Split-Path -Parent $LogPath
if ($logFolder) { New-Item -ItemType Directory -Force -Path $logFolder | Out-Null }

Write-Log "Start: $Instance, $($Databases -join ', ')"
try {
    if (-not $BackupFolder) {
        $BackupFolder = "$(Invoke-Sql "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000))" | Select-Object -First 1)".Trim()
        if (-not $BackupFolder -or $BackupFolder -eq 'NULL') { throw 'The instance has no default backup folder: pass -BackupFolder.' }
    }
    if ($CopyTo) { New-Item -ItemType Directory -Force -Path $CopyTo | Out-Null }
}
catch {
    Write-Log "FAILED: $($_.Exception.Message)"
    exit 1
}

$failed = @()
foreach ($database in $Databases) {
    try {
        $nameSql = $database.Replace("'", "''")
        $quoted = '[' + $database.Replace(']', ']]') + ']'
        $recovery = "$(Invoke-Sql "SELECT recovery_model_desc FROM sys.databases WHERE name = N'$nameSql'" | Select-Object -First 1)".Trim()
        if (-not $recovery) { throw 'There is no such database on this instance.' }

        $path = Join-Path $BackupFolder ('{0}_{1:yyyyMMdd_HHmmss}.bak' -f $database, (Get-Date))
        $pathSql = $path.Replace("'", "''")
        Invoke-Sql "BACKUP DATABASE $quoted TO DISK = N'$pathSql' WITH CHECKSUM, INIT" | Out-Null
        Invoke-Sql "RESTORE VERIFYONLY FROM DISK = N'$pathSql' WITH CHECKSUM" | Out-Null
        Write-Log ('OK {0}: {1} ({2:N1} MB, verified)' -f $database, $path, ((Get-Item -LiteralPath $path).Length / 1MB))
        if ($recovery -eq 'FULL') {
            Write-Log "  Note: $database uses the FULL recovery model; without log backups (this script takes none) its log file keeps growing. SIMPLE fits daily full backups."
        }
        Remove-OldBackups -Folder $BackupFolder -Database $database

        if ($CopyTo) {
            Copy-Item -LiteralPath $path -Destination $CopyTo -Force
            Write-Log "  Copied to $CopyTo"
            Remove-OldBackups -Folder $CopyTo -Database $database
        }
    }
    catch {
        $failed += $database
        Write-Log "FAILED ${database}: $($_.Exception.Message)"
    }
}

if ($failed.Count -gt 0) {
    Write-Log "Done with errors: $($failed -join ', ')"
    exit 1
}
Write-Log 'Done.'
exit 0
