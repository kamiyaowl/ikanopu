FROM microsoft/dotnet:2.0-runtime AS base
WORKDIR /app

FROM microsoft/dotnet:2.0-sdk AS build
WORKDIR /src
COPY ../ikanopu-dotnet/ikanopu-dotnet.csproj ../ikanopu-dotnet/
RUN dotnet restore ../ikanopu-dotnet/ikanopu-dotnet.csproj
COPY . .
WORKDIR /src/../ikanopu-dotnet
RUN dotnet build ikanopu-dotnet.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish ikanopu-dotnet.csproj -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "ikanopu-dotnet.dll"]
