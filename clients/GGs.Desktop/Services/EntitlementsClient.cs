using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GGs.Shared.Api;
using Microsoft.Extensions.Configuration;

namespace GGs.Desktop.Services;

public sealed class EntitlementsClient
{
	private readonly HttpClient _http;

	public EntitlementsClient(HttpClient? http = null)
	{
		var cfg = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
			.AddJsonFile("appsettings.json", optional: true)
			.AddEnvironmentVariables()
			.Build();
		var baseUrl = cfg["Server:BaseUrl"] ?? "https://localhost:5001";
		_http = http ?? GGs.Shared.Http.SecureHttpClientFactory.GetOrCreate(baseUrl, userAgent: "GGs.Desktop");
	}

	public async Task<Entitlements?> FetchAsync(string accessToken, CancellationToken ct = default)
	{
		using var req = new HttpRequestMessage(HttpMethod.Get, "api/entitlements");
		req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
		var res = await _http.SendAsync(req, ct);
		if (!res.IsSuccessStatusCode) return null;
		var json = await res.Content.ReadAsStringAsync(ct);
		var doc = JsonDocument.Parse(json);
		if (!doc.RootElement.TryGetProperty("entitlements", out var entEl)) return null;
		return System.Text.Json.JsonSerializer.Deserialize<Entitlements>(entEl.GetRawText());
	}
}


