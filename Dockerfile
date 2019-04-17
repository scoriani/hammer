FROM microsoft/dotnet:2.2-runtime
WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.csproj ./
#RUN dotnet restore

# copy and build everything else
COPY . ./
#RUN dotnet publish -c Release -o out
ENTRYPOINT ["dotnet", "out/Hammer.dll"]