@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul
cd /d "%~dp0"

set "BACKEND_PORT=5080"
set "FRONTEND_PORT=5173"
set "BACKEND_URL=http://localhost:5080"
set "FRONTEND_URL=http://localhost:5173"
set "WIN_BACKEND=Issue-Backend"
set "WIN_FRONTEND=Issue-Frontend"

if /i "%~1"=="start" goto do_start
if /i "%~1"=="stop" goto do_stop
if /i "%~1"=="restart" goto do_restart
if /i "%~1"=="status" goto do_status
if "%~1"=="1" goto do_start
if "%~1"=="2" goto do_stop
if "%~1"=="3" goto do_restart
if "%~1"=="4" goto do_status
if /i "%~1"=="help" goto do_help
if "%~1"=="/?" goto do_help
if not "%~1"=="" (
  echo 未知參數：%~1
  echo.
  goto do_help
)

:menu
cls
echo ========================================
echo   Issue 個人工作追蹤 — 網站控制
echo ========================================
echo.
call :print_status
echo.
echo   [1] 啟動網站
echo   [2] 停止網站
echo   [3] 重新啟動
echo   [4] 重新整理狀態
echo   [0] 離開
echo.
echo 亦可在命令列加上參數：start、stop、restart、status
echo ========================================
set "choice="
set /p "choice=請選擇： "
if "!choice!"=="1" goto do_start
if "!choice!"=="2" goto do_stop
if "!choice!"=="3" goto do_restart
if "!choice!"=="4" goto menu
if "!choice!"=="0" goto eof_ok
if /i "!choice!"=="q" goto eof_ok
echo.
echo 無效選項。
timeout /t 2 /nobreak >nul
goto menu

:do_help
echo 用法：%~nx0 參數
echo.
echo   不帶參數   開啟選單（滑鼠雙擊可用）
echo   start      啟動後端（埠 5080）與前端（埠 5173）
echo   stop       停止後端與前端
echo   restart    先停止再啟動
echo   status     顯示目前埠狀態
echo.
goto eof_maybe_pause

:do_start
echo.
echo [啟動] 檢查環境...
if not exist "backend\Issue.Api.csproj" (
  echo 找不到 backend\Issue.Api.csproj，請將本檔放在專案根目錄。
  goto eof_fail
)
if not exist "frontend\package.json" (
  echo 找不到 frontend\package.json，請將本檔放在專案根目錄。
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

call :port_listening %BACKEND_PORT%
set "BE_UP=!LISTENING!"
call :port_listening %FRONTEND_PORT%
set "FE_UP=!LISTENING!"

if "!BE_UP!"=="1" if "!FE_UP!"=="1" (
  echo 網站已在執行中。
  echo   後端：%BACKEND_URL%
  echo   前端：%FRONTEND_URL%
  goto eof_ok
)

call :db_hint

if "!BE_UP!"=="1" (
  echo 後端已在埠 %BACKEND_PORT% 執行，略過。
) else (
  echo 啟動後端：dotnet run --project backend --launch-profile http
  start "%WIN_BACKEND%" /D "%~dp0." cmd /k "dotnet run --project backend --launch-profile http"
)

if not exist "frontend\node_modules\" (
  echo 首次啟動，正在安裝前端套件（npm install）...
  pushd frontend
  call npm install
  if errorlevel 1 (
    popd
    echo 前端套件安裝失敗。
    goto eof_fail
  )
  popd
)

if "!FE_UP!"=="1" (
  echo 前端已在埠 %FRONTEND_PORT% 執行，略過。
) else (
  echo 啟動前端：npm start
  start "%WIN_FRONTEND%" /D "%~dp0frontend" cmd /k "npm start"
)

echo.
echo 等待服務就緒...
if "!BE_UP!"=="0" call :wait_port %BACKEND_PORT% 60 後端
if "!FE_UP!"=="0" call :wait_port %FRONTEND_PORT% 60 前端

call :port_listening %BACKEND_PORT%
set "BE_UP=!LISTENING!"
call :port_listening %FRONTEND_PORT%
set "FE_UP=!LISTENING!"

echo.
if "!BE_UP!"=="1" (echo 後端已就緒：%BACKEND_URL%) else (echo 後端尚未就緒，請查看「%WIN_BACKEND%」視窗訊息。)
if "!FE_UP!"=="1" (echo 前端已就緒：%FRONTEND_URL%) else (echo 前端尚未就緒，請查看「%WIN_FRONTEND%」視窗訊息。)

if "!FE_UP!"=="1" start "" "%FRONTEND_URL%"
echo.
echo 後端與前端會在獨立視窗持續執行；要停止請選 [2] 或執行：網站.bat stop
goto eof_ok

:do_stop
echo.
echo [停止] 結束網站行程...
taskkill /FI "WINDOWTITLE eq %WIN_BACKEND%*" /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq %WIN_FRONTEND%*" /T /F >nul 2>&1
call :kill_port %BACKEND_PORT%
call :kill_port %FRONTEND_PORT%
timeout /t 1 /nobreak >nul
echo.
call :print_status
goto eof_ok

:do_restart
echo.
echo [重新啟動]
call :do_stop_quiet
timeout /t 2 /nobreak >nul
goto do_start

:do_status
echo.
call :print_status
goto eof_ok

:do_stop_quiet
taskkill /FI "WINDOWTITLE eq %WIN_BACKEND%*" /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq %WIN_FRONTEND%*" /T /F >nul 2>&1
call :kill_port %BACKEND_PORT%
call :kill_port %FRONTEND_PORT%
goto :eof

:print_status
call :port_listening %BACKEND_PORT%
if "!LISTENING!"=="1" (echo   後端  %BACKEND_URL%    [執行中]) else (echo   後端  %BACKEND_URL%    [未啟動])
call :port_listening %FRONTEND_PORT%
if "!LISTENING!"=="1" (echo   前端  %FRONTEND_URL%    [執行中]) else (echo   前端  %FRONTEND_URL%    [未啟動])
goto :eof

:db_hint
set "DB_PROVIDER="
if exist "backend\appsettings.json" (
  for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "try { $j = Get-Content -LiteralPath '%~dp0backend\appsettings.json' -Raw -Encoding UTF8 | ConvertFrom-Json; Write-Output ([string]$j.Database.Provider).Trim() } catch { }"`) do set "DB_PROVIDER=%%i"
)
if /i "!DB_PROVIDER!"=="MySql" (
  call :port_listening 3306
  if "!LISTENING!"=="0" (
    echo 注意：Database:Provider=MySql，但未偵測到埠 3306。若 MySQL 未啟動，後端連線可能失敗。
    echo.
  )
) else if /i "!DB_PROVIDER!"=="SqlServer" (
  echo 提示：Database:Provider=SqlServer，請確認 SQL Server（如 .\SQLEXPRESS）已啟動。
  echo.
) else if defined DB_PROVIDER (
  echo 注意：Database:Provider=!DB_PROVIDER!（預期為 SqlServer 或 MySql）。
  echo.
) else (
  call :port_listening 3306
  if "!LISTENING!"=="0" (
    echo 注意：無法讀取 Database:Provider；亦未偵測到 MySQL 埠 3306。
    echo.
  )
)
goto :eof

:port_listening
set "LISTENING=0"
for /f "tokens=5" %%p in ('netstat -ano ^| findstr /C:":%~1" ^| findstr /C:"LISTENING"') do (
  set "LISTENING=1"
)
goto :eof

:kill_port
for /f "tokens=5" %%p in ('netstat -ano ^| findstr /C:":%~1" ^| findstr /C:"LISTENING"') do (
  if not "%%p"=="0" (
    echo   結束 PID %%p （埠 %~1）
    taskkill /F /T /PID %%p >nul 2>&1
  )
)
goto :eof

:wait_port
set "_port=%~1"
set "_max=%~2"
set "_name=%~3"
set /a _tries=0
:wait_port_loop
call :port_listening !_port!
if "!LISTENING!"=="1" (
  echo   !_name! 已在埠 !_port! 就緒。
  goto :eof
)
set /a _tries+=1
if !_tries! GEQ !_max! (
  echo   等待 !_name! 逾時（!_max! 秒）。
  goto :eof
)
timeout /t 1 /nobreak >nul
goto wait_port_loop

:eof_fail
echo.
if "%~1"=="" (
  pause
  goto menu
)
endlocal
exit /b 1

:eof_ok
echo.
if "%~1"=="" (
  pause
  goto menu
)
endlocal
exit /b 0

:eof_maybe_pause
if "%~1"=="" pause
endlocal
exit /b 0
