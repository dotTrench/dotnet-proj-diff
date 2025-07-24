# dotnet-proj-diff

![NuGet Version](https://img.shields.io/nuget/v/dotnet-proj-diff?link=https%3A%2F%2Fwww.nuget.org%2Fpackages%2Fdotnet-proj-diff)
![GitHub License](https://img.shields.io/github/license/dotTrench/dotnet-proj-diff)

[Documentation](docs/)

dotnet-proj-diff identifies added, modified, and deleted projects between Git references, making it particularly useful
for CI/CD pipelines to determine which projects need to be rebuilt or tested.

Heavily inspired by [dotnet-affected](https://github.com/leonardochaia/dotnet-affected)

## Installation

```shell
# Install globally
dotnet tool install --global dotnet-proj-diff

# Or install locally
dotnet new tool-manifest # if you don't have one
dotnet tool install dotnet-proj-diff
```

## Usage

```text
Description:
  Calculate which projects in a solution has changed since a specific commit

Usage:
  dotnet-proj-diff [options]

Options:
  -?, -h, --help                                                     Show help and usage information
  --version                                                          Show version information
  --solution                                                         Path to solution file to derive projects from
  --base, --base-ref (REQUIRED)                                      Base git reference to compare against, if not specified 'HEAD' will be used [default: HEAD]
  --head, --head-ref                                                 Head git reference to compare against. If not specified current working tree will be used
  --merge-base                                                       If true instead of using --base use the merge base of --base and --head as the --base reference, if --head is not specified 'HEAD' will be used [default: True]
  --include-deleted                                                  If true deleted projects will be included in output [default: False]
  --include-modified                                                 If true modified projects will be included in output [default: True]
  --include-added                                                    If true added projects will be included in output [default: True]
  --include-referencing                                              if true  projects referencing modified/deleted/added projects will be included in output [default: True]
  --absolute-paths                                                   Output absolute paths, if not specified paths will be relative to the working directory. Or relative to --output if specified. This option will not affect slnf format as this requires relative paths [default: False]
  -f, --format <Json|Plain|Slnf|Traversal>                           Output format, if --output is specified format will be derived from file extension. Otherwise this defaults to 'plain'
  -o, --out, --output                                                Output file, if not set stdout will be used
  --ignore-changed-file                                              Ignore changes in specific files. If these files are a part of the build evaluation process they will still be evaluated, however these files will be considered unchanged by the diff process []
  --log-level <Critical|Debug|Error|Information|None|Trace|Warning>  Set the log level for the command [default: Information]
  --msbuild-traversal-version                                        Set the version of the Microsoft.Build.Traversal SDK when using traversal output format
```

The cli should have some sensible defaults, so you can run it without any arguments and get a list of projects that have
changed in the working tree and are available to build.

## Examples

```shell
# Get all projects in a solution that have changed since the last commit in 'main'
dotnet-proj-diff MySolution.sln --base main
# Get all projects that have changed in working tree, including deleted projects
dotnet-proj-diff --base HEAD --include-deleted
# Get all changed projects between two branches
dotnet-proj-diff --base main --head feature/new-feature

# Test all projects that have changed in working tree
dotnet-proj-diff --base HEAD | dotnet test

# Test all changed test projects in test/ directory
dotnet-proj-diff | grep 'test/' | dotnet test
```

## CI/CD Integration examples
[GitHub Actions](docs/github.md)
