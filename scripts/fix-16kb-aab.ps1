# ============================================================
# fix-16kb-aab.ps1
#
# Play Store 16KB 메모리 페이지 검사 대응.
#
# 동작:
#   1) AAB 안의 문제되는 4KB 정렬 .so 파일을, 미리 추출해둔 16KB 정렬 버전으로 교체
#   2) 그 외 .so는 ELF p_align 헤더 패치
#   3) META-INF 서명 제거 후 jarsigner로 재서명
#   4) {input}-16kb.aab 으로 출력
#
# 사용법:
#   .\fix-16kb-aab.ps1
# ============================================================

param(
    [string]$InputAab = "",
    [string]$OutputAab = "",
    [string]$Keystore = "c:\Dev2026\cartcheck-release.keystore",
    [string]$Alias = "cartcheck"
)

$ErrorActionPreference = "Stop"

# ===== 기본 경로 =====
if ([string]::IsNullOrEmpty($InputAab)) {
    $InputAab = "c:\Dev2026\MartCart.AppFaithful\bin\Release\net9.0-android\publish\com.ssof.cartcheck-Signed.aab"
}
if ([string]::IsNullOrEmpty($OutputAab)) {
    $OutputAab = $InputAab -replace '\.aab$', '-16kb.aab'
}

if (-not (Test-Path $InputAab)) {
    Write-Error "AAB 파일을 찾을 수 없습니다: $InputAab"
}

# ===== 16KB 정렬된 교체용 .so 파일들 =====
$replacementMap = @{
    'base/lib/arm64-v8a/libimage_processing_util_jni.so' = 'c:\Dev2026\scripts\so16kb\libimage_processing_util_jni.arm64-v8a.so'
    'base/lib/x86_64/libimage_processing_util_jni.so'    = 'c:\Dev2026\scripts\so16kb\libimage_processing_util_jni.x86_64.so'
}

foreach ($v in $replacementMap.Values) {
    if (-not (Test-Path $v)) {
        Write-Error "교체용 .so 파일 없음: $v"
    }
}

# ===== jarsigner =====
$jdkBin = "C:\Program Files\Eclipse Adoptium\jdk-17.0.18.8-hotspot\bin"
$jarsigner = Join-Path $jdkBin "jarsigner.exe"
if (-not (Test-Path $jarsigner)) {
    $jarsigner = (Get-Command jarsigner -ErrorAction SilentlyContinue).Source
    if (-not $jarsigner) { Write-Error "jarsigner 못 찾음." }
}

# ===== 키스토어 암호 =====
if ([string]::IsNullOrEmpty($env:CARTCHECK_KEYSTORE_PASS)) {
    $sec = Read-Host "키스토어 암호 입력" -AsSecureString
    $env:CARTCHECK_KEYSTORE_PASS = [System.Net.NetworkCredential]::new('', $sec).Password
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

# ===== ELF p_align 패치 (교체 안 되는 다른 .so용) =====
function Patch-ElfAlignment([byte[]]$bytes) {
    if ($bytes.Length -lt 64) { return @($bytes, $false) }
    if ($bytes[0] -ne 0x7F -or $bytes[1] -ne 0x45 -or $bytes[2] -ne 0x4C -or $bytes[3] -ne 0x46) { return @($bytes, $false) }
    if ($bytes[4] -ne 2) { return @($bytes, $false) }

    $e_phoff = [BitConverter]::ToInt64($bytes, 0x20)
    $e_phentsize = [BitConverter]::ToUInt16($bytes, 0x36)
    $e_phnum = [BitConverter]::ToUInt16($bytes, 0x38)

    $changed = $false
    for ($i = 0; $i -lt $e_phnum; $i++) {
        $off = $e_phoff + $i * $e_phentsize
        if ($off + 0x38 -gt $bytes.Length) { break }
        $p_type = [BitConverter]::ToUInt32($bytes, $off)
        if ($p_type -eq 1) {
            $alignOff = $off + 0x30
            $p_align = [BitConverter]::ToInt64($bytes, $alignOff)
            if ($p_align -lt 0x4000) {
                $newAlign = [BitConverter]::GetBytes([Int64]0x4000)
                [Array]::Copy($newAlign, 0, $bytes, $alignOff, 8)
                $changed = $true
            }
        }
    }
    return @($bytes, $changed)
}

# ===== 처리 =====
$tempAab = Join-Path $env:TEMP "cartcheck-patched-$(Get-Random).aab"

Write-Host "AAB 열고 처리 중..."
$input = [System.IO.Compression.ZipFile]::OpenRead($InputAab)
try {
    if (Test-Path $tempAab) { Remove-Item $tempAab -Force }
    $output = [System.IO.Compression.ZipFile]::Open($tempAab, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $replaced = 0
        $patched = 0
        foreach ($entry in $input.Entries) {
            $name = $entry.FullName

            # 기존 서명 파일은 제외 (재서명할 거니까)
            if ($name -like 'META-INF/*') {
                $upper = $entry.Name.ToUpperInvariant()
                if ($upper -eq 'MANIFEST.MF' -or $upper -like '*.SF' -or $upper -like '*.RSA' -or $upper -like '*.DSA' -or $upper -like '*.EC') {
                    continue
                }
            }

            # 교체 대상이면 외부 파일에서 읽기
            if ($replacementMap.ContainsKey($name)) {
                $bytes = [System.IO.File]::ReadAllBytes($replacementMap[$name])
                Write-Host "  replaced: $name (16KB 정렬 버전으로 교체)"
                $replaced++
            }
            else {
                # 엔트리 바이트 읽기
                $entryStream = $entry.Open()
                try {
                    $ms = New-Object System.IO.MemoryStream
                    $entryStream.CopyTo($ms)
                    $bytes = $ms.ToArray()
                } finally {
                    $entryStream.Dispose()
                }

                # .so 파일이면 p_align 헤더 패치 시도
                if ($name -like '*.so') {
                    $result = Patch-ElfAlignment $bytes
                    $bytes = $result[0]
                    if ($result[1]) {
                        $patched++
                    }
                }
            }

            # 새 AAB에 쓰기
            $newEntry = $output.CreateEntry($name, [System.IO.Compression.CompressionLevel]::Optimal)
            $newEntry.LastWriteTime = $entry.LastWriteTime
            $newStream = $newEntry.Open()
            try {
                $newStream.Write($bytes, 0, $bytes.Length)
            } finally {
                $newStream.Dispose()
            }
        }
        Write-Host ""
        Write-Host "교체: $replaced 개"
        Write-Host "헤더 패치: $patched 개"
    } finally {
        $output.Dispose()
    }
} finally {
    $input.Dispose()
}

# ===== 재서명 =====
Write-Host ""
Write-Host "jarsigner로 재서명 중..."
Move-Item -Force $tempAab $OutputAab

& $jarsigner `
    -keystore $Keystore `
    -storepass $env:CARTCHECK_KEYSTORE_PASS `
    -keypass $env:CARTCHECK_KEYSTORE_PASS `
    -sigalg SHA256withRSA `
    -digestalg SHA-256 `
    $OutputAab $Alias

if ($LASTEXITCODE -ne 0) {
    Write-Error "jarsigner 실패 (exit code $LASTEXITCODE)"
}

Write-Host ""
Write-Host "✓ 완료"
Write-Host "출력: $OutputAab"
