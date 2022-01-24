
#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app

ENV GSSettings:Guacd:Hostname=guacd
ENV GSSettings:Guacd:Port=4822

EXPOSE 80/tcp
EXPOSE 8080/tcp

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "GuacamoleSharp.API/GuacamoleSharp.API.csproj"
WORKDIR "/src/GuacamoleSharp.API"
RUN dotnet build "GuacamoleSharp.API.csproj" -c Release -o /app

FROM build AS publish
WORKDIR "/src/GuacamoleSharp.API"
RUN dotnet publish "GuacamoleSharp.API.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "GuacamoleSharp.API.dll"]