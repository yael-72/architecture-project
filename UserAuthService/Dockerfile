FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["UserAuthService/UserAuthService.csproj", "UserAuthService/"]
COPY ["SharedKernel/SharedKernel.csproj", "SharedKernel/"]
RUN dotnet restore "UserAuthService/UserAuthService.csproj"
COPY . .
WORKDIR "/src/UserAuthService"
RUN dotnet build "UserAuthService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "UserAuthService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UserAuthService.dll"]
