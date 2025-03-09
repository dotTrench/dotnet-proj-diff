# Github Actions

## Test Pull Request
```yaml
name: "Test Pull Request"

on:
  pull_request:
    branches:
      - main
      - dev
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout code
        uses: actions/checkout@v2
        with:
          fetch-depth: 0 # Need full history to be able to build project graph

      - name: "Setup .NET"
        uses: actions/setup-dotnet@v1
        with:
          dotnet-version: 9.x

      - name: Install dotnet-proj-diff
        run: dotnet tool install --global DotnetProjDiff.Tool

      - name: Run dotnet-proj-diff
        run: dotnet-proj-diff <YOUR_SOLUTION_FILE> ${{ github.target_ref }} --output /tmp/diff.slnf

      - name: Restore solution
        run: dotnet restore /tmp/diff.slnf

      - name: Build solution
        run: dotnet build /tmp/diff.slnf --configuration Release

      - name: Run tests
        run: dotnet test /tmp/diff.slnf --configuration Release --no-build --verbosity normal
```

### Publish projects on main branch
```yaml
name: "Publish Projects"
on:
  push:
    branches:
      - main
jobs:
    publish:
        runs-on: ubuntu-latest
        steps:
        - name: Checkout code
          uses: actions/checkout@v2
          with:
            fetch-depth: 0 # Need full history to be able to build project graph
        
        - name: "Setup .NET"
          uses: actions/setup-dotnet@v1
          with:
            dotnet-version: 9.x
        
        - name: Install dotnet-proj-diff
          run: dotnet tool install --global DotnetProjDiff.Tool

        # We need to figure out which commit to use for the diff. We'll hijack nx-set-shas to get the commit SHA of the last successful run of this workflow
        - name: Derive appropriate SHAs for base and head for `nx affected` commands
          uses: nrwl/nx-set-shas@v4

        - name: Run dotnet-proj-diff
          run: dotnet-proj-diff <YOUR_SOLUTION_FILE> ${{ env.NX_BASE }} --output /tmp/diff.slnf

        - name: Restore solution
          run: dotnet restore /tmp/diff.slnf

        - name: Build solution
          run: dotnet build /tmp/diff.slnf --configuration Release

        # Maybe you want to publish the projects as container images to be cool and hip
        - name: Publish projects
          run: dotnet publish /tmp/diff.slnf /t:PublishContainer
```

