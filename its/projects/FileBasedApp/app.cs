#!/usr/bin/env dotnet
#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:project Lib/Lib.csproj
#:package Newtonsoft.Json@13.0.4
#:include Included.cs

using FileBasedApp;
using FileBasedApp.Lib;
using Newtonsoft.Json; // unused on purpose: the package reference itself doesn't raise anything, this unused using does (S1128)

// FIXME raises S1134
Console.WriteLine(Greeter.Greeting());
Console.WriteLine(IncludedHelper.Message());
