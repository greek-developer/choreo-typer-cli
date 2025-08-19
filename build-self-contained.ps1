if (Test-Path ./release) {
    Remove-Item ./release -Recurse -Force -Confirm:$false
}

$version = "0.1.0"
$informationalVersion = "0.1.0-pre-release"

$publishArgs = @(
    "publish", 
    "./src/ChoreoTyper/ChoreoTyper.csproj",
    "-c", "Release",
    "-f", "net8.0-windows",
    "-r", "win-x64",
    "-p:SelfContained=true",
    "-p:PublishSingleFile=true",
    "-p:Version=$version",
    "-p:FileVersion=$version",
    "-p:InformationalVersion=$informationalVersion",
    "-o", "./release/win-x64"
)

dotnet @publishArgs

Compress-Archive -Path "./release/win-x64/choreotyper.exe" -DestinationPath "./release/ChoreoTyper-win64.zip" -Force