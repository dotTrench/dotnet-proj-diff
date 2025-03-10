# dotnet-proj-diff

![NuGet Version](https://img.shields.io/nuget/v/ProjectDiff.Tool)
![GitHub License](https://img.shields.io/github/license/dotTrench/dotnet-proj-diff)


[Documentation](docs/)
```text
Description:
  Calculate which projects in a solution has changed since a specific commit

Usage:
  dotnet-proj-diff <solution> [options]

Arguments:
  <solution>  Path to solution file to derive projects from

Options:
  --base, --base-ref <base-ref> (REQUIRED)     Base git reference to compare against [default: HEAD]
  --head, --head-ref <head-ref>                Head git reference to compare against. If not specified current working tree will be used
  --merge-base                                 If true instead of using --base use the merge base of --base and --head as the --base reference, if --head is not specified 'HEAD' will be used [default: True]
  --include-deleted                            If true deleted projects will be included in output [default: False]
  --include-modified                           If true modified projects will be included in output [default: True]
  --include-added                              If true added projects will be included in output [default: True]
  --include-referencing                        if true  projects referencing modified/deleted/added projects will be included in output [default: True]
  --absolute-paths                             Output absolute paths, if not specified paths will be relative to the working directory. Or relative to --output if specified. This option will not affect slnf format as this requires relative paths [default: False]
  -f, --format <Json|Plain|Slnf|Traversal>     Output format, if --output is specified format will be derived from file extension. Otherwise this defaults to 'plain'
  -o, --out, --output <output>                 Output file, if not set stdout will be used
  --ignore-changed-file <ignore-changed-file>  Ignore changes in specific files. If these files are a part of the build evaluation process they will still be evaluated, however these files will be considered unchanged by the diff process []
  --version                                    Show version information
  -?, -h, --help                               Show help and usage information
```
