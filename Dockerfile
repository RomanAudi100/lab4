# build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /IsLabApp
COPY ["IsLabApp/IsLabApp.csproj", "IsLabApp/"]
RUN dotnet restore "IsLabApp/IsLabApp.csproj"
COPY . .
WORKDIR "/IsLabApp/IsLabApp"
RUN dotnet build "IsLabApp.csproj" -c Release -o /app/build
RUN dotnet publish "IsLabApp.csproj" -c Release -o /app/publish /p:UseAppHost=false


# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "IsLabApp.dll"]
