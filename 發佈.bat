@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul
cd /d "%~dp0"

echo ========================================
echo   Issue — 發佈後端 API 與前端靜態檔
echo ========================================
echo.

if not exist "backend\Issue.Api.csproj" (
  echo 找不到 backend\Issue.Api.csproj，請將本檔放在專案根目錄。
  goto eof_fail
)
if not exist "frontend\package.json" (
  echo 找不到 frontend\package.json，請將本檔放在專案根目錄。
  goto eof_fail
)
if not exist "backend\appsettings.json" (
  echo 找不到 backend\appsettings.json。
  goto eof_fail
)

where dotnet >nul 2>&1
if errorlevel 1 (
  echo 找不到 dotnet。請先安裝 .NET SDK。
  goto eof_fail
)
where npm >nul 2>&1
if errorlevel 1 (
  echo 找不到 npm。請先安裝 Node.js。
  goto eof_fail
)
where powershell >nul 2>&1
if errorlevel 1 (
  echo 找不到 powershell，無法讀取 appsettings.json。
  goto eof_fail
)

echo [1/5] 讀取 backend\appsettings.json 的 Publish:Root ...
for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "$j = Get-Content -LiteralPath '%~dp0backend\appsettings.json' -Raw -Encoding UTF8 | ConvertFrom-Json; if (-not $j.Publish -or [string]::IsNullOrWhiteSpace([string]$j.Publish.Root)) { Write-Error '缺少 Publish:Root'; exit 1 }; $root = [string]$j.Publish.Root.Trim(); if (-not [System.IO.Path]::IsPathRooted($root)) { $root = [System.IO.Path]::GetFullPath((Join-Path '%~dp0' $root)) } else { $root = [System.IO.Path]::GetFullPath($root) }; Write-Output $root"`) do set "PUBLISH_ROOT=%%i"
if errorlevel 1 goto eof_fail
if not defined PUBLISH_ROOT (
  echo 無法解析 Publish:Root。請在 backend\appsettings.json 設定 Publish.Root。
  goto eof_fail
)

set "API_OUT=!PUBLISH_ROOT!\api"
set "WEB_OUT=!PUBLISH_ROOT!\web"
echo   發佈根目錄：!PUBLISH_ROOT!
echo   API  → !API_OUT!
echo   Web  → !WEB_OUT!
echo.

echo [2/5] 發佈後端（dotnet publish）...
dotnet publish "backend\Issue.Api.csproj" -c Release -o "!API_OUT!"
if errorlevel 1 (
  echo 後端發佈失敗。
  goto eof_fail
)
echo.

echo [3/5] 建置前端（npm run build）...
if not exist "frontend\node_modules\" (
  echo 首次建置，正在 npm install ...
  pushd frontend
  call npm install
  if errorlevel 1 (
    popd
    echo 前端套件安裝失敗。
    goto eof_fail
  )
  popd
)
pushd frontend
call npm run build
if errorlevel 1 (
  popd
  echo 前端建置失敗。
  goto eof_fail
)
popd
echo.

echo [4/5] 複製 frontend\dist 並寫入 web\config.js ...
if exist "!WEB_OUT!\" (
  rmdir /s /q "!WEB_OUT!"
)
mkdir "!WEB_OUT!" 2>nul
xcopy /e /i /y "frontend\dist\*" "!WEB_OUT!\" >nul
if errorlevel 1 (
  echo 複製前端檔案失敗。
  goto eof_fail
)

set "API_BASE_URL="
for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "& { $webOut = $args[0]; $settingsPath = $args[1]; $j = Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json; $apiBase = 'http://localhost:5080'; if ($j.Publish -and -not [string]::IsNullOrWhiteSpace([string]$j.Publish.ApiBaseUrl)) { $apiBase = ([string]$j.Publish.ApiBaseUrl).Trim().TrimEnd('/') }; $text = 'window.__ISSUE_CONFIG__ = { apiBase: ' + (ConvertTo-Json $apiBase -Compress) + ' };' + [Environment]::NewLine; [System.IO.File]::WriteAllText((Join-Path $webOut 'config.js'), $text, (New-Object System.Text.UTF8Encoding $false)); Write-Output $apiBase }" "!WEB_OUT!" "%~dp0backend\appsettings.json"`) do set "API_BASE_URL=%%i"
if errorlevel 1 (
  echo 寫入 web\config.js 失敗。
  goto eof_fail
)
if not exist "!WEB_OUT!\config.js" (
  echo 找不到 web\config.js。
  goto eof_fail
)
if defined API_BASE_URL (
  echo   前端 API 位址：!API_BASE_URL!
) else (
  echo   前端 API 位址：http://localhost:5080
)
echo.

echo [5/5] 檢查並補齊資料庫初始資料（--ensure-db）...
if not exist "!API_OUT!\Issue.Api.exe" (
  echo 找不到 !API_OUT!\Issue.Api.exe。
  goto eof_fail
)
pushd "!API_OUT!"
"!API_OUT!\Issue.Api.exe" --ensure-db
if errorlevel 1 (
  popd
  echo 資料庫檢查／初始資料補齊失敗。請確認 api\appsettings.json 的 Database:Provider 與連線字串。
  goto eof_fail
)
popd

echo.
echo ========================================
echo 發佈完成
echo   API：!API_OUT!
echo   Web：!WEB_OUT!
echo.
echo 已執行資料庫初始資料檢查（議題大分類與預設小分類）。
echo 請確認 api\appsettings.json 的 Database:Provider 與連線字串。
echo IIS 請分別掛載 api（ASP.NET Core）與 web（靜態網站）。
echo 前端會讀取 web\config.js 的 apiBase；請與 IIS 上的 API 網址一致。
echo localhost / 127.0.0.1 的畫面來源已允許 CORS；其他主機請加在 Cors:Origins。
echo ========================================
goto eof_ok

:eof_fail
echo.
pause
endlocal
exit /b 1

:eof_ok
echo.
pause
endlocal
exit /b 0
