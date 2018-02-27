@ECHO OFF

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
