# SDK image builds every module referenced by Reflow.Api (see Reflow.slnx).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Reflow.slnx ./
COPY src/Reflow.Api/Reflow.Api.csproj src/Reflow.Api/
COPY src/Reflow.Infrastructure/Reflow.Infrastructure.csproj src/Reflow.Infrastructure/
COPY src/Modules/Reflow.Modules.Identity/Reflow.Modules.Identity.csproj src/Modules/Reflow.Modules.Identity/
COPY src/Modules/Reflow.Modules.Pipelines/Reflow.Modules.Pipelines.csproj src/Modules/Reflow.Modules.Pipelines/
COPY src/Modules/Reflow.Modules.DataProcessing/Reflow.Modules.DataProcessing.csproj src/Modules/Reflow.Modules.DataProcessing/
COPY src/Modules/Reflow.Modules.NodeTypes/Reflow.Modules.NodeTypes.csproj src/Modules/Reflow.Modules.NodeTypes/
COPY src/Modules/Reflow.Modules.PipelineExecution/Reflow.Modules.PipelineExecution.csproj src/Modules/Reflow.Modules.PipelineExecution/
COPY src/Modules/Reflow.Modules.Triggers/Reflow.Modules.Triggers.csproj src/Modules/Reflow.Modules.Triggers/
RUN dotnet restore src/Reflow.Api/Reflow.Api.csproj
COPY . .
RUN dotnet publish src/Reflow.Api/Reflow.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Reflow.Api.dll"]
