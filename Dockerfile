# The MCP server, containerised so directories that verify servers can start one.
#
# This is not how anyone should install SignsOfAI. The supported route is
# `dnx SignsOfAI.Mcp`, which needs no container. This file exists because
# listing sites (Glama, and the awesome-mcp-servers listing that depends on it)
# run the server themselves and check that it answers an introspection request
# before they will list it. Being verifiable is the point.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the project files alone, so editing source does not invalidate
# the restore layer.
# README.md comes along because SignsOfAI.Core references the repository root
# README as a packaging asset; without it the item points at nothing.
COPY Directory.Build.props README.md ./
COPY src/SignsOfAI.Core/SignsOfAI.Core.csproj src/SignsOfAI.Core/
COPY src/SignsOfAI.Mcp/SignsOfAI.Mcp.csproj src/SignsOfAI.Mcp/
RUN dotnet restore src/SignsOfAI.Mcp/SignsOfAI.Mcp.csproj

COPY src/SignsOfAI.Core/ src/SignsOfAI.Core/
COPY src/SignsOfAI.Mcp/ src/SignsOfAI.Mcp/
RUN dotnet publish src/SignsOfAI.Mcp/SignsOfAI.Mcp.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app ./

# stdio transport: the client speaks JSON-RPC over stdin/stdout, so nothing is
# exposed on a port and there is no health endpoint to probe. Seven of the nine
# tools never open a socket at all.
ENTRYPOINT ["dotnet", "SignsOfAI.Mcp.dll"]
