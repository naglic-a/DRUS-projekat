FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ServerApp/ServerApp.csproj", "ServerApp/"]
COPY ["SharedUtils/SharedUtils.csproj", "SharedUtils/"]
RUN dotnet restore "ServerApp/ServerApp.csproj"

COPY . .
WORKDIR "/src/ServerApp"
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ServerApp.dll"]