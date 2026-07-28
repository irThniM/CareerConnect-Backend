FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["CareerConnect.Api/CareerConnect.Api.csproj", "CareerConnect.Api/"]
RUN dotnet restore "CareerConnect.Api/CareerConnect.Api.csproj"

COPY . .

WORKDIR "/src/CareerConnect.Api"

RUN dotnet publish "CareerConnect.Api.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:10000

EXPOSE 10000

ENTRYPOINT ["dotnet", "CareerConnect.Api.dll"]