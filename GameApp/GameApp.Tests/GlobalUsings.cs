global using Xunit;
global using Moq;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.AspNetCore.Mvc.Testing;
global using Microsoft.AspNetCore.Http;

// Check test coverage:
// dotnet reload
// dotnet test --collect:"XPlat Code Coverage"
// reportgenerator -reports:GameApp.Tests/TestResults/**/coverage.cobertura.xml - targetdir:coverage - report - reporttypes:Html