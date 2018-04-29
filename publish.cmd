@ECHO OFF

IF [%~1]==[] (
    GOTO :USAGE
    EXIT /b 1
)
IF [%~2]==[] (
    GOTO :USAGE
    EXIT /b 1
)

SETLOCAL
SET BuildNumber=%~1
SET ApiKey=%~2
SET NugetServerUrl=https://www.myget.org/F/azure-signalr-dev/api/v2/package

PUSHD %~dp0

CALL ./build.cmd /p:BuildNumber=%BuildNumber%

CD ./artifacts/build
CALL dotnet nuget push *.nupkg -k %ApiKey% -s %NugetServerUrl%

POPD
ENDLOCAL
GOTO :EOF

:USAGE
ECHO Build, pack and publish packages in current repo to MyGet/NuGet.
ECHO Two positional parameters are required:
ECHO    1. BuildNumber: Build number of the packages to be published.
ECHO    2. ApiKey: API Key to access MyGet/NuGet feed.
ECHO Usage:
ECHO    publish.cmd 12345 xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
EXIT /b 0

:EOF
