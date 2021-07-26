FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS build
WORKDIR /source

COPY . .
RUN dotnet restore
RUN dotnet publish -c release -o /srv --no-restore

FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine

WORKDIR /usr/local/src
RUN git clone --branch main https://github.com/SteeltoeOSS/NetCoreToolTemplates
RUN dotnet new --install NetCoreToolTemplates/src/Content

WORKDIR /srv
COPY --from=build /srv .
ENV DOTNET_URLS http://0.0.0.0:80
ENTRYPOINT ["dotnet", "Steeltoe.NetCoreToolService.dll"]
