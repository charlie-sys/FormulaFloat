using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FormulaFloat;

public sealed class ModelSettings
{
    public string Endpoint { get; set; } = "";
    public string Model { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 90;
    public string ProtectedKey { get; set; } = "";
    [JsonIgnore] public string ApiKey { get; set; } = "";
    public Uri GetEndpoint()
    {
        if (!Uri.TryCreate(Endpoint.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("请输入 HTTPS 接口地址（本机服务允许 HTTP），不要包含密钥、查询参数或片段。");
        string path = uri.AbsoluteUri.TrimEnd('/');
        return new Uri(path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) ? path : path + "/chat/completions");
    }
    public void Validate()
    {
        GetEndpoint();
        if (string.IsNullOrWhiteSpace(Model)) throw new InvalidOperationException("请填写支持图片输入的模型名称。");
        if (TimeoutSeconds < 10 || TimeoutSeconds > 300) throw new InvalidOperationException("超时应为 10–300 秒。");
    }
}

public static class SettingsStore
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FormulaFloat");
    public static string FilePath => Path.Combine(Folder, "settings.json");
    public static ModelSettings Load()
    {
        if (!File.Exists(FilePath)) return new();
        var settings = JsonSerializer.Deserialize<ModelSettings>(File.ReadAllText(FilePath)) ?? new();
        settings.ApiKey = string.IsNullOrEmpty(settings.ProtectedKey) ? "" : SecretProtection.Unprotect(settings.ProtectedKey);
        return settings;
    }
    public static void Save(ModelSettings settings)
    {
        settings.Validate();
        settings.ProtectedKey = string.IsNullOrEmpty(settings.ApiKey) ? "" : SecretProtection.Protect(settings.ApiKey);
        Directory.CreateDirectory(Folder);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
}

// DPAPI is scoped to the current Windows user. No secret is written in plaintext.
public static class SecretProtection
{
    [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    public static string Protect(string value) => Convert.ToBase64String(Transform(Encoding.UTF8.GetBytes(value), true));
    public static string Unprotect(string value) => Encoding.UTF8.GetString(Transform(Convert.FromBase64String(value), false));
    private static byte[] Transform(byte[] bytes, bool protect)
    {
        var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
        Blob output = default;
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            bool ok = protect ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new InvalidOperationException("无法读取或加密密钥，请重新填写密钥。");
            var result = new byte[output.Length]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
        }
        finally { Marshal.FreeHGlobal(input.Data); if (output.Data != IntPtr.Zero) LocalFree(output.Data); Array.Clear(bytes); }
    }
}
