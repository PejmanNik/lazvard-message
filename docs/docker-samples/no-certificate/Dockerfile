FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
COPY ../../src/*/*.csproj ./

RUN for file in $(ls *.csproj); do \
      mkdir -p src/${file%.*}/ && \
      mv $file src/${file%.*}/; \
    done
RUN ls
RUN dotnet restore ./src/Lazvard.Message.Cli/Lazvard.Message.Cli.csproj  --use-current-runtime

FROM build AS publish
COPY ../../src/ ./src
COPY ../../README.md .
RUN dotnet publish ./src/Lazvard.Message.Cli/Lazvard.Message.Cli.csproj \
    -c Release \
    --no-restore \
    -p:PublishSingleFile=true \
    --self-contained false \
    -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .

ENTRYPOINT ["./Lazvard.Message.Cli"]
