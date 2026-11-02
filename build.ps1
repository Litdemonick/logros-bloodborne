<#
.SYNOPSIS
    Compila Logros Bloodborne y, si está Inno Setup, arma el instalador.

.DESCRIPTION
    No necesita Visual Studio ni el SDK de .NET: usa el compilador de C# que trae Windows
    (.NET Framework 4.x). Resultado:
      bin\Logros Bloodborne.exe            el programa
      dist\Instalar-Logros-Bloodborne.exe  el instalador (si se encontró Inno Setup 6) + su .sha256

.PARAMETER RegenerarIcono
    Vuelve a dibujar assets\icono.ico (el ícono lo dibuja el propio programa).

.EXAMPLE
    .\build.ps1
#>
param([switch]$RegenerarIcono)
$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$src = Join-Path $raiz 'src\LogrosBloodborne'
$bin = Join-Path $raiz 'bin'
$icono = Join-Path $raiz 'assets\icono.ico'
$exe = Join-Path $bin 'Logros Bloodborne.exe'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'No encontré csc.exe (.NET Framework 4.x).' }

$refs = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Xml.dll') | ForEach-Object { "/r:$_" }
New-Item -ItemType Directory -Force $bin | Out-Null

function Compilar([string]$salida, [string[]]$extra) {
    $args = @('/nologo', '/target:winexe', '/optimize+', '/codepage:65001', '/warnaserror-') + $refs + $extra + @("/out:$salida", "/recurse:$src\*.cs")
    & $csc @args
    if ($LASTEXITCODE -ne 0) { throw "Falló la compilación ($salida)." }
}

if ($RegenerarIcono -or -not (Test-Path $icono)) {
    Write-Host 'Dibujando el ícono...'
    $tmp = Join-Path $bin 'icono-tmp.exe'
    Compilar $tmp @()
    New-Item -ItemType Directory -Force (Split-Path $icono) | Out-Null
    $p = Start-Process $tmp -ArgumentList '--icono', "`"$icono`"" -PassThru -Wait
    Remove-Item $tmp -Force
    if (-not (Test-Path $icono)) { throw 'No se pudo generar el ícono.' }
}

Write-Host 'Compilando el programa...'
Compilar $exe @("/win32icon:$icono")
Write-Host "  -> $exe"

# versión: una sola fuente, AssemblyFileVersion en Programa.cs
$version = ([regex]::Match((Get-Content (Join-Path $src 'Programa.cs') -Raw), 'AssemblyFileVersion\("(\d+\.\d+\.\d+)')).Groups[1].Value

$iscc = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if ($iscc) {
    Write-Host "Armando el instalador v$version..."
    & $iscc /Q "/DVersion=$version" (Join-Path $raiz 'installer\LogrosBloodborne.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Falló Inno Setup.' }
    $inst = Join-Path $raiz 'dist\Instalar-Logros-Bloodborne.exe'
    # huella para que el actualizador verifique la descarga
    $hash = (Get-FileHash $inst -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText("$inst.sha256", "$hash  Instalar-Logros-Bloodborne.exe`n")
    Write-Host "  -> $inst"
    Write-Host "  -> $inst.sha256"
} else {
    Write-Host 'Inno Setup 6 no está instalado: se compiló solo el programa (el instalador es opcional).'
}
