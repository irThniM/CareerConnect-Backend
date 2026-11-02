FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore CareerConnect.Api/CareerConnect.Api.csproj
RUN dotnet publish CareerConnect.Api/CareerConnect.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# CV OCR dependencies (Vietnamese + English) and SkiaSharp font support.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
        libfontconfig1 \
        tesseract-ocr \
        tesseract-ocr-eng \
        tesseract-ocr-vie \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000

ENTRYPOINT ["sh", "-c", "dotnet CareerConnect.Api.dll --urls http://0.0.0.0:${PORT:-10000}"]
