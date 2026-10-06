FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ValinorInterApiProxy.Core/ValinorInterApiProxy.Core.csproj src/ValinorInterApiProxy.Core/
COPY src/ValinorInterApiProxy.Infrastructure/ValinorInterApiProxy.Infrastructure.csproj src/ValinorInterApiProxy.Infrastructure/
COPY src/ValinorInterApiProxy.Api/ValinorInterApiProxy.Api.csproj src/ValinorInterApiProxy.Api/
RUN dotnet restore src/ValinorInterApiProxy.Api/ValinorInterApiProxy.Api.csproj
COPY src/ src/
RUN dotnet publish src/ValinorInterApiProxy.Api/ValinorInterApiProxy.Api.csproj \
    -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ValinorInterApiProxy.Api.dll"]