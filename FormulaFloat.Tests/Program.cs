using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FormulaFloat;

if (args.Contains("--render")) { LayoutRender.Run(); return; }

int passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
void Throws(Action action, string name) { try { action(); } catch (InvalidOperationException) { Check(true, name); return; } throw new Exception("FAIL: " + name); }
var settings = new ModelSettings { Endpoint = "https://example.test/v1/", Model = "vision-test", ApiKey = "test-only" };
Check(settings.GetEndpoint().AbsoluteUri == "https://example.test/v1/chat/completions", "base URL normalization");
settings.Endpoint = "http://localhost:11434/v1/chat/completions";
Check(settings.GetEndpoint().AbsolutePath == "/v1/chat/completions", "full local endpoint preserved");
settings.Endpoint = "http://example.test/v1";
Throws(() => settings.Validate(), "remote HTTP rejected");
settings.Endpoint = "https://example.test/v1?api_key=bad";
Throws(() => settings.Validate(), "query secrets rejected");
settings.Endpoint = "https://example.test/v1";
Check(VisionClient.CleanLatex("```latex\n\\[ x^2 \\]\n```") == "x^2", "fences and display delimiters removed");
Check(VisionClient.CleanLatex("$a$ + $b$") == "$a$ + $b$", "multiple inline expressions preserved");
Check(VisionClient.ParseResponse("{\"choices\":[{\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"x^2\"}]}}]}") == "x^2", "content-array response");
Throws(() => VisionClient.ParseResponse("{\"choices\":[]}"), "empty choices rejected");
Throws(() => VisionClient.ParseResponse("not json"), "malformed response rejected");
Throws(() => VisionClient.ParseResponse("{\"choices\":[{\"message\":{\"content\":null}}]}"), "empty content rejected");
Throws(() => VisionClient.ParseResponse("{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"x\"}}]}"), "truncated formula rejected");
var secret = SecretProtection.Protect("test-secret-中文");
Check(!secret.Contains("test-secret") && SecretProtection.Unprotect(secret) == "test-secret-中文", "Windows DPAPI round trip");
Check(!JsonSerializer.Serialize(settings).Contains("test-only"), "API key excluded from JSON");
var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1sAAAAASUVORK5CYII=");
var handler = new FakeHandler(async (request, ct) =>
{
    Check(request.RequestUri!.AbsoluteUri == "https://example.test/v1/chat/completions", "POST target");
    Check(request.Method == HttpMethod.Post && request.Headers.Authorization!.Parameter == "test-only", "POST and authorization");
    using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
    var parts = doc.RootElement.GetProperty("messages")[1].GetProperty("content");
    var prompt = parts[0].GetProperty("text").GetString()!;
    Check(prompt.Contains("x^2") && prompt.Contains("改成 alpha"), "refinement carries current formula and instruction");
    Check(parts[1].GetProperty("image_url").GetProperty("url").GetString() == "data:image/png;base64," + Convert.ToBase64String(png), "original image supplied");
    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"\\\\alpha^2\"}}]}") };
});
Check(await new VisionClient(new HttpClient(handler)).ConvertAsync(settings, png, "x^2", "改成 alpha", default) == @"\alpha^2", "refinement result parsed");
var errorHandler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("sensitive provider body") }));
try { await new VisionClient(new HttpClient(errorHandler)).ConvertAsync(settings, png, "", "", default); throw new Exception("Expected error"); }
catch (InvalidOperationException ex) { Check(ex.Message.Contains("401") && !ex.Message.Contains("sensitive"), "safe actionable HTTP error"); }
var cancelHandler = new FakeHandler(async (_, ct) => { await Task.Delay(10000, ct); return new(HttpStatusCode.OK); });
using var cancel = new CancellationTokenSource(80);
try { await new VisionClient(new HttpClient(cancelHandler)).ConvertAsync(settings, png, "", "", cancel.Token); throw new Exception("Expected cancellation"); }
catch (OperationCanceledException) { Check(true, "user cancellation propagated"); }
Console.WriteLine($"All {passed} checks passed.");

sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
}
