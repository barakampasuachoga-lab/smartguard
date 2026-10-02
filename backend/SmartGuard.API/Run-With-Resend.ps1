$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot 'SmartGuard.API.csproj'
$listener = Get-NetTCPConnection -LocalPort 5156 -State Listen -ErrorAction SilentlyContinue
if ($listener) {
    throw 'SmartGuard API is already using port 5156. Stop the existing API process, then run this script again.'
}

$senderAddress = (Read-Host 'Verified sender email address (on your Resend-verified domain)').Trim()
if ([string]::IsNullOrWhiteSpace($senderAddress) -or $senderAddress -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$') {
    throw 'Enter a valid sender email address on your verified domain.'
}

$secureApiKey = Read-Host 'Resend API key (input is hidden)' -AsSecureString
$apiKeyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureApiKey)
$apiKey = $null

try {
    $apiKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($apiKeyPointer)
    $env:Email__Smtp__Host = 'smtp.resend.com'
    $env:Email__Smtp__Port = '465'
    $env:Email__Smtp__Username = 'resend'
    $env:Email__Smtp__Password = $apiKey
    $env:Email__Smtp__FromAddress = $senderAddress
    $env:Email__Smtp__FromName = 'SmartGuard'
    $env:Email__Smtp__EnableSsl = 'true'

    Write-Host 'Starting SmartGuard API with Resend email delivery. Press Ctrl+C to stop it.'
    dotnet run --project $projectPath --no-build
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($apiKeyPointer)
    $apiKey = $null
    $secureApiKey.Dispose()
    @(
        'Email__Smtp__Host',
        'Email__Smtp__Port',
        'Email__Smtp__Username',
        'Email__Smtp__Password',
        'Email__Smtp__FromAddress',
        'Email__Smtp__FromName',
        'Email__Smtp__EnableSsl'
    ) | ForEach-Object { Remove-Item "Env:$_" -ErrorAction SilentlyContinue }
}
