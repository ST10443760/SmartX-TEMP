FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SmartX.Shared/SmartX.Shared.csproj SmartX.Shared/
COPY SmartX.Simulator/SmartX.Simulator.csproj SmartX.Simulator/
RUN dotnet restore SmartX.Simulator/SmartX.Simulator.csproj

COPY SmartX.Shared/ SmartX.Shared/
COPY SmartX.Simulator/ SmartX.Simulator/
RUN dotnet publish SmartX.Simulator/SmartX.Simulator.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "SmartX.Simulator.dll"]
