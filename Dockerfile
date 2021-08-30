FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS build
WORKDIR /source

COPY . .
RUN dotnet restore
RUN dotnet publish -c release -o /srv --no-restore

FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine

RUN dotnet nuget add source https://pkgs.dev.azure.com/dotnet/Steeltoe/_packaging/dev/nuget/v3/index.json -n SteeltoeDev
RUN dotnet new --install Steeltoe.NetCoreTool.Templates::0.1.1

WORKDIR /srv
COPY --from=build /srv .
ENV DOTNET_URLS http://0.0.0.0:80
ENTRYPOINT ["dotnet", "Steeltoe.NetCoreToolService.dll"]
