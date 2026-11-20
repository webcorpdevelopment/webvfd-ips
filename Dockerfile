#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
# Install curl for health checks and libSkiaSharp dependencies
RUN apt-get update && apt-get install -y curl postgresql-client \
    libfontconfig1 libfreetype6 libx11-6 libxcb1 libxext6 libxrender1 libc6-dev \
    fontconfig libfontconfig1-dev libgdiplus \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["InvoiceScanner.Cloud/InvoiceScanner.Cloud.csproj", "InvoiceScanner.Cloud/"]
RUN dotnet restore "./InvoiceScanner.Cloud/InvoiceScanner.Cloud.csproj"
COPY . .
WORKDIR "/src/InvoiceScanner.Cloud"
RUN dotnet build "./InvoiceScanner.Cloud.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./InvoiceScanner.Cloud.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Create necessary directories with proper permissions
RUN mkdir -p /app/Data /app/Documents /app/Invoices /app/Backups /app/Logs && \
    chmod -R 755 /app
    
# Set environment variable to handle SkiaSharp native libraries
ENV LD_LIBRARY_PATH=/usr/lib/x86_64-linux-gnu:$LD_LIBRARY_PATH
ENTRYPOINT ["dotnet", "InvoiceScanner.dll"]