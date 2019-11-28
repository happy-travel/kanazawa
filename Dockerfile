FROM mcr.microsoft.com/dotnet/core/aspnet:3.0 AS base

ARG VAULT_TOKEN
ENV HTDC_VAULT_TOKEN=$VAULT_TOKEN
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS http://*:5000
EXPOSE 5000
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build
ARG Configuration=Release
WORKDIR /src
COPY . .
RUN dotnet build

FROM build AS publish
ARG Configuration=Release
WORKDIR /src
RUN dotnet publish --no-build --no-restore --no-dependencies -c $Configuration -o /app HappyTravel.Edo.PaymentProcessings

FROM base AS final
WORKDIR /app
COPY --from=publish /app .

HEALTHCHECK --interval=6s --timeout=10s --retries=3 CMD curl -sS 127.0.0.1:5000/health || exit 1

ENTRYPOINT ["dotnet", "HappyTravel.Edo.PaymentProcessings.dll"]