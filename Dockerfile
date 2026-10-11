# syntax=docker/dockerfile:1
# Targets:
#   runtime  (default) -> the API image          docker build -t promix-api .
#   migrator           -> EF Core migration bundle  docker build --target migrator -t promix-migrator .

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so this layer is cached unless project files change
COPY NuGet.Config PrimeInnovation.sln ./
COPY PrimeInnovation.Api/PrimeInnovation.Api.csproj PrimeInnovation.Api/
COPY src/PrimeInnovation.Domain/PrimeInnovation.Domain.csproj src/PrimeInnovation.Domain/
COPY src/PrimeInnovation.Application/PrimeInnovation.Application.csproj src/PrimeInnovation.Application/
COPY src/PrimeInnovation.Infrastructure/PrimeInnovation.Infrastructure.csproj src/PrimeInnovation.Infrastructure/
RUN dotnet restore PrimeInnovation.sln --configfile NuGet.Config

COPY . .
RUN dotnet publish PrimeInnovation.Api/PrimeInnovation.Api.csproj -c Release -o /app/publish --no-restore

# ---------- migration bundle ----------
FROM build AS bundle
RUN dotnet tool install --global dotnet-ef --version 9.0.4
ENV PATH="${PATH}:/root/.dotnet/tools" \
    DOTNET_ROLL_FORWARD=Major
# EF uses AppDbContextFactory (design-time), so Program.cs never runs here and no app secrets are needed.
RUN dotnet ef migrations bundle \
      --project src/PrimeInnovation.Infrastructure \
      --startup-project PrimeInnovation.Api \
      --configuration Release \
      --output /out/efbundle \
      --force

# ---------- migrator image ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /migrator
COPY --from=bundle /out/efbundle ./efbundle
USER app
# Needs only ConnectionStrings__Default (read by AppDbContextFactory). Applies pending migrations and exits.
ENTRYPOINT ["./efbundle"]

# ---------- API runtime (default target: keep last) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Uploads are mounted from a persistent volume; the directory must be writable by the non-root user
RUN mkdir -p /app/wwwroot/uploads && chown -R app:app /app/wwwroot
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PrimeInnovation.Api.dll"]
