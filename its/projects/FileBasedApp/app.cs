#!/usr/bin/env dotnet
#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:project Lib/Lib.csproj

using FileBasedApp.Lib;

// FIXME this is expected to raise S1134
Console.WriteLine(Greeter.Greeting());
