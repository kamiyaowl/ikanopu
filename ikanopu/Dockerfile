FROM microsoft/dotnet:2.1-runtime AS base
WORKDIR /app

FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY ikanopu/ikanopu.csproj ikanopu/
RUN dotnet restore ikanopu/ikanopu.csproj
COPY . .
WORKDIR /src/ikanopu
RUN dotnet build ikanopu.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish ikanopu.csproj -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "ikanopu.dll"]
