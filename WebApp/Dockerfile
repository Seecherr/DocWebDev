FROM mcr.microsoft.com/dotnet/aspnet:latest AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:latest AS build
WORKDIR /src


COPY ["WebApp/WebApp.csproj", "/"]
RUN dotnet restore -v:detailed "/WebApp.csproj"

WORKDIR "/src/WebApp"
COPY . .

RUN dotnet build "WebApp/WebApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebApp/WebApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebApp.dll"]
