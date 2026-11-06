FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["PqrSystem.Api/PqrSystem.Api.csproj", "PqrSystem.Api/"]
RUN dotnet restore "PqrSystem.Api/PqrSystem.Api.csproj"
COPY . .
WORKDIR "/src/PqrSystem.Api"
RUN dotnet publish "PqrSystem.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 10000
ENV ASPNETCORE_URLS=http://+:10000
ENTRYPOINT ["dotnet", "PqrSystem.Api.dll"]
