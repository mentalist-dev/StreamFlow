@ECHO OFF

set version=7.1.2
set output=./packages

dotnet restore ./sources

dotnet build ./sources /p:Version=%version% --configuration=Release --no-restore

dotnet pack ./sources/StreamFlow/StreamFlow.csproj -o %output% /p:Version=%version% --configuration=Release --no-restore --no-build
dotnet pack ./sources/StreamFlow.RabbitMq/StreamFlow.RabbitMq.csproj -o %output% /p:Version=%version% --configuration=Release --no-restore --no-build
dotnet pack ./sources/StreamFlow.RabbitMq.MediatR/StreamFlow.RabbitMq.MediatR.csproj -o %output% /p:Version=%version% --configuration=Release --no-restore --no-build
dotnet pack ./sources/StreamFlow.RabbitMq.Prometheus/StreamFlow.RabbitMq.Prometheus.csproj -o %output% /p:Version=%version% --configuration=Release --no-restore --no-build

