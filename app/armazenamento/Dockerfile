FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Program.cs .
RUN dotnet new web --no-restore -o out && cp Program.cs out/Program.cs && dotnet publish out -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8001
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
HEALTHCHECK --interval=5s --timeout=3s --start-period=5s --retries=3 CMD curl -sf http://localhost:8001/health || exit 1
ENTRYPOINT ["dotnet", "out.dll"]
