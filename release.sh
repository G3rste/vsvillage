#!/bin/bash
dotnet run --project ./CakeBuild/CakeBuild.csproj -- "$@" --skipDownloadLang false
releasefile=$(find Releases -name "*.zip")
echo $releasefile
version=$(echo $releasefile | grep -o '[0-9].*[0-9]')
echo $version
gh release create --generate-notes 'v'$version $releasefile 