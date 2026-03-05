FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["SensorApp/SensorApp.csproj", "SensorApp/"]
COPY ["SharedUtils/SharedUtils.csproj", "SharedUtils/"]
RUN dotnet restore "SensorApp/SensorApp.csproj"

COPY . .
WORKDIR "/src/SensorApp"
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SensorApp.dll"]