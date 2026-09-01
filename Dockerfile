FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

COPY dotnet.core.utils/ dotnet.core.utils/
COPY dotnet.core.utils.server/ dotnet.core.utils.server/
COPY goldenfan-back-develop/dotnet.core.thegoldenfan/ goldenfan-back-develop/dotnet.core.thegoldenfan/
COPY goldenfan-back-develop/dotnet.core.thegoldenfan.controllers/ goldenfan-back-develop/dotnet.core.thegoldenfan.controllers/
COPY goldenfan-back-develop/dotnet.core.thegoldenfan.api/ goldenfan-back-develop/dotnet.core.thegoldenfan.api/

WORKDIR /src/goldenfan-back-develop/dotnet.core.thegoldenfan.api
RUN dotnet restore
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "dotnet.core.thegoldenfan.api.dll"]
