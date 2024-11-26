FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

ENV ORUK_API_Database__DatabaseName oruk-v3

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["OpenReferralApi/OpenReferralApi.csproj", "OpenReferralApi/"]
RUN dotnet restore "OpenReferralApi/OpenReferralApi.csproj"
COPY . .
WORKDIR "/src/OpenReferralApi"
RUN dotnet build "OpenReferralApi.csproj" -c Release -o /app/build 

FROM build AS publish
RUN dotnet publish "OpenReferralApi.csproj" -c Release -o /app/publish 
# -Database__DatabaseName=oruk-v3

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# CMD ASPNETCORE_URLS=http://*:8080 dotnet OpenReferralApi.dll
ENTRYPOINT ["dotnet", "OpenReferralApi.dll"]