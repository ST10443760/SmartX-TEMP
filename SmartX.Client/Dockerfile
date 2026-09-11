FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SmartX.Shared/SmartX.Shared.csproj SmartX.Shared/
COPY SmartX.Client/SmartX.Client.csproj SmartX.Client/
RUN dotnet restore SmartX.Client/SmartX.Client.csproj

COPY SmartX.Shared/ SmartX.Shared/
COPY SmartX.Client/ SmartX.Client/
RUN dotnet publish SmartX.Client/SmartX.Client.csproj -c Release -o /app --no-restore

FROM nginx:alpine AS runtime
COPY --from=build /app/wwwroot /usr/share/nginx/html
COPY SmartX.Client/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
