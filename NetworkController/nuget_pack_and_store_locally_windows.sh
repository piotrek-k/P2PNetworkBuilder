#! /bin/bash
# Exit on error
# set -e
# Verbose
set -x

# Clean
rm -i ./nugets/*.nupkg

# Create
# dotnet build || exit 1
# ./nuget.exe pack NetworkController.csproj || exit 1
mkdir -p ./nugets
dotnet pack --output ./nugets --include-source --interactive

# Move to local storage
file_name=`find ./nugets -maxdepth 1 -name "*.nupkg" -print -quit || exit 1`
mkdir -p /c/Users/piotr/source/nuget_packages || exit 1
./nuget.exe add ${file_name} -source /c/Users/piotr/source/nuget_packages