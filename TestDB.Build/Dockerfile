FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
# Copy root certificate used by nuget.org into trusted store, to support dotnet restore command below
COPY zscaler.crt /usr/local/share/ca-certificates/
RUN update-ca-certificates
WORKDIR "/src"
COPY ["./", "TestDB.Build/"]
WORKDIR "/src/TestDB.Build"
RUN dotnet build "TestDB.Build.csproj" -c Release -o /app/build

# This file creates a container running SQL2019, with a ChatTest database populated from this project's compiled DACPAC
FROM mcr.microsoft.com/mssql/server:2019-latest

# Switch to root user to install updates and Unzip:
USER root
RUN apt-get update && apt-get install unzip -y

# Install SQLPackage for Linux and make it executable:
RUN wget -progress=bar:force -q -O sqlpackage.zip https://go.microsoft.com/fwlink/?linkid=2157202 \
    && unzip -qq sqlpackage.zip -d /opt/sqlpackage \
    && chmod +x /opt/sqlpackage/sqlpackage

# Switch back to mssql user so mssql service does not execute as root:
USER mssql

# Add compiled DACPAC from this project to the image:
COPY --from=build /app/build/TestDB.Build.dacpac /tmp/

# Configure external build arguments to allow configurability:
ARG DBNAME=ChatTest
ARG PASSWORD=P@ssword12

# Configure the required environmental variables:
ENV ACCEPT_EULA=Y
ENV DBNAME=$DBNAME
ENV SA_PASSWORD=$PASSWORD

# Launch SQL server process, wait for it to be running, then use SQLPackage to apply the DACPAC:
RUN ( /opt/mssql/bin/sqlservr & ) | grep -q "Service Broker manager has started" \
    && /opt/sqlpackage/sqlpackage /a:Publish /tsn:. /tdn:$DBNAME /tu:sa /tp:$SA_PASSWORD /sf:/tmp/TestDB.Build.dacpac
