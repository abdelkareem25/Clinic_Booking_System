FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "Clinic.Api/Clinic.Api.csproj"
RUN dotnet publish "Clinic.Api/Clinic.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["sh","-c","ASPNETCORE_URLS=http://0.0.0.0:$PORT dotnet Clinic.Api.dll"]
