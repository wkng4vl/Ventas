#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base

EXPOSE 80
EXPOSE 443


COPY . .

CMD ASPNETCORE_URLS=http://*:$PORT dotnet Sales.Web.dll
