using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GGs.E2ETests;

public class Prompt16Tests
{
    [Fact]
    public async Task Users_And_Tweaks_Paging_Filtering_Sorting_Work()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));

        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("accessToken").GetString()!;

        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Seed: create two tweaks for sorting/paging
        var t1 = await authed.PostAsJsonAsync("/api/v1/tweaks", new { Name = "A tweak", Description = "Alpha", CommandType = 2, ScriptContent = "echo A" });
        t1.EnsureSuccessStatusCode();
        await Task.Delay(10);
        var t2 = await authed.PostAsJsonAsync("/api/v1/tweaks", new { Name = "B tweak", Description = "Beta", CommandType = 2, ScriptContent = "echo B" });
        t2.EnsureSuccessStatusCode();

        // Tweaks: page size 1, page 1 with sort by name asc should return A first
        var list1 = await authed.GetAsync("/api/v1/tweaks?page=1&pageSize=1&sort=name&desc=false&q=tweak");
        list1.EnsureSuccessStatusCode();
        Assert.True(list1.Headers.TryGetValues("X-Total-Count", out var totalVals) && int.Parse(totalVals.First()) >= 2);
        var arr1 = JsonDocument.Parse(await list1.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("A tweak", arr1[0].GetProperty("name").GetString());

        // Tweaks: page 2 returns B tweak
        var list2 = await authed.GetAsync("/api/v1/tweaks?page=2&pageSize=1&sort=name&desc=false&q=tweak");
        list2.EnsureSuccessStatusCode();
        var arr2 = JsonDocument.Parse(await list2.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("B tweak", arr2[0].GetProperty("name").GetString());

        // Users: filter for admin
        var users = await authed.GetAsync("/api/v1/users?page=1&pageSize=10&q=admin");
        users.EnsureSuccessStatusCode();
        Assert.True(users.Headers.Contains("X-Total-Count"));
        var usersArr = JsonDocument.Parse(await users.Content.ReadAsStringAsync()).RootElement;
        Assert.True(usersArr.GetArrayLength() >= 1);
    }
}

