FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /App
COPY ./src ./src
COPY ./test ./test
COPY ./README.md ./README.md
COPY ./Lazvard.Message.sln ./Lazvard.Message.sln
RUN dotnet restore
RUN dotnet publish -c Release -o out
RUN mkdir -p out/config

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /App
COPY --from=build-env /App/out .
ENTRYPOINT ["dotnet", "Lazvard.Message.Cli.dll", "-s", "-c",  "./config/config.toml"]