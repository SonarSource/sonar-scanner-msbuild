#!/usr/bin/env dotnet
#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:project Lib/Lib.csproj
#:include Included.cs
#:package Newtonsoft.Json@13.0.4 // Does not raise S1128

using FileBasedApp.Lib;
using Newtonsoft.Json;

// FIXME raises S1134
Console.WriteLine(Greeter.Greeting());
Console.WriteLine(IncludedHelper.Message());
