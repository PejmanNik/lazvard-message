# Variables
$RepoUrl = "https://github.com/dotnet/aspnetcore.git"
$PathInRepo = "src/Shared/CertificateGeneration/"

# Create a temporary directory
$TempDir = New-Item -ItemType Directory -Force -Path "$env:TEMP\$(Get-Random)"

# Clone the repository to the temporary directory
git clone $RepoUrl $TempDir

# Copy files from the specified path to the current directory
$SourcePath = Join-Path -Path $TempDir -ChildPath $PathInRepo
Copy-Item -Path "$SourcePath\*" -Destination "." -Recurse

# Cleanup: Delete the temporary directory
Remove-Item -Path $TempDir -Recurse -Force