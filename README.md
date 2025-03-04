# dotnet-proj-diff

git diff for .NET projects

```text
Usage:
  dotnet-proj-diff <solution> [<commit>] [options]

Arguments:
  <solution>  Path to solution file to derive projects from
  <commit>    Base git reference to compare against [default: HEAD]

Options:
  --merge-base                          If true instead of using <commit> use the merge base of <commit> and HEAD [default: True]
  --include-deleted                     If true deleted projects will be included in output [default: False]
  --include-modified                    If true modified projects will be included in output [default: True]
  --include-added                       If true added projects will be included in output [default: True]
  --include-referencing                 if true  projects referencing modified/deleted/added projects will be included in output [default: True]
  --absolute-paths                      Output absolute paths, if not specified paths will be relative to the working directory. Or relative to --output if specified. This option will not affect slnf format as this requires relative paths [default: False]
  --format <Json|Plain|Slnf|Traversal>  Output format, if --output is specified format will be derived from file extension. Otherwise this defaults to 'plain'
  --output <output>                     Output file, if not set stdout will be used
  --version                             Show version information
  -?, -h, --help                        Show help and usage information
```
