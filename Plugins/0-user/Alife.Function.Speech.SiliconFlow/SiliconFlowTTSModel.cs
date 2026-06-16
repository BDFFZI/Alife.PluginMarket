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

[Module("硅基流动 TTS", "对接 SiliconFlow CosyVoice2-0.5B 在线语音合成，支持8种预设音色及音色克隆",
    defaultCategory: "Alife 官方/模型接入/语音模型",
    EditorUI = typeof(SiliconFlowTTSUI))]
public class SiliconFlowTTSModel(
    ILogger<SiliconFlowTTSModel> logger
) : ISpeechModel,
    IConfigurable<SiliconFlowTTSConfig>
{
    static readonly HttpClient _http = new();
    static readonly JsonSerializerOptions _jsonOpt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public SiliconFlowTTSConfig? Configuration { get; set; }

    // ═══════════════════════════════════════
    //  Text → Speech
    //  API: POST {BaseUrl}/v1/audio/speech
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
            return "错误: 请先配置 API Key";
        }

        if (text.Length > 128000)
        {
            logger.LogWarning("输入文本过长({Len})，截断至128k", text.Length);
            text = text[..128000];
        }

        // 去空格（API 要求）
        text = text.Replace(" ", "").Replace("\n", "").Replace("\r", "");

        string md5Hash;
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
            md5Hash = Convert.ToHexString(hashBytes);
        }

        string ext = cfg.ResponseFormat;
        string safeFileName = $"siliconflow_{md5Hash}.{ext}";
        string outputPath = Path.Combine(AlifePath.TempFolderPath, safeFileName);

        if (File.Exists(outputPath))
            return outputPath;

        try
        {
            var body = new Dictionary<string, object?>
            {
                ["model"] = cfg.Model,
                ["input"] = text,
                ["voice"] = cfg.Voice,
                ["response_format"] = cfg.ResponseFormat,
                ["sample_rate"] = cfg.SampleRate,
                ["speed"] = cfg.Speed,
                ["gain"] = cfg.Gain,
                ["stream"] = false,
            };

            var extraBody = new Dictionary<string, object?>();
            if (!string.IsNullOrWhiteSpace(cfg.CloneReferences))
            {
                try
                {
                    var refs = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(cfg.CloneReferences);
                    extraBody["references"] = refs;
                }
                catch { }
            }
            if (extraBody.Count > 0)
                body["extra_body"] = extraBody;

            var json = JsonSerializer.Serialize(body, _jsonOpt);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post,
                $"{cfg.BaseUrl.TrimEnd('/')}/v1/audio/speech")
            {
                Content = content,
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cfg.ApiKey);

            var response = await _http.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("API 错误: {Status} {Error}",
                    (int)response.StatusCode, errorBody);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var file = File.Create(outputPath);
            await stream.CopyToAsync(file, cancellationToken);

            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length < 200)
            {
                logger.LogError("输出文件无效: {Path}", outputPath);
                return null;
            }

            return outputPath;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "硅基流动 TTS 请求失败");
            return null;
        }
    }

    // ═══════════════════════════════════════
    //  上传参考音频 → 预定义克隆音色 URI
    //  POST {BaseUrl}/v1/uploads/audio/voice
    // ═══════════════════════════════════════

    public async Task<string?> CloneVoiceAsync(string audioPath, string voiceName, string? transcript = null)
    {
        var cfg = Configuration;
        if (cfg == null || string.IsNullOrWhiteSpace(cfg.ApiKey))
            return "错误: 请先配置 API Key";

        if (!File.Exists(audioPath))
            return "错误: 参考音频文件不存在";

        if (string.IsNullOrWhiteSpace(transcript))
            return "错误: 请提供参考音频的文字内容（text 参数必填）";

        try
        {
            var bytes = await File.ReadAllBytesAsync(audioPath);
            var b64 = Convert.ToBase64String(bytes);

            var body = new Dictionary<string, object?>
            {
                ["model"] = cfg.Model,
                ["audio"] = b64,
                ["customName"] = voiceName,
                ["text"] = transcript,
            };

            var json = JsonSerializer.Serialize(body, _jsonOpt);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post,
                $"{cfg.BaseUrl.TrimEnd('/')}/v1/uploads/audio/voice")
            {
                Content = content,
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cfg.ApiKey);

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                logger.LogError("克隆 API 错误: {Status} {Error}",
                    (int)response.StatusCode, errorBody);
                return $"错误: 克隆失败 ({errorBody})";
            }

            var result = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(result);
            var uri = doc.RootElement.GetProperty("uri").GetString();
            return uri;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "音色克隆失败");
            return $"错误: {ex.Message}";
        }
    }
}
