#! /bin/bash

# Clean
rm -i ./*.nupkg

# Create
nuget pack

# Move to local storage
file_name=`find -name "*.nupkg" -print -quit`
mkdir -p ~/.nuget_local_packages
nuget add ${file_name} -source ~/.nuget_local_packages