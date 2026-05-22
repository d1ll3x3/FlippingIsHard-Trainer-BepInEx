# Setup script to find and copy BepInEx and Unity DLLs
# Works across different Steam library locations

Write-Host "Setting up BepInEx Trainer build environment..." -ForegroundColor Cyan
Write-Host ""

# Create lib folder
if (-not (Test-Path "lib")) {
    New-Item -ItemType Directory -Path "lib" | Out-Null
}

# Find game installation
$gamePath = $null
$searchPaths = @(
    "I:\SteamLibrary\steamapps\common\Flipping is Hard Demo",
    "C:\SteamLibrary\steamapps\common\Flipping is Hard Demo",
    "D:\SteamLibrary\steamapps\common\Flipping is Hard Demo",
    "E:\SteamLibrary\steamapps\common\Flipping is Hard Demo",
    "C:\Program Files\Steam\steamapps\common\Flipping is Hard Demo",
    "C:\Program Files (x86)\Steam\steamapps\common\Flipping is Hard Demo",
    "D:\Program Files\Steam\steamapps\common\Flipping is Hard Demo",
    "D:\Program Files (x86)\Steam\steamapps\common\Flipping is Hard Demo"
)

foreach ($path in $searchPaths) {
    if (Test-Path "$path\BepInEx") {
        $gamePath = $path
        break
    }
}

if (-not $gamePath) {
    Write-Host "ERROR: Could not find game installation" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please make sure:" -ForegroundColor Yellow
    Write-Host "  1. BepInEx is installed in your game folder"
    Write-Host "  2. The game is in a standard Steam library location"
    Write-Host ""
    exit 1
}

Write-Host "Found game at: $gamePath" -ForegroundColor Green
Write-Host ""

# Copy BepInEx DLL
Write-Host "Copying BepInEx DLLs..." -ForegroundColor Cyan
$bepinexDll = "$gamePath\BepInEx\core\BepInEx.dll"
if (Test-Path $bepinexDll) {
    Copy-Item $bepinexDll "lib\" -Force
    Write-Host "  OK: BepInEx.dll copied" -ForegroundColor Green
} else {
    Write-Host "  ERROR: BepInEx.dll not found at $bepinexDll" -ForegroundColor Red
}

# Copy Unity DLLs
Write-Host "Copying Unity DLLs..." -ForegroundColor Cyan

# Try BepInEx unity-libs folder first (IL2CPP)
$unityDll = "$gamePath\BepInEx\unity-libs\UnityEngine.dll"
if (Test-Path $unityDll) {
    Copy-Item $unityDll "lib\" -Force
    Write-Host "  OK: UnityEngine.dll copied" -ForegroundColor Green
} else {
    # Fallback to Managed folder (Mono)
    $unityDll = "$gamePath\Flipping is Hard Demo_Data\Managed\UnityEngine.dll"
    if (Test-Path $unityDll) {
        Copy-Item $unityDll "lib\" -Force
        Write-Host "  OK: UnityEngine.dll copied" -ForegroundColor Green
    } else {
        Write-Host "  ERROR: UnityEngine.dll not found" -ForegroundColor Red
    }
}

# Try BepInEx unity-libs folder first (IL2CPP)
$coreModuleDll = "$gamePath\BepInEx\unity-libs\UnityEngine.CoreModule.dll"
if (Test-Path $coreModuleDll) {
    Copy-Item $coreModuleDll "lib\" -Force
    Write-Host "  OK: UnityEngine.CoreModule.dll copied" -ForegroundColor Green
} else {
    # Fallback to Managed folder (Mono)
    $coreModuleDll = "$gamePath\Flipping is Hard Demo_Data\Managed\UnityEngine.CoreModule.dll"
    if (Test-Path $coreModuleDll) {
        Copy-Item $coreModuleDll "lib\" -Force
        Write-Host "  OK: UnityEngine.CoreModule.dll copied" -ForegroundColor Green
    } else {
        Write-Host "  ERROR: UnityEngine.CoreModule.dll not found" -ForegroundColor Red
    }
}

Write-Host ""
if ((Test-Path "lib\BepInEx.dll") -and (Test-Path "lib\UnityEngine.dll")) {
    Write-Host "Copying additional Unity modules..." -ForegroundColor Cyan
    
    $modules = @(
        "UnityEngine.InputModule.dll",
        "UnityEngine.InputLegacyModule.dll",
        "UnityEngine.TextRenderingModule.dll",
        "UnityEngine.IMGUIModule.dll",
        "UnityEngine.PhysicsModule.dll"
    )
    
    foreach ($module in $modules) {
        $modulePath = "$gamePath\BepInEx\unity-libs\$module"
        if (Test-Path $modulePath) {
            Copy-Item $modulePath "lib\" -Force
            Write-Host "  OK: $module copied" -ForegroundColor Green
        }
    }
    
    Write-Host ""
    Write-Host "Setup complete! You can now build the project." -ForegroundColor Green
} else {
    Write-Host "Setup incomplete. Check the errors above." -ForegroundColor Red
}
