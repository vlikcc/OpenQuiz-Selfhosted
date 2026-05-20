FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY OpenQuiz.slnx ./
COPY src/OpenQuiz.Domain/OpenQuiz.Domain.csproj src/OpenQuiz.Domain/
COPY src/OpenQuiz.Application/OpenQuiz.Application.csproj src/OpenQuiz.Application/
COPY src/OpenQuiz.Infrastructure/OpenQuiz.Infrastructure.csproj src/OpenQuiz.Infrastructure/
COPY src/OpenQuiz.Api/OpenQuiz.Api.csproj src/OpenQuiz.Api/
RUN dotnet restore OpenQuiz.slnx

COPY . .
RUN dotnet publish src/OpenQuiz.Api/OpenQuiz.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OpenQuiz.Api.dll"]
