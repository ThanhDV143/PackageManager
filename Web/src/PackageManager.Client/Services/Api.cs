using System.Net;
using System.Net.Http.Json;
using PackageManager.Shared;

namespace PackageManager.Client.Services;

public class ApiException(string message, HttpStatusCode status) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

/// <summary>Typed calls to the server API. Non-success responses become <see cref="ApiException"/> with a Vietnamese message.</summary>
public class Api(HttpClient http)
{
    public static string Esc(string value) => Uri.EscapeDataString(value);

    // Packages
    public Task<PagedResult<PackageSummaryDto>> ListPackages(string sort, int page, int pageSize) =>
        Get<PagedResult<PackageSummaryDto>>($"api/packages?sort={Esc(sort)}&page={page}&pageSize={pageSize}");
    public Task<PackageDetailDto?> GetPackage(string id) => GetOrNull<PackageDetailDto>($"api/packages/{Esc(id)}");
    public Task<List<PackageLookupDto>> Lookup() => Get<List<PackageLookupDto>>("api/packages/lookup");
    public Task<PackageDetailDto> CreatePackage(PackageEditDto dto) => Send<PackageDetailDto>(HttpMethod.Post, "api/packages", dto);
    public Task<PackageDetailDto> UpdatePackage(string id, PackageEditDto dto) => Send<PackageDetailDto>(HttpMethod.Put, $"api/packages/{Esc(id)}", dto);
    public Task DeletePackage(string id, bool deleteFiles) => Send(HttpMethod.Delete, $"api/packages/{Esc(id)}?deleteFiles={deleteFiles}");
    public Task<PackageDetailDto> RefreshPackage(string id) => Send<PackageDetailDto>(HttpMethod.Post, $"api/packages/{Esc(id)}/refresh");
    public Task DeleteVersion(int id, bool deleteFile) => Send(HttpMethod.Delete, $"api/versions/{id}?deleteFile={deleteFile}");
    public Task<PackageEditDto> ReadAssetStore(string url) => Get<PackageEditDto>($"api/assetstore?url={Esc(url)}");
    public Task<List<UploadResolveItem>> ResolveUploads(List<string> names) =>
        Send<List<UploadResolveItem>>(HttpMethod.Post, "api/uploads/resolve", new UploadResolveRequest { FileNames = names });

    public static string VersionDownloadUrl(int versionId) => $"api/versions/{versionId}/download";
    public static string UploadUrl(string packageId, string version, string fileName, string? unity) =>
        $"api/packages/{Esc(packageId)}/versions/upload?version={Esc(version)}&fileName={Esc(fileName)}" +
        (string.IsNullOrWhiteSpace(unity) ? "" : $"&unityVersion={Esc(unity)}");

    // Unmanaged files
    public Task<int> UnmanagedCount() => Get<int>("api/unmanaged/count");
    public Task<UnmanagedScanDto> ScanUnmanaged() => Get<UnmanagedScanDto>("api/unmanaged");
    public Task<PackageDetailDto> AssignFiles(AssignFilesRequest req) => Send<PackageDetailDto>(HttpMethod.Post, "api/unmanaged/assign", req);
    public Task IgnoreFiles(List<string> paths) => Send(HttpMethod.Post, "api/unmanaged/ignore", new IgnoreFilesRequest { RelativePaths = paths });

    // UPM & search
    public Task<UpmListDto> ListUpm() => Get<UpmListDto>("api/upm");
    public Task<UpmPackageDetailDto?> GetUpm(string name) => GetOrNull<UpmPackageDetailDto>($"api/upm/{Esc(name)}");
    public Task<SearchResponseDto> Search(string q) => Get<SearchResponseDto>($"api/search?q={Esc(q)}");

    // Auth
    public Task<AuthStateDto> Me() => Get<AuthStateDto>("api/auth/me");
    public Task<AuthStateDto> Login(LoginRequest req) => Send<AuthStateDto>(HttpMethod.Post, "api/auth/login", req);
    public Task Logout() => Send(HttpMethod.Post, "api/auth/logout");

    private async Task<T> Get<T>(string url)
    {
        using var res = await SendRaw(new HttpRequestMessage(HttpMethod.Get, url));
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T?> GetOrNull<T>(string url) where T : class
    {
        try { return await Get<T>(url); }
        catch (ApiException ex) when (ex.Status == HttpStatusCode.NotFound) { return null; }
    }

    private async Task<T> Send<T>(HttpMethod method, string url, object? body = null)
    {
        using var res = await SendRaw(Build(method, url, body));
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task Send(HttpMethod method, string url, object? body = null)
    {
        using var _ = await SendRaw(Build(method, url, body));
    }

    private static HttpRequestMessage Build(HttpMethod method, string url, object? body) =>
        new(method, url) { Content = body == null ? null : JsonContent.Create(body) };

    private async Task<HttpResponseMessage> SendRaw(HttpRequestMessage req)
    {
        HttpResponseMessage res;
        try { res = await http.SendAsync(req); }
        catch (HttpRequestException) { throw new ApiException("Không kết nối được máy chủ.", 0); }

        if (res.IsSuccessStatusCode) return res;
        string message = await ReadError(res);
        res.Dispose();
        throw new ApiException(message, res.StatusCode);
    }

    public static string ErrorMessage(HttpStatusCode status, string? body)
    {
        if (!string.IsNullOrWhiteSpace(body) && body.TrimStart().StartsWith('{'))
        {
            try
            {
                var err = System.Text.Json.JsonSerializer.Deserialize<ApiError>(body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
                if (!string.IsNullOrWhiteSpace(err?.Message)) return err.Message;
            }
            catch (System.Text.Json.JsonException) { }
        }
        return status switch
        {
            HttpStatusCode.Unauthorized => "Phiên đăng nhập đã hết — đăng nhập lại.",
            HttpStatusCode.Forbidden => "Bạn không có quyền làm việc này.",
            HttpStatusCode.NotFound => "Không tìm thấy.",
            HttpStatusCode.TooManyRequests => "Thử quá nhiều lần, đợi vài phút rồi thử lại.",
            HttpStatusCode.RequestEntityTooLarge => "File quá lớn so với giới hạn của máy chủ / reverse proxy.",
            _ => $"Lỗi máy chủ ({(int)status}).",
        };
    }

    private static async Task<string> ReadError(HttpResponseMessage res)
    {
        string body = "";
        try { body = await res.Content.ReadAsStringAsync(); } catch (HttpRequestException) { }
        return ErrorMessage(res.StatusCode, body);
    }
}
