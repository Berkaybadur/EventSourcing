FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/TaskBoard.Domain/*.csproj src/TaskBoard.Domain/
COPY src/TaskBoard.Api/*.csproj src/TaskBoard.Api/
RUN dotnet restore src/TaskBoard.Api
COPY src src
RUN dotnet publish src/TaskBoard.Api -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TaskBoard.Api.dll"]
