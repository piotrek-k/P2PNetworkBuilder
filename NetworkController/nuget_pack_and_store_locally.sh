#! /bin/bash
# Exit on error
# set -e
# Verbose
set -x

# Clean
rm -i ./*.nupkg

# Create
dotnet build || exit 1
nuget pack || exit 1

# Move to local storage
file_name=`find -name "*.nupkg" -print -quit || exit 1`
mkdir -p ~/.nuget_local_packages || exit 1
nuget add ${file_name} -source ~/.nuget_local_packages