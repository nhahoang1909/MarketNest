# MarketNest — DevOps Requirements

> Version: 0.1 (Planning) | Status: Draft | Date: 2026-04

---

## 1. Phase Overview

| Phase | Period | Infrastructure | Goal |
|-------|--------|---------------|------|
| Phase 1 | Month 1–3 | Docker Compose + GitHub Actions | Ship something deployable |
| Phase 2 | Month 4–5 | + Nginx SSL + Observability | Production-grade monolith |
| Phase 3 | Month 6–7 | + YARP Gateway + RabbitMQ | Distributed systems basics |
| Phase 4 | Month 8–9 | K8s (kind → AKS/EKS) + ArgoCD | Operate a real cluster |

---

## 2. Phase 1: Docker Compose Setup

### docker-compose.yml (Development)
```yaml
version: '3.9'
services:
  app:
    build: .
    ports: ["5000:8080"]
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__Default=Host=postgres;Database=marketnest;Username=mn;Password=mn_secret
      - Redis__ConnectionString=redis:6379
    depends_on:
      postgres: { condition: service_healthy }
      redis:    { condition: service_started }
    volumes:
      - ./src:/app/src  # hot reload in dev

  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: marketnest
      POSTGRES_USER: mn
      POSTGRES_PASSWORD: mn_secret
    volumes:
      - pg_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD", "pg_isready", "-U", "mn"]
      interval: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    command: redis-server --appendonly yes
    volumes:
      - redis_data:/data

  rabbitmq:
    image: rabbitmq:3-management-alpine
    ports: ["15672:15672"]  # Management UI (dev only)
    environment:
      RABBITMQ_DEFAULT_USER: mn
      RABBITMQ_DEFAULT_PASS: mn_secret

  mailhog:
    image: mailhog/mailhog
    ports: ["8025:8025"]  # Web UI to view emails in dev

  seq:
    image: datalust/seq:latest
    ports: ["5341:80"]
    environment:
      ACCEPT_EULA: Y
    volumes:
      - seq_data:/data

volumes:
  pg_data:
  redis_data:
  seq_data:
```

### docker-compose.prod.yml (Production overrides)
```yaml
# Extends docker-compose.yml; strips dev tools, adds prod settings
services:
  app:
    image: ghcr.io/youruser/marketnest:${TAG}
    restart: unless-stopped
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      # Secrets injected via .env file or secret manager
    deploy:
      resources:
        limits: { memory: 512M }

  nginx:
    image: nginx:alpine
    ports: ["80:80", "443:443"]
    volumes:
      - ./infra/nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./infra/nginx/certs:/etc/nginx/certs:ro
      - ./infra/nginx/conf.d:/etc/nginx/conf.d:ro
    depends_on: [app]

  # Remove mailhog, expose rabbitmq only internally
```

---

## 3. Phase 1: CI/CD — GitHub Actions

### ci.yml (on every push)
```yaml
name: CI

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    
    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_DB: marketnest_test
          POSTGRES_USER: mn
          POSTGRES_PASSWORD: mn_secret
        options: >-
          --health-cmd pg_isready
          --health-interval 5s
          --health-retries 5
      redis:
        image: redis:7
        options: >-
          --health-cmd "redis-cli ping"
          --health-interval 5s

    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'
          
      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
          
      - name: Restore dependencies
        run: dotnet restore
        
      - name: Build
        run: dotnet build --no-restore --configuration Release
        
      - name: Unit Tests
        run: dotnet test tests/MarketNest.UnitTests --no-build --configuration Release
        
      - name: Integration Tests
        run: dotnet test tests/MarketNest.IntegrationTests --no-build --configuration Release
        env:
          ConnectionStrings__Default: "Host=localhost;Database=marketnest_test;Username=mn;Password=mn_secret"
          Redis__ConnectionString: "localhost:6379"
          
      - name: Architecture Tests
        run: dotnet test tests/MarketNest.ArchitectureTests --no-build --configuration Release

  docker-build:
    needs: build-and-test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Build Docker image
        run: docker build -t marketnest:${{ github.sha }} .
        
      - name: Push to GHCR
        run: |
          echo ${{ secrets.GITHUB_TOKEN }} | docker login ghcr.io -u ${{ github.actor }} --password-stdin
          docker tag marketnest:${{ github.sha }} ghcr.io/${{ github.repository }}:${{ github.sha }}
          docker push ghcr.io/${{ github.repository }}:${{ github.sha }}
```

### deploy.yml (on tag push)
```yaml
name: Deploy

on:
  push:
    tags: ['v*']

jobs:
  deploy:
    runs-on: ubuntu-latest
    environment: production
    steps:
      - name: Deploy via SSH
        uses: appleboy/ssh-action@v1
        with:
          host: ${{ secrets.PROD_HOST }}
          username: ${{ secrets.PROD_USER }}
          key: ${{ secrets.PROD_SSH_KEY }}
          script: |
            cd /opt/marketnest
            export TAG=${{ github.ref_name }}
            docker compose -f docker-compose.yml -f docker-compose.prod.yml pull
            docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --remove-orphans
            docker image prune -f
```

---

## 4. Phase 2: Nginx + SSL (Production-Ready)

### nginx.conf
```nginx
# /infra/nginx/nginx.conf
worker_processes auto;

events { worker_connections 1024; }

http {
    # Security headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains; preload" always;
    add_header X-Frame-Options DENY always;
    add_header X-Content-Type-Options nosniff always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;
    add_header Content-Security-Policy "default-src 'self'; script-src 'self' 'nonce-{nonce}' cdn.jsdelivr.net; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:;" always;

    # Gzip
    gzip on;
    gzip_types text/plain text/css application/json text/javascript;
    
    # Rate limiting (coarse Nginx-level, fine-grained in .NET)
    limit_req_zone $binary_remote_addr zone=api:10m rate=30r/s;
    limit_req_zone $binary_remote_addr zone=auth:10m rate=5r/m;

    server {
        listen 80;
        server_name marketnest.example.com;
        return 301 https://$host$request_uri;
    }

    server {
        listen 443 ssl http2;
        server_name marketnest.example.com;

        ssl_certificate     /etc/nginx/certs/fullchain.pem;
        ssl_certificate_key /etc/nginx/certs/privkey.pem;
        ssl_protocols       TLSv1.3;
        ssl_prefer_server_ciphers off;

        location / {
            limit_req zone=api burst=20 nodelay;
            proxy_pass http://app:8080;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }

        location /auth/ {
            limit_req zone=auth burst=3;
            proxy_pass http://app:8080;
        }
    }
}
```

---

## 5. Phase 2: Observability

### OpenTelemetry Setup (.NET)
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("MarketNest.*")
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddRedisInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri("http://seq:5341/ingest/otlp/v1/traces")))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());
```

### Serilog Configuration
```csharp
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "MarketNest")
    .Enrich.WithCorrelationId()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .WriteTo.Seq("http://seq:5341")
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .CreateLogger();
```

### Key Metrics to Track
- Request rate per endpoint
- Order state transition rates
- Cart reservation hit/miss rate
- Payment success/failure rate
- Background job execution time
- DB query duration (P95, P99)
- Redis hit rate

### k6 Load Testing (Phase 2)
```javascript
// tests/k6/smoke-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '2m', target: 10 },
    { duration: '5m', target: 10 },
    { duration: '2m', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
  },
};

export default function () {
  const res = http.get('https://marketnest.example.com/');
  check(res, { 'status is 200': (r) => r.status === 200 });
  sleep(1);
}
```

---

## 6. Phase 3: API Gateway with YARP

### YARP Configuration
```csharp
// appsettings.json (Phase 3)
{
  "ReverseProxy": {
    "Routes": {
      "core-route": {
        "ClusterId": "core-cluster",
        "Match": { "Path": "{**catch-all}" },
        "Transforms": [{ "RequestHeader": "X-Forwarded-Proto", "Set": "https" }]
      },
      "notification-route": {
        "ClusterId": "notification-cluster",
        "Match": { "Path": "/api/notifications/{**catch-all}" }
      }
    },
    "Clusters": {
      "core-cluster": {
        "LoadBalancingPolicy": "RoundRobin",
        "Destinations": {
          "core1": { "Address": "http://core-service:8080/" }
        },
        "HealthCheck": {
          "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health" }
        }
      },
      "notification-cluster": {
        "Destinations": {
          "notif1": { "Address": "http://notification-service:8080/" }
        }
      }
    }
  }
}
```

### RabbitMQ Topology (Phase 3)
```
Exchanges:
  marketnest.events   (topic, durable)

Bindings:
  order.placed        → notification-service queue
  order.shipped       → notification-service queue
  dispute.opened      → notification-service queue + admin-service queue
  payout.processed    → notification-service queue

Queues (quorum, durable):
  notification-service.emails
  notification-service.deadletter
```

---

## 7. Phase 4: Kubernetes

### Directory Structure
```
infra/
├── k8s/
│   ├── base/
│   │   ├── namespace.yaml
│   │   ├── core-service/
│   │   │   ├── deployment.yaml
│   │   │   ├── service.yaml
│   │   │   ├── hpa.yaml
│   │   │   └── configmap.yaml
│   │   ├── notification-service/
│   │   ├── postgres/
│   │   │   └── statefulset.yaml
│   │   ├── redis/
│   │   └── rabbitmq/
│   ├── overlays/
│   │   ├── staging/
│   │   │   └── kustomization.yaml
│   │   └── production/
│   │       └── kustomization.yaml
│   └── ingress/
│       └── nginx-ingress.yaml
└── helm/
    └── marketnest/  ← Helm chart (alternative to kustomize)
```

### Core Service HPA
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: core-service-hpa
  namespace: marketnest-prod
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: core-service
  minReplicas: 2
  maxReplicas: 10
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
    - type: Resource
      resource:
        name: memory
        target:
          type: Utilization
          averageUtilization: 80
```

### ArgoCD Application
```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: marketnest
  namespace: argocd
spec:
  project: default
  source:
    repoURL: https://github.com/youruser/marketnest
    targetRevision: HEAD
    path: infra/k8s/overlays/production
  destination:
    server: https://kubernetes.default.svc
    namespace: marketnest-prod
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
    syncOptions:
      - CreateNamespace=true
```

---

## 8. Dockerfile (Multi-Stage)

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/MarketNest.Web/MarketNest.Web.csproj", "MarketNest.Web/"]
COPY ["src/MarketNest.Core/MarketNest.Core.csproj", "MarketNest.Core/"]
# ... all projects ...
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
RUN npx tailwindcss -i ./wwwroot/css/input.css -o ./wwwroot/css/site.css --minify

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
```

---

## 9. Secrets Management

| Phase | Method |
|-------|--------|
| Development | `dotnet user-secrets` (never committed) |
| Phase 1 Prod | `.env` file on server (gitignored), SSH-only server access |
| Phase 2+ Prod | Azure Key Vault / HashiCorp Vault (read at startup) |
| K8s Phase 4 | K8s Secrets + External Secrets Operator → Vault |

### Never Commit
```gitignore
# .gitignore
*.env
.env.*
appsettings.Production.json
appsettings.Secrets.json
**/user-secrets/
```

---

## 10. Database Migration Strategy

```
EF Core Migrations (applied at startup in Phase 1-2):

Program.cs:
  if (app.Environment.IsDevelopment() || args.Contains("--migrate"))
  {
      using var scope = app.Services.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<MarketNestDbContext>();
      await db.Database.MigrateAsync();
  }

Phase 4 (K8s): 
  - Init container runs migrations before app container starts
  - Ensures zero-downtime deployments
  
Migration naming convention:
  YYYYMMDD_HHmm_DescriptiveTitle
  Example: 20260410_1430_AddOrderDispute
```

---

## 11. Branching Strategy

```
main          ← Production; protected; requires PR + CI green
develop       ← Integration branch; merges to main via PR on release
feature/*     ← Feature branches from develop
fix/*         ← Hotfix branches
release/v*    ← Release preparation (from develop)
```

**PR Requirements:**
- All CI checks pass (build + tests)
- At least 1 reviewer (Phase 3+; solo project: self-review via checklist)
- No merge without green architecture tests
