# Plugin Loader for SE on .NET 8.0

Here you can find Plugin Loader and Space Engineers Launcher ported to .NET 8.0.

## Usage

Clone the [se-dotnet-game](https://github.com/viktor-ferenczi/se-dotnet-game)
repository first and follow the instructions there to build the game.

Once it works, then you can add Plugin Loader by copying over the projects
from this repository into your local Git working copy folder which has the
decompiled game source in it. Overwrite the solution file or add the two
projects manually in your IDE.

Run the game by running the `SpaceEngineersLauncher` project.

Alternatively build and run it from the command line by executing the
`BuildAndRunWithPL.bat` script.

## Compatibility

Please note, that plugins using transpiler patches or verifying the bytecode would likely break.
Incompatible plugins could be made compatible with the .NET 8.0 build of the game, they just need some work.
The plugin authors may make them available at their discretion either as an alternate version or by using
runtime conditions on the IL code.

## References

- Original [Plugin Loader](https://github.com/sepluginloader/)
- [Space Engineers on .NET 8.0](https://github.com/viktor-ferenczi/se-dotnet-game)
