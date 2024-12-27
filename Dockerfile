# syntax=docker/dockerfile:1

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env

WORKDIR /source

COPY DibariBot/*.csproj DibariBot/

ARG TARGETARCH

RUN dotnet restore DibariBot/ -a $TARGETARCH

COPY . .

RUN set -xe; \
dotnet publish -c Release -a $TARGETARCH -o /app; \
chmod +x /app/DibariBot

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime

WORKDIR /app

COPY --from=build-env /app .

VOLUME [ "/data" ]

ENV BOT_CONFIG_LOCATION=/data/botconfig.yaml

CMD [ "dotnet", "DibariBot.dll" ]
