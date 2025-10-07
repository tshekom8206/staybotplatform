# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["Hostr.Workers.csproj", "./"]
RUN dotnet restore "Hostr.Workers.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "Hostr.Workers.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Hostr.Workers.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copy published files
COPY --from=publish /app/publish .

# Run the application
ENTRYPOINT ["dotnet", "Hostr.Workers.dll"]
