# GGs 3.0 Enterprise Deployment Guide

## 🚀 Production Deployment

This guide covers deploying GGs 3.0 to production environments with enterprise-grade security, scalability, and monitoring.

## Prerequisites

- **Azure Subscription** (or equivalent cloud provider)
- **Domain with SSL certificate**
- **Redis instance** (Azure Cache for Redis recommended)
- **SQL Server** (Azure SQL Database recommended)
- **Azure Key Vault** for secret management
- **Application Insights** for monitoring

## Environment Configuration

### 1. Azure Key Vault Setup

Create secrets in Azure Key Vault:

```bash
# JWT signing key (256-bit)
az keyvault secret set --vault-name "your-keyvault" --name "auth-jwt-key-current" --value "your-secure-256-bit-key"

# License signing keys (RSA 2048-bit)
az keyvault secret set --vault-name "your-keyvault" --name "license-private-key-current" --value "base64-encoded-private-key"
az keyvault secret set --vault-name "your-keyvault" --name "license-public-key-current" --value "base64-encoded-public-key"

# Database connection string
az keyvault secret set --vault-name "your-keyvault" --name "ConnectionStrings--SqlServer" --value "Server=your-server;Database=GGs;..."

# Redis connection string
az keyvault secret set --vault-name "your-keyvault" --name "ConnectionStrings--Redis" --value "your-redis-connection-string"
```

### 2. Application Configuration

**appsettings.Production.json:**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "SqlServer": "", // Will be loaded from Key Vault
    "Redis": ""      // Will be loaded from Key Vault
  },
  "Azure": {
    "KeyVault": {
      "VaultUrl": "https://your-keyvault.vault.azure.net/"
    },
    "ApplicationInsights": {
      "ConnectionString": "InstrumentationKey=your-key;IngestionEndpoint=..."
    }
  },
  "Auth": {
    "JwtKey": "", // Will be loaded from Key Vault
    "JwtIssuer": "https://your-domain.com",
    "JwtAudience": "https://your-domain.com",
    "JwtExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 30,
    "OIDC": {
      "Enabled": true,
      "Authority": "https://your-sso-provider.com",
      "ClientId": "your-client-id",
      "ClientSecret": "", // Store in Key Vault
      "ResponseType": "code",
      "Scope": "openid profile email"
    }
  },
  "License": {
    "PublicKeyPem": "",  // Will be loaded from Key Vault
    "Issuer": "GGs Enterprise",
    "DefaultExpiryDays": 365
  },
  "Server": {
    "AllowedOrigins": "https://your-domain.com,https://app.your-domain.com",
    "Kestrel": {
      "MaxConcurrentConnections": 2048,
      "MaxRequestBodySizeBytes": 5000000,
      "KeepAliveTimeoutSeconds": 120,
      "RequestHeadersTimeoutSeconds": 30
    }
  },
  "Security": {
    "ClientCertificate": {
      "Enabled": true,
      "RequiredPaths": ["/api/audit", "/api/ingest"]
    },
    "KeyRotation": {
      "IntervalDays": 90,
      "AutoRotateEnabled": true
    }
  },
  "Saml": {
    "EntityId": "https://your-domain.com",
    "CertificatePath": "/app/certificates/saml.pfx",
    "CertificatePassword": "", // Store in Key Vault
    "IdpSsoUrl": "https://your-idp.com/sso",
    "PostLogoutRedirectUrl": "https://your-domain.com"
  },
  "RateLimiting": {
    "GlobalPerMinute": 6000,
    "PerUserPerMinute": 100,
    "EliBotPerUserPerDay": {
      "Basic": 5,
      "Pro": 25,
      "Enterprise": 100,
      "Moderator": 200,
      "Admin": 99999,
      "Owner": 99999
    }
  },
  "Otel": {
    "ServerEnabled": true,
    "ServiceName": "GGs.Server.Production"
  }
}
```

## Deployment Options

### Option 1: Azure App Service

**Azure App Service Configuration:**

```bash
# Create App Service Plan
az appservice plan create --name ggs-plan --resource-group ggs-rg --sku P2V2 --is-linux

# Create Web App
az webapp create --name ggs-api --plan ggs-plan --resource-group ggs-rg --runtime "DOTNETCORE|8.0"

# Configure managed identity
az webapp identity assign --name ggs-api --resource-group ggs-rg

# Grant Key Vault access
az keyvault set-policy --name your-keyvault --object-id $(az webapp identity show --name ggs-api --resource-group ggs-rg --query principalId -o tsv) --secret-permissions get list

# Deploy application
az webapp deployment source config-zip --name ggs-api --resource-group ggs-rg --src ggs-deployment.zip
```

### Option 2: Azure Container Instances

**Dockerfile:**

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["server/GGs.Server/GGs.Server.csproj", "server/GGs.Server/"]
COPY ["shared/GGs.Shared/GGs.Shared.csproj", "shared/GGs.Shared/"]
RUN dotnet restore "server/GGs.Server/GGs.Server.csproj"
COPY . .
WORKDIR "/src/server/GGs.Server"
RUN dotnet build "GGs.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GGs.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r ggs && useradd -r -g ggs ggs
USER ggs

ENTRYPOINT ["dotnet", "GGs.Server.dll"]
```

**Deploy to ACI:**

```bash
# Build and push image
docker build -t ggs-server:latest .
docker tag ggs-server:latest your-registry.azurecr.io/ggs-server:latest
docker push your-registry.azurecr.io/ggs-server:latest

# Create container instance
az container create \
  --resource-group ggs-rg \
  --name ggs-api \
  --image your-registry.azurecr.io/ggs-server:latest \
  --cpu 2 \
  --memory 4 \
  --ports 80 443 \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT=Production \
    Azure__KeyVault__VaultUrl=https://your-keyvault.vault.azure.net/ \
  --assign-identity \
  --restart-policy Always
```

### Option 3: Azure Kubernetes Service (AKS)

**deployment.yaml:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ggs-api
  labels:
    app: ggs-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: ggs-api
  template:
    metadata:
      labels:
        app: ggs-api
    spec:
      containers:
      - name: ggs-api
        image: your-registry.azurecr.io/ggs-server:latest
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: Azure__KeyVault__VaultUrl
          value: "https://your-keyvault.vault.azure.net/"
        resources:
          requests:
            memory: "1Gi"
            cpu: "500m"
          limits:
            memory: "2Gi"
            cpu: "1000m"
        livenessProbe:
          httpGet:
            path: /health/live
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 10
---
apiVersion: v1
kind: Service
metadata:
  name: ggs-api-service
spec:
  selector:
    app: ggs-api
  ports:
  - protocol: TCP
    port: 80
    targetPort: 80
  type: LoadBalancer
```

## Database Setup

### SQL Server Migration

```bash
# Install Entity Framework tools
dotnet tool install --global dotnet-ef

# Update database schema
dotnet ef database update --connection "your-production-connection-string"

# Create initial admin user
dotnet run --environment Production -- --seed-admin
```

### Redis Configuration

**Azure Cache for Redis settings:**
- SKU: Premium P1 (6GB, 20,000 connections)
- Enable persistence for durability
- Configure firewall rules
- Enable authentication

## SSL/TLS Configuration

### Azure App Service
- Enable HTTPS Only
- Configure custom domain
- Use App Service Managed Certificate or upload custom certificate

### Load Balancer/Application Gateway
```json
{
  "sslPolicy": "AppGwSslPolicy20220101S",
  "minProtocolVersion": "TLSv1_2",
  "cipherSuites": [
    "TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384",
    "TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256"
  ]
}
```

## Monitoring & Observability

### Application Insights Configuration

Key metrics to monitor:
- Request rate and response times
- Dependency call durations (DB, Redis)
- Exception rates and types
- Authentication success/failure rates
- Cache hit/miss ratios
- Custom business metrics (tweak executions, license validations)

### Log Analytics Queries

**Authentication failures:**
```kusto
requests
| where timestamp > ago(1h)
| where url contains "auth"
| where resultCode startswith "4"
| summarize count() by bin(timestamp, 5m), resultCode
```

**Performance bottlenecks:**
```kusto
dependencies
| where timestamp > ago(1h)
| where type in ("SQL", "Redis")
| summarize avg(duration), max(duration), count() by bin(timestamp, 5m), target
```

## Security Hardening

### Network Security
1. **Virtual Network**: Deploy in VNet with private endpoints
2. **Network Security Groups**: Restrict inbound traffic to HTTPS only
3. **Web Application Firewall**: Enable on Application Gateway
4. **DDoS Protection**: Enable Azure DDoS Protection Standard

### Application Security
1. **HTTPS Enforcement**: Redirect all HTTP to HTTPS
2. **HSTS Headers**: Enforce HTTPS in browsers
3. **Content Security Policy**: Prevent XSS attacks
4. **Rate Limiting**: Implement per-user and global limits
5. **Input Validation**: Use FluentValidation for all inputs
6. **SQL Injection Protection**: Use parameterized queries (EF Core)

### Identity Security
1. **JWT Rotation**: Implement automatic key rotation
2. **Multi-Factor Authentication**: Enforce for admin accounts
3. **Session Management**: Secure token storage and transmission
4. **Password Policies**: Enforce strong password requirements

## Backup & Disaster Recovery

### Database Backup
```bash
# Automated daily backups (Azure SQL Database)
az sql db backup-policy set \
  --resource-group ggs-rg \
  --server ggs-sql-server \
  --database ggs-db \
  --retention-days 35 \
  --weekly-retention 12 \
  --monthly-retention 60 \
  --yearly-retention 10
```

### Redis Backup
- Enable RDB persistence
- Configure backup to Azure Storage
- Test restore procedures regularly

### Application Backup
- Store configuration in Azure DevOps/GitHub
- Implement Infrastructure as Code (ARM/Bicep templates)
- Document restoration procedures

## Performance Optimization

### Database Optimization
1. **Connection Pooling**: Configure optimal pool sizes
2. **Indexing**: Create indexes on frequently queried columns
3. **Query Optimization**: Use LINQ efficiently
4. **Read Replicas**: For read-heavy workloads

### Redis Optimization
1. **Connection Multiplexing**: Single connection per application
2. **Key Expiration**: Set appropriate TTL values
3. **Memory Optimization**: Use appropriate data structures
4. **Cluster Mode**: For high-throughput scenarios

### Application Optimization
1. **Output Caching**: Cache frequently requested data
2. **Compression**: Enable response compression
3. **CDN**: Use Azure Front Door for static assets
4. **Async Operations**: Use async/await properly

## Scaling Strategy

### Horizontal Scaling
- **App Service**: Scale out to multiple instances
- **AKS**: Use Horizontal Pod Autoscaler
- **Database**: Use read replicas for read operations
- **Redis**: Use clustering for high-throughput

### Vertical Scaling
- Monitor CPU, memory, and I/O metrics
- Scale up during peak usage periods
- Use autoscaling rules based on metrics

## Health Checks & Monitoring

### Health Check Endpoints
- `/health/live`: Basic application liveness
- `/health/ready`: Dependency readiness (DB, Redis)
- `/health/detail`: Detailed health information (admin only)

### Alerting Rules
1. **High Error Rate**: > 5% error rate for 5 minutes
2. **High Latency**: > 2 seconds average response time
3. **Database Issues**: Connection failures or timeouts
4. **Redis Issues**: Connection failures or high memory usage
5. **Certificate Expiry**: SSL certificates expiring in 30 days

## Troubleshooting

### Common Issues

**Database Connection Timeouts:**
```bash
# Check connection string
# Verify firewall rules
# Monitor connection pool usage
```

**Redis Connection Issues:**
```bash
# Verify Redis connection string
# Check Redis memory usage
# Monitor connection count
```

**High Memory Usage:**
```bash
# Check for memory leaks
# Monitor garbage collection
# Review cache usage patterns
```

### Log Analysis
Use structured logging to troubleshoot issues:
```csharp
_logger.LogError("Database connection failed: {ConnectionString} {Exception}", 
    connectionString, exception);
```

## Security Checklist

- [ ] HTTPS enforced with HSTS
- [ ] JWT keys stored in Key Vault
- [ ] Database credentials in Key Vault
- [ ] Input validation implemented
- [ ] Rate limiting configured
- [ ] Audit logging enabled
- [ ] Error handling implemented
- [ ] Security headers configured
- [ ] Certificate-based authentication enabled
- [ ] Multi-factor authentication enforced
- [ ] Network security groups configured
- [ ] Web Application Firewall enabled
- [ ] DDoS protection enabled
- [ ] Backup procedures tested
- [ ] Monitoring and alerting configured
- [ ] Incident response plan documented

## Support & Maintenance

### Regular Maintenance Tasks
1. **Weekly**: Review logs and metrics
2. **Monthly**: Security updates and patches
3. **Quarterly**: Performance reviews and optimizations
4. **Annually**: Security audits and penetration testing

### Contact Information
- **Technical Support**: support@ggs.local
- **Security Issues**: security@ggs.local
- **Emergency Contact**: +1-555-GGS-HELP

---

**Note**: This deployment guide assumes Azure cloud services. Adapt configurations for other cloud providers (AWS, GCP) or on-premises deployments as needed.
