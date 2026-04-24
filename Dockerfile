# Stage 1: Build .NET
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer caching
COPY ["src/MarketNest.Web/MarketNest.Web.csproj", "MarketNest.Web/"]
COPY ["src/MarketNest.Core/MarketNest.Core.csproj", "MarketNest.Core/"]
COPY ["src/MarketNest.Identity/MarketNest.Identity.csproj", "MarketNest.Identity/"]
COPY ["src/MarketNest.Catalog/MarketNest.Catalog.csproj", "MarketNest.Catalog/"]
COPY ["src/MarketNest.Cart/MarketNest.Cart.csproj", "MarketNest.Cart/"]
COPY ["src/MarketNest.Orders/MarketNest.Orders.csproj", "MarketNest.Orders/"]
COPY ["src/MarketNest.Payments/MarketNest.Payments.csproj", "MarketNest.Payments/"]
COPY ["src/MarketNest.Reviews/MarketNest.Reviews.csproj", "MarketNest.Reviews/"]
COPY ["src/MarketNest.Disputes/MarketNest.Disputes.csproj", "MarketNest.Disputes/"]
COPY ["src/MarketNest.Notifications/MarketNest.Notifications.csproj", "MarketNest.Notifications/"]
COPY ["src/MarketNest.Admin/MarketNest.Admin.csproj", "MarketNest.Admin/"]
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
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .
COPY --from=css-build /css/wwwroot/css/site.css ./wwwroot/css/site.css

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "MarketNest.Web.dll"]
