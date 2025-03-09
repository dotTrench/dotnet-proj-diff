# Formats

dotnet-proj-diff supports multiple output formats. The output format is determined by the output file extension, or by
the `--format option. The supported formats are:

### plain (.txt)

Path to the projects are outputted as plain text. This is the default format if no output file is specified. This is
useful for simple scripting scenarios.
e.g. ```dotnet-proj-diff <YOUR_SOLUTION_FILE> | dotnet build```

### slnf (.slnf)

This outputs the projects in a solution filter format. This allows you to use the output as any other solution file and
should probably be your go to format unless you have a specific reason to use any of the other formats.

### json (.json)

This is the most detailed format. It outputs a json array of objects, each representing a project. This also includes
the reason why the project was included in the diff. This is useful for more complex scenarios where you want to process
the output in a custom way.

### traversal (.proj)

This outputs the projects in
a [Microsoft.Build.Traversal](https://github.com/microsoft/MSBuildSdks/blob/main/src/Traversal/README.md) format.
