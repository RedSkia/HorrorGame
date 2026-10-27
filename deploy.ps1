# Deploys HorrorGame/Builds/Web to Cloudflare Pages (called by deploy.cmd).
# The Cloudflare API token (permission: Account > Cloudflare Pages > Edit) and account ID are kept in Windows
# Credential Manager (Web Credentials, "HorrorGame Cloudflare": user name = account ID, password = token).
# Asked once; given only to this wrangler process as environment variables. -Reset forgets them.
param([switch]$Reset)
$ErrorActionPreference = 'Stop'
$Resource = 'HorrorGame Cloudflare'
$Build = Join-Path $PSScriptRoot 'HorrorGame\Builds\Web'

[void][Windows.Security.Credentials.PasswordVault, Windows.Security.Credentials, ContentType = WindowsRuntime]
$vault = New-Object Windows.Security.Credentials.PasswordVault

function Get-Saved {
    try { $c = $vault.FindAllByResource($Resource)[0]; $c.RetrievePassword(); $c } catch { $null }
}

if ($Reset) {
    $saved = Get-Saved
    if ($saved) { $vault.Remove((New-Object Windows.Security.Credentials.PasswordCredential($Resource, $saved.UserName, 'x'))) }
    Write-Host 'Cloudflare token removed from Credential Manager.'
}

if (-not (Test-Path (Join-Path $Build 'index.html'))) { Write-Host 'No web build found. Run build.cmd first.'; exit 1 }
$big = Get-ChildItem $Build -Recurse -File | Where-Object Length -ge 25MB
if ($big) { $big | ForEach-Object { Write-Host "Too big for Cloudflare Pages (25 MiB): $($_.FullName)" }; exit 1 }

$saved = Get-Saved
if (-not $saved) {
    Write-Host 'Cloudflare login (saved in Windows Credential Manager, asked only once).'
    $account = Read-Host 'Account ID'
    $secure = Read-Host 'API token (hidden)' -AsSecureString
    $token = [Net.NetworkCredential]::new('', $secure).Password
    if (-not $account -or -not $token) { Write-Host 'Account ID and token are required.'; exit 1 }
    $saved = New-Object Windows.Security.Credentials.PasswordCredential($Resource, $account, $token)
    $vault.Add($saved)
}

# Only this process (and wrangler, its child) sees the token.
$env:CLOUDFLARE_ACCOUNT_ID = $saved.UserName
$env:CLOUDFLARE_API_TOKEN = $saved.Password
npx --yes wrangler pages deploy $Build --project-name horrorgame --branch main --commit-dirty=true
exit $LASTEXITCODE
