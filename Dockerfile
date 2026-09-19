FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the .NET projects the API needs. The solution also contains
# Clinic.Web (an .esproj Angular project) and Clinic.Tests; restoring at
# solution level pulls those in and fails, and the frontend is deployed
# separately to Cloudflare Pages anyway.
COPY Clinic.Domain/ Clinic.Domain/
COPY Clinic.Application/ Clinic.Application/
COPY Clinic.Infrastructure/ Clinic.Infrastructure/
COPY Clinic.Api/ Clinic.Api/

RUN dotnet restore "Clinic.Api/Clinic.Api.csproj"
RUN dotnet publish "Clinic.Api/Clinic.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["sh","-c","ASPNETCORE_URLS=http://0.0.0.0:$PORT dotnet Clinic.Api.dll"]
