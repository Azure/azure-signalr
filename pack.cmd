@ECHO OFF

SETLOCAL
IF NOT [%~1]==[] SET BuildNumber=%~1

PUSHD %~dp0

RD /S /Q .publish
MKDIR .publish

CD src/
FOR /f %%p IN ('DIR . /AD /B') do (
    PUSHD %%p
    IF EXIST *.csproj (dotnet pack -c Release -o ..\..\.publish)
    POPD
)

POPD

ENDLOCAL
