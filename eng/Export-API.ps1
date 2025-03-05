# Path to the root directory containing the src folder
$srcPath = Resolve-Path "$PSScriptRoot/../src"

# Enumerate all .csproj files under src/**/ directory, excluding any in the src/submodules folder
$csprojFiles = Get-ChildItem -Path $srcPath -Recurse -Filter "*.csproj" | Where-Object {
    $projectDir = $_.DirectoryName
    # Exclude files in the src/submodules folder
    -not ($projectDir -like "$srcPath\submodules\*")
}
if ($csprojFiles.Count -gt 0) {
    # Iterate over each found .csproj file
    foreach ($csproj in $csprojFiles) {
        # Log which project will be built
        Write-Host "Building project: $($csproj.FullName)"

        # Run dotnet build with the necessary parameters for each project
        dotnet build -v diag $csproj.FullName /p:GenerateApiListingOnBuild=true /p:Configuration=Release /restore
    } 
} else {
    Write-Host "No .csproj files found in the src directory."
}