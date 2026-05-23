FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["RawPostingsFilter.Application/RawPostingsFilter.Application.csproj", "RawPostingsFilter.Application/"]
COPY ["RawPostingsFilter.Infrastructure/RawPostingsFilter.Infrastructure.csproj", "RawPostingsFilter.Infrastructure/"]
COPY ["RawPostingsFilter.Persistence/RawPostingsFilter.Persistence.csproj", "RawPostingsFilter.Persistence/"]
COPY ["RawPostingsFilter/RawPostingsFilter.csproj", "RawPostingsFilter/"]
RUN dotnet restore "RawPostingsFilter/RawPostingsFilter.csproj"
COPY . .
WORKDIR "/src/RawPostingsFilter"
RUN dotnet build "RawPostingsFilter.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "RawPostingsFilter.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RawPostingsFilter.dll"]
