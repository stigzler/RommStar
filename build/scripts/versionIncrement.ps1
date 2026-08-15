param (
    [string]$Config,
    [string]$VersionFile
)

# 1. ANTI-DOUBLE-INCREMENT LOCK (For WPF Two-Pass Compilation)
if (Test-Path $VersionFile) {
    $fileInfo = Get-Item $VersionFile
    $timeSinceLastWrite = (Get-Date) - $fileInfo.LastWriteTime
    
    # If the file was bumped less than 10 seconds ago, this is just MSBuild's second pass. 
    # Read the current version, output it to the compiler, and exit without incrementing!
    if ($timeSinceLastWrite.TotalSeconds -lt 10) {
        $currentVersion = Get-Content $VersionFile
        Write-Output $currentVersion
        exit
    }
}

# 2. NORMAL INCREMENT LOGIC
$currentVersion = Get-Content $VersionFile
$parts = $currentVersion.Split('.')
$major = [int]$parts[0]
$minor = [int]$parts[1]
$patch = [int]$parts[2]
$build = [int]$parts[3]

# Increment based on the Visual Studio Configuration
switch ($Config) {
    "Major"   { $major++; $minor=0; $patch=0; $build=0 }
    "Release" { $minor++; $patch=0; $build=0 }
    "Patch"   { $patch++; $build=0 }
    "Debug"   { $build++ }
}

$newVersion = "$major.$minor.$patch.$build"

# Save it back to the text file
Set-Content $VersionFile $newVersion

# Output it for MSBuild to catch
Write-Output $newVersion