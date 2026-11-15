# Сборка приложения. Нужен .NET Framework 4.x (входит в Windows).
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "Не найден csc.exe" }

New-Item -ItemType Directory -Path (Join-Path $here "dist") -Force | Out-Null

& $csc /nologo /codepage:65001 /target:winexe `
    /out:(Join-Path $here "dist\Просвет.exe") `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    /win32icon:(Join-Path $here "src\icon.ico") `
    /win32manifest:(Join-Path $here "src\app.manifest") `
    (Join-Path $here "src\Program.cs")

Write-Host "Готово: dist\Просвет.exe"
