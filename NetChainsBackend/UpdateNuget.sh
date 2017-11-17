#!/bin/bash
rm *.nupkg
cat NCBackend.cs > NCBackend.bak
cat NCBackend.bak | sed 's/NetChainsBackend/NetChains/' > NCBackend.cs
nuget pack NetChainsBackend.csproj
cat NCBackend.bak > NCBackend.cs && rm NCBackend.bak
nuget push *.nupkg -Src http://www.nuget.org/api/v2/packages