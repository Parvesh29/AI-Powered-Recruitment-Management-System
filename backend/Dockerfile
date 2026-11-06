FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/global.json ./global.json
COPY backend/src/HireFlow.Domain/HireFlow.Domain.csproj backend/src/HireFlow.Domain/
COPY backend/src/HireFlow.Application/HireFlow.Application.csproj backend/src/HireFlow.Application/
COPY backend/src/HireFlow.Infrastructure/HireFlow.Infrastructure.csproj backend/src/HireFlow.Infrastructure/
COPY backend/src/HireFlow.API/HireFlow.API.csproj backend/src/HireFlow.API/
RUN dotnet restore backend/src/HireFlow.API/HireFlow.API.csproj
COPY backend/src/HireFlow.Domain/ backend/src/HireFlow.Domain/
COPY backend/src/HireFlow.Application/ backend/src/HireFlow.Application/
COPY backend/src/HireFlow.Infrastructure/ backend/src/HireFlow.Infrastructure/
COPY backend/src/HireFlow.API/ backend/src/HireFlow.API/
RUN dotnet publish backend/src/HireFlow.API/HireFlow.API.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "HireFlow.API.dll"]
