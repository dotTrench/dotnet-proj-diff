#!/bin/bash

set -e

dotnet pack
dotnet tool uninstall --global ProjectDiff.Tool
dotnet tool install --global --source src/ProjectDiff.Tool/bin/Release ProjectDiff.Tool --prerelease
