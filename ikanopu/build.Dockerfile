FROM microsoft/dotnet:2.1-runtime AS base
WORKDIR /app

FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY ikanopu/ikanopu.csproj ikanopu/
RUN dotnet restore ikanopu/ikanopu.csproj
COPY . .
WORKDIR /src/ikanopu
RUN dotnet build ikanopu.csproj -c Release 

ENTRYPOINT ["dotnet" "publish" "ikanopu.csproj" "-c" "Release" "-r" "win-x64"]
