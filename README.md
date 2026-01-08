Solution prr Right Click



Add → New Project

Select: xUnit Test Project

Name:



LearningCoreWebApi.Tests



Target framework:

.NET 8.0



Required NuGet Packages (Test Project)



Test project me ye packages honi chahiye:



xunit

xunit.runner.visualstudio

Moq

Microsoft.AspNetCore.Http





Install-Package Microsoft.EntityFrameworkCore -Version 8.0.0

Install-Package Microsoft.EntityFrameworkCore.SqlServer -Version 8.0.0

Install-Package Microsoft.EntityFrameworkCore.Tools -Version 8.0.0



// install pkg by nuget

Serilog.AspNetCore

Serilog.Settings.Configuration

Serilog.Enrichers.Environment

Serilog.Enrichers.Thread



// remove this portion from appsettings.json file, as we are now handling this logging using serilog props

"Logging": {

&nbsp; "LogLevel": {

&nbsp;   "Default": "Information",

&nbsp;   "Microsoft.AspNetCore": "Warning"

&nbsp; }

},



BCrypt.Net-Next



dotnet add package MailKit

cd LearningCoreWebApi



myaccount.google.com/apppasswords



dotnet clean

dotnet build

Drop-Database

Add-Migration InitialClean

Update-Database
Add-Migration InitialAuthTables
Update-Database

docker build -f LearningCoreWebApi/Dockerfile -t learningcoreapi-image .

docker.io/library/learningcoreapi-image:latest

docker run -d -p 8080:80 --name my-running-api learningcoreapi-image

http://localhost:8080/swagger/index.html

powershell -Command "Test-NetConnection -ComputerName 'localhost' -Port 1433"
sqlservermanager16.msc
docker network ls
netstat -ant