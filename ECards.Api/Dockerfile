FROM dhi.io/aspnetcore:10-alpine3.22 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ECards.Api.csproj", "./"]
RUN dotnet restore "ECards.Api.csproj"
COPY . .
RUN dotnet build "ECards.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ECards.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false
RUN mkdir -p /app/publish/storage/custom /app/publish/storage/premade

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "ECards.Api.dll"]
