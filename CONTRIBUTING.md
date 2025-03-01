# Contributing

This whole page is work-in-progress.

## Build

To ensure that we test the end user experience, `ManualTests` and `UseCaseTests` don't depend on any of the projects in the solution, but depend directly on the generated `.nupkg` files. This prevents building the solution with a simple `dotnet build`.

When you run `dotnet build` on the solution, `restore` is run on all projects first. Since we didn't build the nuget packages yet at this stage, restore fails for these tests project and consequentially, `dotnet build` fails.

You can use the following command to build the nuget packages first before restoring the tests. Running this once will allow you to use `dotnet build` going forward.


```sh
 dotnet build .\src\FlyingLogs.Analyzers\ && dotnet build .\src\FlyingLogs.Core\ && dotnet pack .\pack\FlyingLogs.Package\ ; dotnet clean .\test\FlyingLogs.ManualTests\ ; dotnet build .\test\FlyingLogs.ManualTests\
```