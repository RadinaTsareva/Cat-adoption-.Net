FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CatAdoption.Api/CatAdoption.Api.csproj CatAdoption.Api/
RUN dotnet restore CatAdoption.Api/CatAdoption.Api.csproj

COPY . .
WORKDIR /src/CatAdoption.Api
RUN dotnet publish CatAdoption.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CatAdoption.Api.dll"]
