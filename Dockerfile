FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    NUGET_XMLDOC_MODE=skip \
    ASPNETCORE_URLS=http://+:8080

# Cloud Run exige 8080, mas você pode mapear para 33001 localmente
EXPOSE 8080

ENTRYPOINT ["dotnet", "wedding.gift.Application.Webapi.dll"]
