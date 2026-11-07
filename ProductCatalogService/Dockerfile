FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ProductCatalogService/ProductCatalogService.csproj", "ProductCatalogService/"]
COPY ["SharedKernel/SharedKernel.csproj", "SharedKernel/"]
RUN dotnet restore "ProductCatalogService/ProductCatalogService.csproj"
COPY . .
WORKDIR "/src/ProductCatalogService"
RUN dotnet build "ProductCatalogService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ProductCatalogService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ProductCatalogService.dll"]
