FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY web-crawler.Console/web-crawler.Console.csproj web-crawler.Console/
COPY web-crawler.Core/web-crawler.Core.csproj web-crawler.Core/

RUN dotnet restore web-crawler.Console/web-crawler.Console.csproj

COPY web-crawler.Console/ web-crawler.Console/
COPY web-crawler.Core/ web-crawler.Core/

RUN dotnet publish web-crawler.Console/web-crawler.Console.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "web-crawler.Console.dll"]