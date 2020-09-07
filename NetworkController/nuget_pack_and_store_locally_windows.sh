#! /bin/bash
# Exit on error
# set -e
# Verbose
set -x

# Clean
rm -i ./*.nupkg

# Create
dotnet build || exit 1
./nuget.exe pack NetworkController.csproj || exit 1

# Move to local storage
file_name=`find -name "*.nupkg" -print -quit || exit 1`
mkdir -p /c/Users/piotr/source/nuget_packages || exit 1
./nuget.exe add ${file_name} -source /c/Users/piotr/source/nuget_packages