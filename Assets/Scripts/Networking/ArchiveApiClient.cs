using System;
using System.Collections.Generic;
using AmbedkarHeritage.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace AmbedkarHeritage.Networking
{
    // JSON DTOs for the FastAPI responses (list endpoints return a wrapper
    // object so JsonUtility can deserialize them into a List<T> field).
    [Serializable]
    public class ApiDocumentListDto
    {
        public List<ArchiveRecord> items;
        public int count;
    }

    [Serializable]
    public class ApiEventDto
    {
        public string id;
        public string title;
        public string description;
        public string date;
        public string category;
    }

    [Serializable]
    public class ApiEventListDto
    {
        public List<ApiEventDto> items;
        public int count;
    }

    [Serializable]
    public class ApiRelatedItemDto
    {
        public string relationshipType;
        public ArchiveRecord document;
    }

    [Serializable]
    public class ApiRelatedListDto
    {
        public string sourceId;
        public int count;
        public List<ApiRelatedItemDto> items;
    }

    [Serializable]
    public class ApiHealthDto
    {
        public string status;
        public string database;
    }

    // Phase E OCR DTOs — mirror the FastAPI OCR endpoints (camelCase).
    [Serializable]
    public class ApiOcrPageSummaryDto
    {
        public int pageNumber;
        public string status;
        public string language;
        public float confidence;
    }

    [Serializable]
    public class ApiOcrStatusDto
    {
        public string documentId;
        public string status;
        public int pages;
        public string language;
        public string error;
        public List<ApiOcrPageSummaryDto> pageSummaries = new List<ApiOcrPageSummaryDto>();
    }

    [Serializable]
    public class ApiOcrPageDto
    {
        public string documentId;
        public int pageNumber;
        public string status;
        public string language;
        public float confidence;
        public string extractedText;
        public string errorMessage;
    }

    /// <summary>
    /// Reusable REST client for the Ambedkar Digital Heritage backend.
    ///
    /// Runtime path uses UnityWebRequest (async, Quest-safe) with timeouts,
    /// retry-on-network-failure, HTTP error mapping and JSON parsing.
    /// Editor-only synchronous helpers (System.Net.Http) back the automated
    /// regression checks, which run in -batchmode without a frame pump.
    ///
    /// No secrets are ever hard-coded; the base URL comes from
    /// <see cref="Core.MuseumApp.ApiBaseUrl"/>.
    /// </summary>
    public static class ArchiveApiClient
    {
        public static float DefaultTimeoutSeconds = 10f;

        /// <summary>Max attempts per request (1 retry after a network failure).</summary>
        public static int MaxAttempts = 2;

        public static string LastError { get; private set; }

        private static string JoinUrl(string baseUrl, string path)
        {
            return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
        }

        // ------------------------------------------------------------------
        // Runtime (UnityWebRequest, callback based)
        // ------------------------------------------------------------------
        public static void GetDocuments(string baseUrl, Action<List<ArchiveRecord>, string> onDone)
        {
            RunList(baseUrl, "documents", onDone, attempt: 1);
        }

        public static void GetDocument(string baseUrl, string id, Action<ArchiveRecord, string> onDone)
        {
            if (string.IsNullOrEmpty(id))
            {
                onDone?.Invoke(null, "Empty document id");
                return;
            }

            UnityWebRequest request = UnityWebRequest.Get(JoinUrl(baseUrl, "documents/" + Uri.EscapeDataString(id)));
            request.timeout = (int)DefaultTimeoutSeconds;
            RunRaw(request, (text, error) =>
            {
                if (error != null)
                {
                    onDone?.Invoke(null, error);
                    return;
                }

                try
                {
                    ArchiveRecord record = JsonUtility.FromJson<ArchiveRecord>(text);
                    if (record == null)
                    {
                        onDone?.Invoke(null, "Malformed document payload");
                        return;
                    }

                    record.EnsureSafeDefaults();
                    onDone?.Invoke(record, null);
                }
                catch (Exception e)
                {
                    onDone?.Invoke(null, "JSON parse failed: " + e.Message);
                }
            });
        }

        public static void GetEvents(string baseUrl, Action<List<ArchiveRecord>, string> onDone)
        {
            Request(JoinUrl(baseUrl, "events"), (text, error) =>
            {
                if (error != null)
                {
                    onDone?.Invoke(null, error);
                    return;
                }

                try
                {
                    ApiEventListDto dto = JsonUtility.FromJson<ApiEventListDto>(text);
                    List<ArchiveRecord> records = new List<ArchiveRecord>();
                    if (dto != null && dto.items != null)
                    {
                        foreach (ApiEventDto e in dto.items)
                        {
                            records.Add(new ArchiveRecord
                            {
                                id = e.id,
                                type = ArchiveRecordType.Event,
                                title = e.title,
                                description = e.description,
                                date = e.date,
                                category = e.category,
                                isSampleData = false
                            });
                        }
                    }

                    onDone?.Invoke(records, null);
                }
                catch (Exception ex)
                {
                    onDone?.Invoke(null, "JSON parse failed: " + ex.Message);
                }
            });
        }

        public static void GetRelated(string baseUrl, string id, Action<List<ArchiveRecord>, string> onDone)
        {
            Request(JoinUrl(baseUrl, "related/" + Uri.EscapeDataString(id)), (text, error) =>
            {
                if (error != null)
                {
                    onDone?.Invoke(null, error);
                    return;
                }

                try
                {
                    ApiRelatedListDto dto = JsonUtility.FromJson<ApiRelatedListDto>(text);
                    List<ArchiveRecord> records = new List<ArchiveRecord>();
                    if (dto != null && dto.items != null)
                    {
                        foreach (ApiRelatedItemDto item in dto.items)
                        {
                            if (item.document == null)
                            {
                                continue;
                            }

                            item.document.EnsureSafeDefaults();
                            records.Add(item.document);
                        }
                    }

                    onDone?.Invoke(records, null);
                }
                catch (Exception ex)
                {
                    onDone?.Invoke(null, "JSON parse failed: " + ex.Message);
                }
            });
        }

        public static void CheckHealth(string baseUrl, Action<bool, string> onDone)
        {
            Request(JoinUrl(baseUrl, "health"), (text, error) =>
            {
                if (error != null)
                {
                    onDone?.Invoke(false, error);
                    return;
                }

                try
                {
                    ApiHealthDto dto = JsonUtility.FromJson<ApiHealthDto>(text);
                    bool ok = dto != null && dto.status == "ok";
                    onDone?.Invoke(ok, ok ? null : "Backend reporting degraded state");
                }
                catch (Exception ex)
                {
                    onDone?.Invoke(false, "JSON parse failed: " + ex.Message);
                }
            });
        }

        // Phase E: OCR status (document-level + per-page summaries) and full
        // per-page transcript. Never crash — failures surface via the error arg.
        public static void GetOcrStatus(string baseUrl, string id, Action<ApiOcrStatusDto, string> onDone)
        {
            if (string.IsNullOrEmpty(id))
            {
                onDone?.Invoke(null, "Empty document id");
                return;
            }

            Request(JoinUrl(baseUrl, "documents/" + Uri.EscapeDataString(id) + "/ocr"), (text, error) =>
            {
                if (error != null)
                {
                    onDone?.Invoke(null, error);
                    return;
                }

                try
                {
                    ApiOcrStatusDto dto = JsonUtility.FromJson<ApiOcrStatusDto>(text);
                    if (dto == null)
                    {
                        onDone?.Invoke(null, "Malformed OCR status payload");
                        return;
                    }

                    onDone?.Invoke(dto, null);
                }
                catch (Exception ex)
                {
                    onDone?.Invoke(null, "JSON parse failed: " + ex.Message);
                }
            });
        }

        public static void GetOcrPage(string baseUrl, string id, int pageNumber, Action<ApiOcrPageDto, string> onDone)
        {
            if (string.IsNullOrEmpty(id))
            {
                onDone?.Invoke(null, "Empty document id");
                return;
            }

            Request(JoinUrl(baseUrl, "documents/" + Uri.EscapeDataString(id) + "/ocr/pages/" + pageNumber),
                (text, error) =>
                {
                    if (error != null)
                    {
                        onDone?.Invoke(null, error);
                        return;
                    }

                    try
                    {
                        ApiOcrPageDto dto = JsonUtility.FromJson<ApiOcrPageDto>(text);
                        if (dto == null)
                        {
                            onDone?.Invoke(null, "Malformed OCR page payload");
                            return;
                        }

                        onDone?.Invoke(dto, null);
                    }
                    catch (Exception ex)
                    {
                        onDone?.Invoke(null, "JSON parse failed: " + ex.Message);
                    }
                });
        }

        private static void RunList(string baseUrl, string path, Action<List<ArchiveRecord>, string> onDone, int attempt)
        {
            Request(JoinUrl(baseUrl, path), (text, error) =>
            {
                if (error != null && IsRetryableError(error) && attempt < MaxAttempts)
                {
                    RunList(baseUrl, path, onDone, attempt + 1);
                    return;
                }

                if (error != null)
                {
                    onDone?.Invoke(null, error);
                    return;
                }

                try
                {
                    ApiDocumentListDto dto = JsonUtility.FromJson<ApiDocumentListDto>(text);
                    List<ArchiveRecord> records = new List<ArchiveRecord>();
                    if (dto != null && dto.items != null)
                    {
                        foreach (ArchiveRecord record in dto.items)
                        {
                            record.EnsureSafeDefaults();
                            records.Add(record);
                        }
                    }

                    onDone?.Invoke(records, null);
                }
                catch (Exception ex)
                {
                    onDone?.Invoke(null, "JSON parse failed: " + ex.Message);
                }
            });
        }

        private static void Request(string url, Action<string, string> onDone)
        {
            UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = (int)DefaultTimeoutSeconds;
            RunRaw(request, onDone);
        }

        private static void RunRaw(UnityWebRequest request, Action<string, string> onDone)
        {
            try
            {
                var operation = request.SendWebRequest();
                operation.completed += op =>
                {
                    using (request)
                    {
                        if (string.IsNullOrEmpty(request.error) && request.responseCode < 400)
                        {
                            LastError = null;
                            onDone?.Invoke(request.downloadHandler.text, null);
                        }
                        else
                        {
                            string error = FriendlyError(request);
                            LastError = error;
                            onDone?.Invoke(null, error);
                        }
                    }
                };
            }
            catch (Exception e)
            {
                LastError = "Request failed to start: " + e.Message;
                onDone?.Invoke(null, LastError);
            }
        }

        private static bool IsRetryableError(string error)
        {
            return error != null &&
                   (error.Contains("Network error")
                    || error.Contains("Cannot connect")
                    || error.Contains("Unable to resolve")
                    || error.Contains("timed out"));
        }

        /// <summary>Map a UnityWebRequest failure to a friendly, stable message.</summary>
        public static string FriendlyError(UnityWebRequest request)
        {
            if (request == null)
            {
                return "Null request";
            }

            if (request.responseCode == 0)
            {
                // Connection-level failure (refused / DNS / timeout).
                string detail = string.IsNullOrEmpty(request.error) ? "Connection failed" : request.error;
                return "Network error: " + detail;
            }

            switch (request.responseCode)
            {
                case 401: return "Unauthorized (401)";
                case 403: return "Forbidden (403)";
                case 404: return "Not found (404)";
                case 408: return "Request timed out (408)";
                case 422: return "Invalid input (422)";
                case 500: return "Server error (500)";
                case 503: return "Service unavailable (503)";
                default: return "HTTP " + request.responseCode;
            }
        }

        // ------------------------------------------------------------------
        // Editor-only synchronous helpers (batchmode regression checks).
        // UnityWebRequest completion needs a frame pump, which -executeMethod
        // does not provide, so these use System.Net.Http and block.
        // ------------------------------------------------------------------
#if UNITY_EDITOR
        public static bool TryGetDocumentsSyncEditor(string baseUrl, out List<ArchiveRecord> records,
            out string error, float timeoutSeconds = 20f)
        {
            records = null;
            error = null;
            try
            {
                using (System.Net.Http.HttpClient client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = System.TimeSpan.FromSeconds(timeoutSeconds);
                    var response = client.GetAsync(JoinUrl(baseUrl, "documents")).ConfigureAwait(false).GetAwaiter().GetResult();
                    string body = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                    {
                        error = "HTTP " + (int)response.StatusCode + " from /api/documents";
                        return false;
                    }

                    ApiDocumentListDto dto = JsonUtility.FromJson<ApiDocumentListDto>(body);
                    List<ArchiveRecord> parsed = new List<ArchiveRecord>();
                    if (dto != null && dto.items != null)
                    {
                        foreach (ArchiveRecord record in dto.items)
                        {
                            record.EnsureSafeDefaults();
                            parsed.Add(record);
                        }
                    }

                    records = parsed;
                    return true;
                }
            }
            catch (System.Exception e)
            {
                error = "API unreachable: " + e.Message;
                return false;
            }
        }

        public static bool TryGetHealthSyncEditor(string baseUrl, out string error, float timeoutSeconds = 10f)
        {
            error = null;
            try
            {
                using (System.Net.Http.HttpClient client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = System.TimeSpan.FromSeconds(timeoutSeconds);
                    var response = client.GetAsync(JoinUrl(baseUrl, "health")).ConfigureAwait(false).GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                    {
                        error = "HTTP " + (int)response.StatusCode + " from /api/health";
                        return false;
                    }

                    return true;
                }
            }
            catch (System.Exception e)
            {
                error = "API unreachable: " + e.Message;
                return false;
            }
        }

        // Phase E editor sync helpers: OCR status + per-page OCR text.
        public static bool TryGetOcrStatusSyncEditor(string baseUrl, string id,
            out ApiOcrStatusDto status, out string error, float timeoutSeconds = 20f)
        {
            status = null;
            error = null;
            try
            {
                using (System.Net.Http.HttpClient client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = System.TimeSpan.FromSeconds(timeoutSeconds);
                    string url = JoinUrl(baseUrl, "documents/" + Uri.EscapeDataString(id) + "/ocr");
                    var response = client.GetAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();
                    string body = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                    {
                        error = "HTTP " + (int)response.StatusCode + " from GET /api" + url.Substring(url.IndexOf("/documents"));
                        return false;
                    }

                    status = JsonUtility.FromJson<ApiOcrStatusDto>(body);
                    if (status == null)
                    {
                        error = "Malformed OCR status payload";
                        return false;
                    }

                    return true;
                }
            }
            catch (System.Exception e)
            {
                error = "API unreachable: " + e.Message;
                return false;
            }
        }

        public static bool TryGetOcrPageSyncEditor(string baseUrl, string id, int pageNumber,
            out ApiOcrPageDto page, out string error, float timeoutSeconds = 20f)
        {
            page = null;
            error = null;
            try
            {
                using (System.Net.Http.HttpClient client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = System.TimeSpan.FromSeconds(timeoutSeconds);
                    string url = JoinUrl(baseUrl, "documents/" + Uri.EscapeDataString(id) + "/ocr/pages/" + pageNumber);
                    var response = client.GetAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();
                    string body = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                    {
                        error = "HTTP " + (int)response.StatusCode + " from GET /api" + url.Substring(url.IndexOf("/documents"));
                        return false;
                    }

                    page = JsonUtility.FromJson<ApiOcrPageDto>(body);
                    if (page == null)
                    {
                        error = "Malformed OCR page payload";
                        return false;
                    }

                    return true;
                }
            }
            catch (System.Exception e)
            {
                error = "API unreachable: " + e.Message;
                return false;
            }
        }
#endif
    }
}