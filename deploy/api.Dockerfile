# OnlineShopping API — build from the repository root:
#   docker build -f deploy/api.Dockerfile -t onlineshop-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached until a project file or package version changes).
COPY Directory.Build.props Directory.Packages.props ./
COPY src/OnlineShopping.Domain/OnlineShopping.Domain.csproj src/OnlineShopping.Domain/
COPY src/OnlineShopping.Application/OnlineShopping.Application.csproj src/OnlineShopping.Application/
COPY src/OnlineShopping.Infrastructure/OnlineShopping.Infrastructure.csproj src/OnlineShopping.Infrastructure/
COPY src/OnlineShopping.Infrastructure.Migrations.PostgreSql/OnlineShopping.Infrastructure.Migrations.PostgreSql.csproj src/OnlineShopping.Infrastructure.Migrations.PostgreSql/
COPY src/OnlineShopping.Api/OnlineShopping.Api.csproj src/OnlineShopping.Api/
RUN dotnet restore src/OnlineShopping.Api/OnlineShopping.Api.csproj

COPY src/ src/
# The OpenAPI document is generated for the web app during development builds, not inside the image.
RUN dotnet publish src/OnlineShopping.Api/OnlineShopping.Api.csproj -c Release -o /app --no-restore \
    -p:OpenApiGenerateDocuments=false -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Runs as the image's built-in non-root user; listens on 8080 inside the container network only.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "OnlineShopping.Api.dll"]
