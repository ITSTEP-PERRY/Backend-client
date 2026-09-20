# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["AdminService/AdminService.Api/AdminService.Api.csproj", "AdminService/AdminService.Api/"]
COPY ["AdminService/AdminService.Application/AdminService.Application.csproj", "AdminService/AdminService.Application/"]
COPY ["AdminService/AdminService.Infrastructure/AdminService.Infrastructure.csproj", "AdminService/AdminService.Infrastructure/"]

RUN dotnet restore "AdminService/AdminService.Api/AdminService.Api.csproj"

COPY AdminService/ AdminService/

WORKDIR "/src/AdminService/AdminService.Api"

RUN dotnet publish "AdminService.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "AdminService.Api.dll"]