#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/AdvancedUpdaterGitHubProxy.csproj", "."]
RUN dotnet restore "AdvancedUpdaterGitHubProxy.csproj"
COPY . .
WORKDIR "/src/src"
RUN dotnet build "AdvancedUpdaterGitHubProxy.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AdvancedUpdaterGitHubProxy.csproj" -c Release -o /app/publish
# Install dotnet debug tools
RUN dotnet tool install --tool-path /tools dotnet-trace \
 && dotnet tool install --tool-path /tools dotnet-counters \
 && dotnet tool install --tool-path /tools dotnet-dump \
 && dotnet tool install --tool-path /tools dotnet-gcdump

FROM base AS final
WORKDIR /tools
COPY --from=publish /tools .
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AdvancedUpdaterGitHubProxy.dll"]