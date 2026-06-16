using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Alife.Framework;
using Alife.Platform;
using Microsoft.Extensions.Logging;

namespace Alife.Function.Speech;

[Module("MiniMax TTS", "MiniMax T2A V2 在线语音合成，支持中英双语 300+ 音色及音色克隆",
    defaultCategory: "Alife 官方/模型接入/语音模型",
    EditorUI = typeof(MiniMaxTTSUI))]
public class MiniMaxTTSModel(
    ILogger<MiniMaxTTSModel> logger
) : ISpeechModel,
    IConfigurable<MiniMaxTTSConfig>
{
    static readonly HttpClient _http = new();
    static readonly JsonSerializerOptions _jsonOpt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public MiniMaxTTSConfig? Configuration { get; set; }

    // ═══════════════════════════════════════
    //  Text → Speech（T2A V2）
    //  API: POST /v1/t2a_v2  (BaseUrl + /v1/t2a_v2)
    //  Auth: Bearer <token>
    // ═══════════════════════════════════════

    public async Task<string?> GenerateSpeechFileAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var cfg = Configuration;
        if (cfg == null || string.IsNullOrWhiteSpace(cfg.ApiKey))
        {
            logger.LogError("API Key 未配置");
            return "错误：请先配置 API Key";
        }

        string md5Hash;
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
            md5Hash = Convert.ToHexString(hashBytes);
        }

        string ext = cfg.AudioFormat == "pcm" ? "pcm" : cfg.AudioFormat;
        string safeFileName = $"minimax_{md5Hash}.{ext}";
        string outputPath = Path.Combine(AlifePath.TempFolderPath, safeFileName);

        if (File.Exists(outputPath))
            return outputPath;

        try
        {
            // ── 构建 voice_setting ──
            var voiceSetting = new Dictionary<string, object?>
            {
                ["voice_id"] = cfg.VoiceId,
                ["speed"] = cfg.Speed,
                ["vol"] = cfg.Volume,
                ["pitch"] = cfg.Pitch,
            };
            if (!string.IsNullOrWhiteSpace(cfg.Emotion))
                voiceSetting["emotion"] = cfg.Emotion;

            var body = new Dictionary<string, object?>
            {
                ["model"] = cfg.Model,
                ["text"] = text,
                ["stream"] = false,
                ["output_format"] = "hex",
                ["voice_setting"] = voiceSetting,
                ["audio_setting"] = new Dictionary<string, object?>
                {
                    ["sample_rate"] = 32000,
                    ["format"] = cfg.AudioFormat,
                    ["channel"] = 1,
                },
            };

            string json = JsonSerializer.Serialize(body, _jsonOpt);
            string url = $"{cfg.BaseUrl.TrimEnd('/')}/v1/t2a_v2";

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cfg.ApiKey);

            using var response = await _http.SendAsync(
                request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            int statusCode = root.GetProperty("base_resp").GetProperty("status_code").GetInt32();
            if (statusCode != 0)
            {
                string errMsg = root.GetProperty("base_resp").GetProperty("status_msg").GetString() ?? "未知错误";
                logger.LogError("MiniMax T2A 返回错误: {Code} {Msg}", statusCode, errMsg);
                return $"API 错误 ({statusCode}): {errMsg}";
            }

            string hexAudio = root.GetProperty("data").GetProperty("audio").GetString()!;
            byte[] audioBytes = Convert.FromHexString(hexAudio);
            await File.WriteAllBytesAsync(outputPath, audioBytes, cancellationToken);

            logger.LogInformation("MiniMax TTS 合成成功: {Len} bytes → {Path}",
                audioBytes.Length, outputPath);
            return outputPath;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError("MiniMax API 请求失败: {Msg}", ex.Message);
            return $"API 请求失败：{ex.Message}";
        }
        catch (TaskCanceledException)
        {
            logger.LogError("MiniMax 请求超时");
            return "请求超时，请检查网络或 API 地址";
        }
        catch (JsonException ex)
        {
            logger.LogError("MiniMax 响应解析失败: {Msg}", ex.Message);
            return $"响应解析失败：{ex.Message}";
        }
        catch (Exception ex)
        {
            logger.LogError("MiniMax TTS 合成异常: {Msg}", ex.Message);
            return $"合成失败：{ex.Message}";
        }
    }

    // ═══════════════════════════════════════
    //  Voice Clone — 实例方法
    // ═══════════════════════════════════════

    public async Task<string?> CloneVoiceAsync(
        string audioFilePath,
        string newVoiceId,
        string? promptAudioPath = null,
        string? promptText = null,
        CancellationToken cancellationToken = default)
    {
        return await CloneVoiceStaticAsync(
            Configuration, audioFilePath, newVoiceId,
            promptAudioPath, promptText, logger, cancellationToken);
    }

    // ═══════════════════════════════════════
    //  Voice Clone — 静态方法
    // ═══════════════════════════════════════

    public static async Task<long> UploadFileAsync(
        MiniMaxTTSConfig cfg, string filePath, string purpose, CancellationToken ct)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("音频文件不存在", filePath);

        var fileBytes = await File.ReadAllBytesAsync(filePath, ct);
        string fileName = Path.GetFileName(filePath);

        using var formContent = new MultipartFormDataContent();
        formContent.Add(new StringContent(purpose), "purpose");
        formContent.Add(new ByteArrayContent(fileBytes), "file", fileName);

        string url = $"{cfg.BaseUrl.TrimEnd('/')}/v1/files/upload";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = formContent,
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cfg.ApiKey);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        int sc = root.GetProperty("base_resp").GetProperty("status_code").GetInt32();
        if (sc != 0)
        {
            string msg = root.GetProperty("base_resp").GetProperty("status_msg").GetString()
                ?? "上传失败";
            throw new Exception($"文件上传失败 ({sc}): {msg}");
        }

        return root.GetProperty("file").GetProperty("file_id").GetInt64();
    }

    public static async Task<string?> CloneVoiceStaticAsync(
        MiniMaxTTSConfig? cfg,
        string audioFilePath,
        string newVoiceId,
        string? promptAudioPath = null,
        string? promptText = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        if (cfg == null || string.IsNullOrWhiteSpace(cfg.ApiKey))
            return "错误：请先配置 API Key";
        if (string.IsNullOrWhiteSpace(newVoiceId))
            return "错误：请指定音色名称（voice_id）";

        try
        {
            logger?.LogInformation("上传源音频: {Path}", audioFilePath);
            long fileId;
            try
            {
                fileId = await UploadFileAsync(cfg, audioFilePath, "voice_clone", cancellationToken);
            }
            catch (FileNotFoundException)
            {
                return $"文件不存在：{audioFilePath}";
            }
            logger?.LogInformation("上传成功，file_id={Id}", fileId);

            long? promptFileId = null;
            if (!string.IsNullOrWhiteSpace(promptAudioPath))
            {
                logger?.LogInformation("上传提示音频: {Path}", promptAudioPath);
                promptFileId = await UploadFileAsync(cfg, promptAudioPath, "prompt_audio", cancellationToken);
                logger?.LogInformation("提示音频上传成功，file_id={Id}", promptFileId);
            }

            var cloneBody = new Dictionary<string, object?>
            {
                ["file_id"] = fileId,
                ["voice_id"] = newVoiceId,
                ["model"] = cfg.Model,
                ["need_noise_reduction"] = false,
                ["need_volume_normalization"] = false,
            };
            if (promptFileId.HasValue)
            {
                cloneBody["clone_prompt"] = new Dictionary<string, object?>
                {
                    ["prompt_audio"] = promptFileId.Value,
                    ["prompt_text"] = promptText ?? "",
                };
            }

            string json = JsonSerializer.Serialize(cloneBody, _jsonOpt);
            string url = $"{cfg.BaseUrl.TrimEnd('/')}/v1/voice_clone";

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cfg.ApiKey);

            using var response = await _http.SendAsync(
                request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            int statusCode = root.GetProperty("base_resp").GetProperty("status_code").GetInt32();
            if (statusCode != 0)
            {
                string errMsg = root.GetProperty("base_resp").GetProperty("status_msg").GetString()
                    ?? "未知错误";
                logger?.LogError("MiniMax 克隆返回错误: {Code} {Msg}", statusCode, errMsg);
                return $"克隆失败 ({statusCode}): {errMsg}";
            }

            cfg.VoiceId = newVoiceId;
            logger?.LogInformation("音色克隆成功: {VoiceId}", newVoiceId);
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger?.LogError("克隆 API 请求失败: {Msg}", ex.Message);
            return $"请求失败：{ex.Message}";
        }
        catch (Exception ex)
        {
            logger?.LogError("音色克隆异常: {Msg}", ex.Message);
            return $"克隆失败：{ex.Message}";
        }
    }
}
