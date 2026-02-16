FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Pr1.MinWebService.csproj ./
RUN dotnet restore Pr1.MinWebService.csproj

COPY . ./
RUN dotnet publish Pr1.MinWebService.csproj -c Release -o /app/publish --no-restore

FROM build AS test
WORKDIR /src/task1_tests
RUN dotnet restore Pr1.MinWebService.Tests.csproj
RUN dotnet test Pr1.MinWebService.Tests.csproj --no-restore --verbosity normal

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Pr1.MinWebService.dll"]
