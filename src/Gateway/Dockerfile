# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
# Copy entire solution from compose build context (repo root)
COPY . .
# Restore and publish from gateway project folder so it can resolve referenced projects
WORKDIR /src/src/Gateway
RUN dotnet restore "ExchangeRate.Gateway.csproj"
RUN dotnet publish "ExchangeRate.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "ExchangeRate.Gateway.dll"]
