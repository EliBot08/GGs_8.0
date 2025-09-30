using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GGs.LoadTests;

public static class LoadTestScenarios
{
    private static readonly HttpClient HttpClient = new();
    private const string BaseUrl = "https://localhost:7117"; // Adjust for your server

    public static NBomberContext CreateLoadTestContext()
    {
        return NBomberRunner
            .RegisterScenarios(
                AuthenticationLoadTest(),
                EntitlementsLoadTest(),
                TweaksApiLoadTest(),
                ScimProvisioningLoadTest(),
                HealthCheckLoadTest()
            );
    }

    private static Scenario AuthenticationLoadTest()
    {
        var loginPayload = new
        {
            Email = "loadtest@example.com",
            Password = "LoadTest123!"
        };

        var step = Step.Create("login_request", async context =>
        {
            var json = JsonSerializer.Serialize(loginPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpClient.PostAsync($"{BaseUrl}/api/auth/login", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                var loginResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
                
                if (loginResponse.TryGetProperty("token", out var tokenElement))
                {
                    context.Data["jwt_token"] = tokenElement.GetString();
                }
                
                return Response.Ok();
            }

            return Response.Fail($"Login failed: {response.StatusCode}");
        });

        return Scenario.Create("authentication_load", step)
            .WithLoadSimulations(
                Simulation.InjectPerSec(rate: 10, during: TimeSpan.FromMinutes(2)),
                Simulation.KeepConstant(copies: 50, during: TimeSpan.FromMinutes(3))
            );
    }

    private static Scenario EntitlementsLoadTest()
    {
        var step = Step.Create("get_entitlements", async context =>
        {
            var token = context.Data.TryGetValue("jwt_token", out var jwtToken) ? jwtToken?.ToString() : null;
            
            if (string.IsNullOrEmpty(token))
            {
                // Generate a mock JWT for load testing
                token = GenerateMockJwtToken();
            }

            var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/entitlements");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await HttpClient.SendAsync(request);
            
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"Entitlements failed: {response.StatusCode}");
        });

        return Scenario.Create("entitlements_load", step)
            .WithLoadSimulations(
                Simulation.InjectPerSec(rate: 20, during: TimeSpan.FromMinutes(2)),
                Simulation.KeepConstant(copies: 100, during: TimeSpan.FromMinutes(5))
            );
    }

    private static Scenario TweaksApiLoadTest()
    {
        var step = Step.Create("get_tweaks", async context =>
        {
            var token = context.Data.TryGetValue("jwt_token", out var jwtToken) ? jwtToken?.ToString() : GenerateMockJwtToken();

            var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/tweaks");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await HttpClient.SendAsync(request);
            
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"Tweaks API failed: {response.StatusCode}");
        });

        return Scenario.Create("tweaks_api_load", step)
            .WithLoadSimulations(
                Simulation.InjectPerSec(rate: 15, during: TimeSpan.FromMinutes(2)),
                Simulation.KeepConstant(copies: 75, during: TimeSpan.FromMinutes(4))
            );
    }

    private static Scenario ScimProvisioningLoadTest()
    {
        var step = Step.Create("scim_get_users", async context =>
        {
            var token = context.Data.TryGetValue("jwt_token", out var jwtToken) ? jwtToken?.ToString() : GenerateMockJwtToken();

            var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/scim/v2/Users?count=10");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await HttpClient.SendAsync(request);
            
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"SCIM API failed: {response.StatusCode}");
        });

        return Scenario.Create("scim_provisioning_load", step)
            .WithLoadSimulations(
                Simulation.InjectPerSec(rate: 5, during: TimeSpan.FromMinutes(1)),
                Simulation.KeepConstant(copies: 20, during: TimeSpan.FromMinutes(3))
            );
    }

    private static Scenario HealthCheckLoadTest()
    {
        var step = Step.Create("health_check", async context =>
        {
            var response = await HttpClient.GetAsync($"{BaseUrl}/health/ready");
            
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail($"Health check failed: {response.StatusCode}");
        });

        return Scenario.Create("health_check_load", step)
            .WithLoadSimulations(
                Simulation.InjectPerSec(rate: 50, during: TimeSpan.FromMinutes(1)),
                Simulation.KeepConstant(copies: 200, during: TimeSpan.FromMinutes(2))
            );
    }

    private static string GenerateMockJwtToken()
    {
        // Generate a mock JWT for load testing (not for production use)
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" })));
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new 
        { 
            sub = "loadtest-user",
            roles = new[] { "BasicUser" },
            exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        })));
        var signature = Convert.ToBase64String(Encoding.UTF8.GetBytes("mock-signature"));
        
        return $"{header}.{payload}.{signature}";
    }
}

public static class LoadTestRunner
{
    public static async Task RunAllTests()
    {
        var scenario = LoadTestScenarios.CreateLoadTestContext();
        
        await NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFormats(ReportFormat.Html, ReportFormat.Csv, ReportFormat.Json)
            .WithReportFolder("load-test-results")
            .RunAsync();
    }

    public static async Task RunSpecificTest(string scenarioName)
    {
        var allScenarios = new[]
        {
            LoadTestScenarios.AuthenticationLoadTest(),
            LoadTestScenarios.EntitlementsLoadTest(),
            LoadTestScenarios.TweaksApiLoadTest(),
            LoadTestScenarios.ScimProvisioningLoadTest(),
            LoadTestScenarios.HealthCheckLoadTest()
        };

        var targetScenario = allScenarios.FirstOrDefault(s => s.ScenarioName == scenarioName);
        if (targetScenario == null)
        {
            throw new ArgumentException($"Scenario '{scenarioName}' not found");
        }

        await NBomberRunner
            .RegisterScenarios(targetScenario)
            .WithReportFormats(ReportFormat.Html, ReportFormat.Csv)
            .WithReportFolder($"load-test-results/{scenarioName}")
            .RunAsync();
    }
}
