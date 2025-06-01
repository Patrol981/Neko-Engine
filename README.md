# What's Dwarf Engine?

Dwarf is a game engine targeting C-RPG and RTS genres because author is
passionate about theese :relaxed:, so it will work best with this type of games
but do feel free to create any other game with it!

# Features

- 3D/2D environment
- Native AOT compatible
- Customizable system pipeline
- Entity component system
- Modern graphics API using Vulkan

# Platform Support

| Platform | Is supported       |
| -------- | ------------------ |
| Windows  | :white_check_mark: |
| Linux    | :white_check_mark: |
| MacOS    | :x:                |
| Android  | :x:                |
| iOS      | :x:                |

Engine supports for now only Windows and Linux platform, MacOS with Metal support
will be there eventually but it's not the main priority

Android support is planned too along with iOS but not in the nearest future

# Scripting

For now the only language that is fully supported is C#, allthough I'm planning
to publish official bindings for both TypeScript and Python

# Building

There are/will be 3 diffrent ways to create games using the engine

## 1. Using as project dependency in your .csproj

It's fairly simple, just clone repo and add it to your client project and you're
good to go

## 2. Importing as .DLL

You can precompile engine, since it is a Library project it will output .DLL
files for you to use. Then in your .csproj you have to specify dlls that you'll
be using:

```xml
<ItemGroup>
	<Reference  Include="Dwarf">
		<HintPath>dlls\Dwarf.dll</HintPath>
	</Reference>
	<Reference  Include="Dwarf.AbstractionLayer">
		<HintPath>dlls\Dwarf.AbstractionLayer.dll</HintPath>
	</Reference>
</ItemGroup>
```

## 3. Using Dwarf Foundry (WIP)

You may got the feeling that creating project with Dwarf.dll can be a bit tricky
to get it right, hence there is an official launcher in development that will
improve your experience. When it will be ready You will find link to download
<b>here</b>
