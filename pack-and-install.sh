#!/bin/bash

set -e

dotnet pack
dotnet tool uninstall --global dotnet-proj-diff
dotnet tool install --global --source src/dotnet-proj-diff/bin/Release dotnet-proj-diff --prerelease
