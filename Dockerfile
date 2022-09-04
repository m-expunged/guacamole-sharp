#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app

EXPOSE 80/tcp
EXPOSE 8080/tcp

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "GuacamoleSharp.csproj"
WORKDIR "/src/GuacamoleSharp"
RUN dotnet build "GuacamoleSharp.csproj" -c Release -o /app

FROM build AS publish
WORKDIR "/src/GuacamoleSharp"
RUN dotnet publish "GuacamoleSharp.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "GuacamoleSharp.dll"]