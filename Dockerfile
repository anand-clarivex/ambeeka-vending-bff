FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
RUN addgroup --system app && adduser --system --ingroup app app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Pervaxis.Vpa.Vending/Pervaxis.Vpa.Vending.csproj", "src/Pervaxis.Vpa.Vending/"]
RUN dotnet restore "src/Pervaxis.Vpa.Vending/Pervaxis.Vpa.Vending.csproj"
COPY . .
WORKDIR "/src/src/Pervaxis.Vpa.Vending"
RUN dotnet build "Pervaxis.Vpa.Vending.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "Pervaxis.Vpa.Vending.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER app
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 CMD curl --fail http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Pervaxis.Vpa.Vending.dll"]
