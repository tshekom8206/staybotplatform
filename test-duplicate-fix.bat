@echo off
echo Testing Duplicate Task Fix for Hostr API
echo ========================================

echo.
echo 1. Getting authentication token...
curl -s -X POST "http://localhost:5000/api/auth/login" ^
  -H "Content-Type: application/json" ^
  -d "{\"email\": \"test@admin.com\", \"password\": \"Password123!\"}" > auth_response.json

for /f "tokens=2 delims=:" %%a in ('findstr "token" auth_response.json') do (
  set token_part=%%a
)
set TOKEN=%token_part:~2,-1%
echo Token obtained: %TOKEN:~0,20%...

echo.
echo 2. Getting task count before test...
curl -s -X GET "http://localhost:5000/api/staff/tasks" ^
  -H "Authorization: Bearer %TOKEN%" > tasks_before.json

for /f %%i in ('findstr /c:"\"id\":" tasks_before.json ^| find /c /v ""') do set TASKS_BEFORE=%%i
echo Tasks before: %TASKS_BEFORE%

echo.
echo 3. Sending hair dryer request...
curl -s -X POST "http://localhost:5000/api/message/route" ^
  -H "Content-Type: application/json" ^
  -H "Authorization: Bearer %TOKEN%" ^
  -d "{\"tenantId\": 1, \"phoneNumber\": \"+27123456789\", \"messageText\": \"I need a hair dryer\"}"

echo.
echo 4. Waiting 3 seconds for task processing...
timeout /t 3 /nobreak > nul

echo.
echo 5. Getting task count after test...
curl -s -X GET "http://localhost:5000/api/staff/tasks" ^
  -H "Authorization: Bearer %TOKEN%" > tasks_after.json

for /f %%i in ('findstr /c:"\"id\":" tasks_after.json ^| find /c /v ""') do set TASKS_AFTER=%%i
echo Tasks after: %TASKS_AFTER%

set /a NEW_TASKS=%TASKS_AFTER% - %TASKS_BEFORE%
echo New tasks created: %NEW_TASKS%

echo.
echo 6. Checking for duplicates in recent tasks...
echo Recent tasks:
findstr "title.*Hair Dryer\|title.*Guest Request" tasks_after.json

echo.
if %NEW_TASKS% EQU 1 (
  echo ✓ SUCCESS: Only 1 task created - duplicate bug is FIXED!
) else (
  echo ✗ FAILURE: %NEW_TASKS% tasks created - duplicate bug still exists
)

echo.
echo Cleaning up temporary files...
del auth_response.json tasks_before.json tasks_after.json 2>nul

echo.
echo Test completed!
pause