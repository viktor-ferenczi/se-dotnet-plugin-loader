@set SolutionDir=%cd%
dotnet run --configuration Release --project SpaceEngineersLauncher -- -skipintro >build.log 2>&1
