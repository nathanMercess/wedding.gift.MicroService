# Use the standard Microsoft .NET Core container
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app
COPY ./publish /app

ENV DOTNET_RUNNING_IN_CONTAINER=true \
	# Enable correct mode for dotnet watch (only mode supported in a container)
	DOTNET_USE_POLLING_FILE_WATCHER=true \
	# Skip extraction of XML docs - generally not useful within an image/container - helps perfomance
    NUGET_XMLDOC_MODE=skip

# Expose port 33001 for the Web API traffic
EXPOSE 33001

# Run the dotnet application from within the container
ENTRYPOINT ["dotnet", "wedding.gift.Application.Webapi.dll"]

