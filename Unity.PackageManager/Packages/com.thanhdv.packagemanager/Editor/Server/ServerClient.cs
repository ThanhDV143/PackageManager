using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PackageManager.Shared;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ThanhDV.PackageManager
{
    internal class ServerException : Exception
    {
        public long Status { get; }
        public ServerException(string message, long status) : base(message) => Status = status;
    }

    /// <summary>Typed calls to the portal's read-only API (no login needed). Failures become <see cref="ServerException"/>.</summary>
    internal static class ServerClient
    {
        public static bool IsConfigured =>
            Uri.TryCreate(ProjectData.Instance.ServerUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

        private static string BaseUrl => ProjectData.Instance.ServerUrl.Trim().TrimEnd('/');

        public static string Esc(string value) => Uri.EscapeDataString(value);

        public static string PackagePageUrl(string packageId) => $"{BaseUrl}/packages/{Esc(packageId)}";
        public static string UpmPageUrl(string name) => $"{BaseUrl}/upm/{Esc(name)}";

        public static async Task<List<PackageSummaryDto>> ListPackages()
        {
            var all = new List<PackageSummaryDto>();
            for (int page = 1; ; page++)
            {
                var result = await Get<PagedResult<PackageSummaryDto>>($"api/packages?sort={PackageSort.Name}&page={page}&pageSize=200");
                all.AddRange(result.Items);
                if (result.Items.Count == 0 || all.Count >= result.Total) return all;
            }
        }

        /// <returns>null when the server no longer has the package.</returns>
        public static async Task<PackageDetailDto> GetPackage(string packageId)
        {
            try { return await Get<PackageDetailDto>($"api/packages/{Esc(packageId)}"); }
            catch (ServerException e) when (e.Status == 404) { return null; }
        }

        public static Task<UpmListDto> ListUpm() => Get<UpmListDto>("api/upm");

        /// <summary>A package's main image (an absolute URL, usually the Asset Store CDN); cached for the editor session.</summary>
        public static async Task<Texture2D> GetImage(string url)
        {
            if (ImageCache.TryGetValue(url, out var cached) && cached != null) return cached;
            using var request = UnityWebRequestTexture.GetTexture(url);
            await Send(request, null);
            var texture = DownloadHandlerTexture.GetContent(request);
            texture.hideFlags = HideFlags.HideAndDontSave;
            ImageCache[url] = texture;
            return texture;
        }

        private static readonly Dictionary<string, Texture2D> ImageCache = new Dictionary<string, Texture2D>();

        /// <returns>null when the registry no longer has the package.</returns>
        public static async Task<UpmPackageDetailDto> GetUpm(string name)
        {
            try { return await Get<UpmPackageDetailDto>($"api/upm/{Esc(name)}"); }
            catch (ServerException e) when (e.Status == 404) { return null; }
        }

        /// <summary>
        /// Streams a version's file to <paramref name="destination"/> (via a temp file, so a failed download leaves nothing behind).
        /// <paramref name="progress"/> gets the fraction done and the bytes received so far.
        /// </summary>
        public static async Task Download(int versionId, string destination, Action<float, ulong> progress)
        {
            string temp = destination + ".download";
            using (var request = new UnityWebRequest(Url($"api/versions/{versionId}/download"), UnityWebRequest.kHttpVerbGET))
            {
                request.downloadHandler = new DownloadHandlerFile(temp) { removeFileOnAbort = true };
                await Send(request, progress);
            }
            if (File.Exists(destination)) File.Delete(destination);
            File.Move(temp, destination);
        }

        private static async Task<T> Get<T>(string path)
        {
            using var request = UnityWebRequest.Get(Url(path));
            await Send(request, null);
            return JsonConvert.DeserializeObject<T>(request.downloadHandler.text)
                ?? throw new ServerException("The server returned an empty response.", request.responseCode);
        }

        private static string Url(string path)
        {
            if (!IsConfigured) throw new ServerException("No server URL set (Project Settings > ThanhDV Package Manager).", 0);
            return $"{BaseUrl}/{path}";
        }

        /// <summary>Awaits a request on the editor loop; throws <see cref="ServerException"/> unless it succeeded.</summary>
        private static Task Send(UnityWebRequest request, Action<float, ulong> progress)
        {
            // The server's error messages default to Vietnamese (the web UI); ask for English.
            request.SetRequestHeader("Accept-Language", "en");
            var done = new TaskCompletionSource<bool>();
            EditorApplication.CallbackFunction tick = null;
            if (progress != null)
            {
                tick = () => progress(request.downloadProgress, request.downloadedBytes);
                EditorApplication.update += tick;
            }
            request.SendWebRequest().completed += _ =>
            {
                if (tick != null) EditorApplication.update -= tick;
                if (request.result == UnityWebRequest.Result.Success) done.SetResult(true);
                else done.SetException(ToException(request));
            };
            return done.Task;
        }

        private static ServerException ToException(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError)
                return new ServerException($"Cannot reach the server ({request.error}).", 0);

            long status = request.responseCode;
            // DownloadHandlerFile has no text; error bodies only matter for API calls.
            string body = request.downloadHandler is DownloadHandlerBuffer ? request.downloadHandler.text : null;
            if (!string.IsNullOrWhiteSpace(body) && body.TrimStart().StartsWith("{"))
            {
                try
                {
                    var error = JsonConvert.DeserializeObject<ApiError>(body);
                    if (!string.IsNullOrWhiteSpace(error?.Message)) return new ServerException(error.Message, status);
                }
                catch (JsonException) { }
            }
            return new ServerException(status switch
            {
                404 => "Not found.",
                _ => $"Server error ({status}).",
            }, status);
        }
    }
}
