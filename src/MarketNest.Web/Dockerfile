# Stage 1: Build .NET
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy Central Package Management and build config first (needed before restore)
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY global.json .

# Copy all .csproj files to enable layer caching for restore
COPY ["src/MarketNest.Web/MarketNest.Web.csproj", "MarketNest.Web/"]
COPY ["src/MarketNest.Admin/MarketNest.Admin.csproj", "MarketNest.Admin/"]
COPY ["src/MarketNest.Analyzers/MarketNest.Analyzers.csproj", "MarketNest.Analyzers/"]
COPY ["src/MarketNest.Auditing/MarketNest.Auditing.csproj", "MarketNest.Auditing/"]
COPY ["src/MarketNest.Cart/MarketNest.Cart.csproj", "MarketNest.Cart/"]
COPY ["src/MarketNest.Catalog/MarketNest.Catalog.csproj", "MarketNest.Catalog/"]
COPY ["src/MarketNest.Disputes/MarketNest.Disputes.csproj", "MarketNest.Disputes/"]
COPY ["src/MarketNest.Identity/MarketNest.Identity.csproj", "MarketNest.Identity/"]
COPY ["src/MarketNest.Notifications/MarketNest.Notifications.csproj", "MarketNest.Notifications/"]
COPY ["src/MarketNest.Orders/MarketNest.Orders.csproj", "MarketNest.Orders/"]
COPY ["src/MarketNest.Payments/MarketNest.Payments.csproj", "MarketNest.Payments/"]
COPY ["src/MarketNest.Promotions/MarketNest.Promotions.csproj", "MarketNest.Promotions/"]
COPY ["src/MarketNest.Reviews/MarketNest.Reviews.csproj", "MarketNest.Reviews/"]
COPY ["src/Base/MarketNest.Base.Api/MarketNest.Base.Api.csproj", "Base/MarketNest.Base.Api/"]
COPY ["src/Base/MarketNest.Base.Common/MarketNest.Base.Common.csproj", "Base/MarketNest.Base.Common/"]
COPY ["src/Base/MarketNest.Base.Domain/MarketNest.Base.Domain.csproj", "Base/MarketNest.Base.Domain/"]
COPY ["src/Base/MarketNest.Base.Infrastructure/MarketNest.Base.Infrastructure.csproj", "Base/MarketNest.Base.Infrastructure/"]
COPY ["src/Base/MarketNest.Base.Utility/MarketNest.Base.Utility.csproj", "Base/MarketNest.Base.Utility/"]
RUN dotnet restore "MarketNest.Web/MarketNest.Web.csproj"

COPY src/ .
RUN dotnet publish "MarketNest.Web/MarketNest.Web.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 2: Tailwind CSS build
FROM node:22-alpine AS css-build
WORKDIR /css
COPY src/MarketNest.Web/package*.json ./
RUN npm ci
COPY src/MarketNest.Web/ .
RUN npx @tailwindcss/cli -i ./wwwroot/css/input.css -o ./wwwroot/css/site.css --minify

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN useradd -m -u 1001 appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .
COPY --from=css-build /css/wwwroot/css/site.css ./wwwroot/css/site.css

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "MarketNest.Web.dll"]
