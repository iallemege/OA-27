@echo off
setlocal EnableExtensions
set "GAME=d:\Steam\steamapps\common\Nuclear Option"
set MANAGED=%GAME%\NuclearOption_Data\Managed
set BEP=%GAME%\BepInEx\core
set PLUGINS=%GAME%\BepInEx\plugins
set OUT=%~dp0OA-27Variant.dll
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
  echo csc.exe not found
  exit /b 1
)
if not exist "%MANAGED%\Assembly-CSharp.dll" (
  echo Assembly-CSharp.dll not found
  exit /b 1
)

"%CSC%" /noconfig /nostdlib /nologo /optimize+ /target:library /platform:anycpu /langversion:5 ^
  /out:"%OUT%" ^
  /r:"%MANAGED%\mscorlib.dll" ^
  /r:"%MANAGED%\netstandard.dll" ^
  /r:"%MANAGED%\System.dll" ^
  /r:"%MANAGED%\System.Core.dll" ^
  /r:"%BEP%\BepInEx.dll" ^
  /r:"%BEP%\0Harmony.dll" ^
  /r:"%MANAGED%\Assembly-CSharp.dll" ^
  /r:"%MANAGED%\Mirage.dll" ^
  /r:"%MANAGED%\UnityEngine.CoreModule.dll" ^
  /r:"%MANAGED%\UnityEngine.dll" ^
  /r:"%MANAGED%\UnityEngine.PhysicsModule.dll" ^
  /r:"%MANAGED%\UnityEngine.ParticleSystemModule.dll" ^
  /r:"%MANAGED%\UnityEngine.IMGUIModule.dll" ^
  /r:"%MANAGED%\UnityEngine.TextRenderingModule.dll" ^
  /r:"%MANAGED%\UnityEngine.InputLegacyModule.dll" ^
  /r:"%MANAGED%\UnityEngine.UI.dll" ^
  "%~dp0src\Plugin.cs" ^
  "%~dp0src\Service.cs" ^
  "%~dp0src\Hangar.cs" ^
  "%~dp0src\FlightFix.cs" ^
  "%~dp0src\FactionJoin.cs" ^
  "%~dp0src\LoadoutLock.cs" ^
  "%~dp0src\Ab4Fx.cs" ^
  "%~dp0src\Gunpod.cs" ^
  "%~dp0src\RearLure.cs" ^
  "%~dp0src\OaTraits.cs" ^
  "%~dp0src\OaWso.cs" ^
  "%~dp0src\NobpDonor.cs" ^
  "%~dp0src\Inventory.cs" ^
  "%~dp0src\LoadScreen.cs"

if errorlevel 1 (
  echo OA-27Variant BUILD FAILED
  exit /b 1
)

echo Built: %OUT%
if exist "%PLUGINS%\OA27C.dll" del /f /q "%PLUGINS%\OA27C.dll" 2>nul
if exist "%PLUGINS%\OA-27Variant.dll" del /f /q "%PLUGINS%\OA-27Variant.dll" 2>nul
copy /Y "%OUT%" "%PLUGINS%\OA-27Variant.dll"
echo Installed: %PLUGINS%\OA-27Variant.dll
echo Standalone: no BIA / MiG-15S / Oritasy / Blueprinter. Optional donor Aryx.OA-27.Cavalier_*.nobp
endlocal
