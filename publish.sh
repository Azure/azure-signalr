ApiKey=$1
Source=$2

dotnet nuget push ./artifacts/build/*.nupkg -s $Source -k $ApiKey