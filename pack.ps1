# 양쪽 게임 버전으로 빌드해 IL이 같은지 확인한 뒤 dist/AutoFingering-<version>.zip을 만든다.
# 구버전으로 만든 DLL을 배포하므로, 신버전에만 있는 멤버를 실수로 쓰면 여기서 빌드나 비교가 실패한다.
# UMM이 설치된 게임 두 벌(R145, R150)이 필요하다. 한 벌뿐이면 README의 빌드 절을 본다.
param(
    [string]$GameOld = 'F:\SteamLibrary\steamapps\common\A Dance of Fire and Ice',
    [string]$GameNew = 'D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice',
    [string]$IlSpy = $(if ($env:ILSPYCMD) { $env:ILSPYCMD } else { 'ilspycmd' })
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'src\AutoFingering.csproj'
$infoPath = Join-Path $root 'src\Info.json'
$work = Join-Path ([IO.Path]::GetTempPath()) ('AutoFingering-pack-' + [Guid]::NewGuid().ToString('N'))

# 긴 컴파일 오류 대신 빠진 것을 바로 알려 준다.
foreach ($game in $GameOld, $GameNew) {
    foreach ($file in 'Assembly-CSharp.dll', 'UnityModManager\UnityModManager.dll') {
        $path = Join-Path $game "A Dance of Fire and Ice_Data\Managed\$file"
        if (-not (Test-Path $path)) { throw "게임이나 UMM 파일이 없음: $path (-GameOld, -GameNew로 지정)" }
    }
}
if (-not (Get-Command $IlSpy -ErrorAction SilentlyContinue)) {
    throw "ilspycmd를 찾을 수 없음: $IlSpy (dotnet tool install -g ilspycmd 후 -IlSpy로 지정)"
}

function Build-Against([string]$label, [string]$gameDir) {
    $out = Join-Path $work $label
    # obj를 분리해야 참조만 다른 두 빌드가 서로의 중간 결과를 재사용하지 않는다.
    # 끝을 슬래시로 둔다. PowerShell 5.1은 큰따옴표 앞의 역슬래시를 이스케이프로 넘겨 공백 있는 경로의 인자가 깨진다.
    $obj = (Join-Path $work ($label + '-obj')) + '/'
    # Out-Host: 함수 안의 표준 출력이 반환값에 섞이지 않게 한다.
    & dotnet build $project -c Release --nologo -v q "-p:GameDir=$gameDir" "-p:BaseIntermediateOutputPath=$obj" -o $out | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$label 빌드 실패: $gameDir" }

    $files = @(Get-ChildItem $out -File -Recurse | ForEach-Object { $_.Name })
    if ($files.Count -ne 1 -or $files[0] -ne 'AutoFingering.dll') {
        throw "$label 출력에는 AutoFingering.dll만 있어야 함: $($files -join ', ')"
    }

    return (Join-Path $out 'AutoFingering.dll')
}

function Get-IlText([string]$dll, [string]$label) {
    $path = Join-Path $work ($label + '.il')
    # ilspycmd는 설치된 런타임보다 낮은 대상으로 빌드되어 있어 상위 버전으로 올려 실행한다. 호출한 셸에는 남기지 않는다.
    $saved = $env:DOTNET_ROLL_FORWARD
    $env:DOTNET_ROLL_FORWARD = 'Major'
    try {
        & $IlSpy -il $dll | Out-File -FilePath $path -Encoding utf8
        if ($LASTEXITCODE -ne 0) { throw "ilspycmd 실패: $dll" }
    }
    finally {
        $env:DOTNET_ROLL_FORWARD = $saved
    }
    return [IO.File]::ReadAllLines($path)
}

New-Item -ItemType Directory -Path $work | Out-Null
try {
    $dllOld = Build-Against 'old' $GameOld
    $dllNew = Build-Against 'new' $GameNew

    $ilOld = Get-IlText $dllOld 'old'
    $ilNew = Get-IlText $dllNew 'new'
    # Compare-Object는 줄 순서를 보지 않고 기본이 대소문자 무시라 판정에는 쓰지 않고 차이를 보여 줄 때만 쓴다.
    if (($ilOld -join "`n") -cne ($ilNew -join "`n")) {
        Compare-Object $ilOld $ilNew -CaseSensitive | Select-Object -First 40 | Format-Table -AutoSize | Out-String | Write-Host
        throw '구버전과 신버전 빌드의 IL이 다름'
    }
    Write-Host "IL 비교: 구버전과 신버전 동일 ($($ilOld.Count)줄)"

    $info = Get-Content $infoPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $dist = Join-Path $root 'dist'
    New-Item -ItemType Directory -Force -Path $dist | Out-Null
    $zipPath = Join-Path $dist "AutoFingering-$($info.Version).zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    # Compress-Archive는 PowerShell 5.1에서 역슬래시 항목명을 만든다. UMM 설치기는 슬래시로 폴더를 찾는다.
    $zip = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $infoPath, 'AutoFingering/Info.json')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $dllOld, 'AutoFingering/AutoFingering.dll')
    }
    finally {
        $zip.Dispose()
    }

    Write-Host "작성: $zipPath"
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
