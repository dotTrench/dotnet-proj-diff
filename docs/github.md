# Github Actions
## Sample GitHub Actions Workflows

### Test Pull Request

This creates a workflow that runs on pull requests to the `main` and `dev` branches. It uses `dotnet-proj-diff` to determine which projects have changed, restores, builds, and tests only those projects.
dotnet-proj-diff generates a solution filter file (`.slnf`) that contains only the projects that have changed, which is then used for restoring, building, and testing.
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
        run: dotnet tool install --global dotnet-proj-diff

      - name: Run dotnet-proj-diff
        run: dotnet-proj-diff --base ${{ github.target_ref }} --output /tmp/diff.slnf

      - name: Restore changed projects
        run: dotnet restore /tmp/diff.slnf

      - name: Build changed projects
        run: dotnet build /tmp/diff.slnf --configuration Release --no-restore

      - name: Test changed projects
        run: dotnet test /tmp/diff.slnf --configuration Release --no-build --no-restore
```

### Publish projects on main branch

This workflow runs on pushes to the `main` branch. It uses `dotnet-proj-diff` to determine which projects have changed, restores, builds, and publishes those projects.
dotnet-proj-diff generates a solution filter file (`.slnf`) that contains only the projects that have changed, which is then used for restoring, building, and publishing.

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
          run: dotnet tool install --global dotnet-proj-diff

        # We need to figure out which commit to use for the diff. We'll hijack nx-set-shas to get the commit SHA of the last successful run of this workflow
        - name: Derive appropriate SHAs for base and head for `nx affected` commands
          uses: nrwl/nx-set-shas@v4

        - name: Run dotnet-proj-diff
          run: dotnet-proj-diff --base ${{ env.NX_BASE }} --output /tmp/diff.slnf

        - name: Restore changed projects
          run: dotnet restore /tmp/diff.slnf

        - name: Build changed projects
          run: dotnet build /tmp/diff.slnf --configuration Release --no-restore

        # Maybe you want to publish the projects as container images to be cool and hip
        - name: Publish changed projects
          run: dotnet publish /tmp/diff.slnf /t:PublishContainer --no-build --no-restore --configuration Release 
```

