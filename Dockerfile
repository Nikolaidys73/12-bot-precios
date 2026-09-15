FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

COPY . ./
RUN dotnet restore
RUN dotnet publish L2PriceBot.App/L2PriceBot.App.csproj -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

RUN apt-get update && apt-get install -y tzdata

COPY --from=build-env /app/out .

ENTRYPOINT ["dotnet", "L2PriceBot.App.dll"]
