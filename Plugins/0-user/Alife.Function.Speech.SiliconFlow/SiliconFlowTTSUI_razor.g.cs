namespace Alife.Function.Speech;

using System;
using System.Threading.Tasks;
using Alife.Framework;
using AntDesign;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

/// <summary>硅基流动 TTS 配置组件</summary>
public class SiliconFlowTTSUI : ModuleUIBase<SiliconFlowTTSModel, SiliconFlowTTSConfig>
{
    int _s;

    static readonly (string Label, string Value)[] PresetVoices = new[]
    {
        ("沉稳女声 anna",      "FunAudioLLM/CosyVoice2-0.5B:anna"),
        ("热情女声 bella",     "FunAudioLLM/CosyVoice2-0.5B:bella"),
        ("温柔女声 claire",    "FunAudioLLM/CosyVoice2-0.5B:claire"),
        ("活泼女声 diana",     "FunAudioLLM/CosyVoice2-0.5B:diana"),
        ("沉稳男声 alex",      "FunAudioLLM/CosyVoice2-0.5B:alex"),
        ("低沉男声 benjamin",  "FunAudioLLM/CosyVoice2-0.5B:benjamin"),
        ("磁性男声 charles",   "FunAudioLLM/CosyVoice2-0.5B:charles"),
        ("阳光男声 david",     "FunAudioLLM/CosyVoice2-0.5B:david"),
    };

    static readonly string[] Formats = { "mp3", "wav", "pcm", "opus" };

    // ── 克隆用字段 ──────────────────────────────────────────
    string? _cloneAudioPath;
    string? _cloneVoiceName;
    string? _cloneText;        // 参考音频对应的文字内容
    string? _cloneStatus;
    string? _clonedUri;        // 克隆成功后拿到的 URI
    bool _isCloning;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Configuration == null)
        {
            builder.AddMarkupContent(0, "<div style='padding:20px;color:#999;'>配置加载中...</div>");
            return;
        }
        _s = 1;

        // ═══════════════════════════════════════════
        //  API Key
        // ═══════════════════════════════════════════
        builder.AddMarkupContent(_s++, "<div style='margin-bottom:4px;font-size:13px;'>API Key</div>");
        builder.OpenElement(_s++, "input");
        builder.AddAttribute(_s++, "type", "password");
        builder.AddAttribute(_s++, "value", Configuration.ApiKey);
        builder.AddAttribute(_s++, "placeholder", "sk-...");
        builder.AddAttribute(_s++, "style", "width:100%;padding:4px 8px;border:1px solid #d9d9d9;border-radius:4px;font-size:13px;");
        builder.AddAttribute(_s++, "onchange",
            EventCallback.Factory.Create<ChangeEventArgs>(this, e => Configuration.ApiKey = e.Value?.ToString() ?? ""));
        builder.CloseElement();

        // ═══════════════════════════════════════════
        //  模型名（只读）
        // ═══════════════════════════════════════════
        builder.AddMarkupContent(_s++, "<div style='margin:12px 0 4px 0;font-size:13px;'>模型</div>");
        builder.AddMarkupContent(_s++,
            "<div style='padding:6px 8px;background:#f5f5f5;border-radius:4px;font-size:13px;color:#333;'>" +
            Configuration.Model + "</div>");

        // ═══════════════════════════════════════════
        //  音色（预设快捷按钮 + 输入框）
        // ═══════════════════════════════════════════
        builder.AddMarkupContent(_s++, "<div style='margin:12px 0 4px 0;font-size:13px;'>音色</div>");
        foreach (var (label, value) in PresetVoices)
        {
            bool active = Configuration.Voice == value;
            string v = value;
            builder.OpenElement(_s++, "button");
            builder.AddAttribute(_s++, "type", "button");
            builder.AddAttribute(_s++, "style",
                "padding:3px 8px;margin:2px;border:1px solid " +
                (active ? "#1677ff" : "#d9d9d9") + ";background:" +
                (active ? "#1677ff" : "#fff") + ";color:" +
                (active ? "#fff" : "#333") + ";border-radius:4px;cursor:pointer;font-size:12px;");
            builder.AddAttribute(_s++, "onclick",
                EventCallback.Factory.Create<MouseEventArgs>(this, _ =>
                {
                    Configuration.Voice = v;
                    StateHasChanged();
                }));
            builder.AddMarkupContent(_s++, label);
            builder.CloseElement();
        }

        // 音色值输入框（支持克隆 URI 或手动输入）
        builder.AddMarkupContent(_s++, "<div style='margin-top:4px;'></div>");
        builder.OpenElement(_s++, "input");
        builder.AddAttribute(_s++, "type", "text");
        builder.AddAttribute(_s++, "value", Configuration.Voice);
        builder.AddAttribute(_s++, "placeholder", "FunAudioLLM/CosyVoice2-0.5B:anna 或 speech:xxx");
        builder.AddAttribute(_s++, "style", "width:100%;padding:4px 8px;border:1px solid #d9d9d9;border-radius:4px;font-size:12px;");
        builder.AddAttribute(_s++, "onchange",
            EventCallback.Factory.Create<ChangeEventArgs>(this, e => Configuration.Voice = e.Value?.ToString() ?? ""));
        builder.CloseElement();

        // ═══════════════════════════════════════════
        //  语速 Slider
        // ═══════════════════════════════════════════
        builder.AddMarkupContent(_s++, "<div style='margin:12px 0 4px 0;font-size:13px;'>语速 (" +
            Configuration.Speed.ToString("F2") + ")</div>");
        builder.OpenComponent(_s++, typeof(Slider<double>));
        builder.AddComponentParameter(_s++, "Min", 0.25);
        builder.AddComponentParameter(_s++, "Max", 4.0);
        builder.AddComponentParameter(_s++, "Step", 0.05);
        builder.AddComponentParameter(_s++, "DefaultValue", 1.0);
        builder.AddComponentParameter(_s++, "Value", Configuration.Speed);
        builder.AddComponentParameter(_s++, "ValueChanged",
            EventCallback.Factory.Create<double>(this, v => Configuration.Speed = Math.Round(v, 2)));
        builder.AddComponentParameter(_s++, "Style", "width:100%;");
        builder.CloseComponent();

        // ═══════════════════════════════════════════
        //  增益 Slider
        // ═══════════════════════════════════════════
        builder.AddMarkupContent(_s++, "<div style='margin:12px 0 4px 0;font-size:13px;'>音量增益 dB (" +
            Configuration.Gain.ToString("F1") + ")</div>");
        builder.OpenComponent(_s++, typeof(Slider<double>));
        builder.AddComponentParameter(_s++, "Min", -10.0);
        builder.AddComponentParameter(_s++, "Max", 10.0);
        builder.AddComponentParameter(_s++, "Step", 0.5);
        builder.AddComponentParameter(_s++, "DefaultValue", 0.0);
        builder.AddComponentParameter(_s++, "Value", Configuration.Gain);
        builder.AddComponentParameter(_s++, "ValueChanged",
            EventCallback.Factory.Create<double>(this, v => Configuration.Gain = Math.Round(v, 1)));
        builder.AddComponentParameter(_s++, "Style", "width:100%;");
        builder.CloseComponent();

        // ═══════════════════════════════════════════
        //  输出格式（纯 HTML radio）
        // ═══════════════════════════════════════════
        builder.AddMarkupContent(_s++, "<div style='margin:12px 0 4px 0;font-size:13px;'>输出格式</div>");
        builder.OpenElement(_s++, "div");
        builder.AddAttribute(_s++, "style", "display:flex;gap:12px;");
        foreach (var fmt in Formats)
        {
            string f = fmt;
            builder.OpenElement(_s++, "label");
            builder.AddAttribute(_s++, "style", "font-size:13px;cursor:pointer;");
            builder.OpenElement(_s++, "input");
            builder.AddAttribute(_s++, "type", "radio");
            builder.AddAttribute(_s++, "name", "sf_format");
            builder.AddAttribute(_s++, "value", f);
            builder.AddAttribute(_s++, "checked", Configuration.ResponseFormat == f);
            builder.AddAttribute(_s++, "onchange",
                EventCallback.Factory.Create<ChangeEventArgs>(this, _ =>
                {
                    Configuration.ResponseFormat = f;
                    if (f == "opus") Configuration.SampleRate = 48000;
                    else if (Configuration.SampleRate == 48000) Configuration.SampleRate = 44100;
                }));
            builder.CloseElement();
            builder.AddMarkupContent(_s++, " " + f.ToUpper());
            builder.CloseElement();
        }
        builder.CloseElement();

        // ═══════════════════════════════════════════
        //  采样率
        // ═══════════════════════════════════════════
        bool isOpus = Configuration.ResponseFormat == "opus";
        string srMsg = isOpus ? "opus 仅 48000" : "8000 / 16000 / 24000 / 32000 / 44100";
        builder.AddMarkupContent(_s++, "<div style='margin:12px 0 4px 0;font-size:13px;'>采样率 (" + srMsg + ")</div>");
        builder.OpenElement(_s++, "input");
        builder.AddAttribute(_s++, "type", "number");
        builder.AddAttribute(_s++, "value", Configuration.SampleRate.ToString());
        builder.AddAttribute(_s++, "min", isOpus ? "48000" : "8000");
        builder.AddAttribute(_s++, "max", isOpus ? "48000" : "44100");
        builder.AddAttribute(_s++, "step", "1000");
        builder.AddAttribute(_s++, "style", "width:120px;padding:4px 8px;border:1px solid #d9d9d9;border-radius:4px;font-size:13px;");
        builder.AddAttribute(_s++, "onchange",
            EventCallback.Factory.Create<ChangeEventArgs>(this, e =>
            {
                if (int.TryParse(e.Value?.ToString(), out int sr)) Configuration.SampleRate = sr;
            }));
        builder.CloseElement();

        // ═══════════════════════════════════════════
        //  ── 分割线 ──
        // ═══════════════════════════════════════════
        builder.AddMarkupContent(_s++, "<hr style='margin:16px 0;border:none;border-top:1px solid #eee;'/>");

        // ═══════════════════════════════════════════
        //  音色克隆（上传方式 → 用户预定义音色）
        // ═══════════════════════════════════════════
        builder.AddMarkupContent(_s++, "<div style='margin-bottom:8px;font-size:13px;font-weight:bold;'>🎤 音色克隆（上传参考音频）</div>");

        // 参考音频路径
        builder.AddMarkupContent(_s++, "<div style='margin-bottom:4px;font-size:12px;color:#666;'>参考音频 WAV 路径（&lt;30秒）</div>");
        builder.OpenElement(_s++, "input");
        builder.AddAttribute(_s++, "type", "text");
        builder.AddAttribute(_s++, "value", _cloneAudioPath ?? "");
        builder.AddAttribute(_s++, "placeholder", "D:\\path\\ref.wav");
        builder.AddAttribute(_s++, "style", "width:100%;padding:4px 8px;border:1px solid #d9d9d9;border-radius:4px;font-size:13px;");
        builder.AddAttribute(_s++, "onchange",
            EventCallback.Factory.Create<ChangeEventArgs>(this, e => _cloneAudioPath = e.Value?.ToString() ?? ""));
        builder.CloseElement();

        // 参考音频文字内容（新增！）
        builder.AddMarkupContent(_s++, "<div style='margin:8px 0 4px 0;font-size:12px;color:#666;'>参考音频对应的文字内容（必填，用于训练校准）</div>");
        builder.OpenElement(_s++, "textarea");
        builder.AddAttribute(_s++, "value", _cloneText ?? "");
        builder.AddAttribute(_s++, "placeholder", "请输入这段音频说的是什么...");
        builder.AddAttribute(_s++, "rows", "2");
        builder.AddAttribute(_s++, "style", "width:100%;padding:4px 8px;border:1px solid #d9d9d9;border-radius:4px;font-size:13px;resize:vertical;");
        builder.AddAttribute(_s++, "onchange",
            EventCallback.Factory.Create<ChangeEventArgs>(this, e => _cloneText = e.Value?.ToString() ?? ""));
        builder.CloseElement();

        // 克隆音色名称
        builder.AddMarkupContent(_s++, "<div style='margin:8px 0 4px 0;font-size:12px;color:#666;'>克隆音色名称（用作标识）</div>");
        builder.OpenElement(_s++, "input");
        builder.AddAttribute(_s++, "type", "text");
        builder.AddAttribute(_s++, "value", _cloneVoiceName ?? "");
        builder.AddAttribute(_s++, "placeholder", "my-voice");
        builder.AddAttribute(_s++, "style", "width:100%;padding:4px 8px;border:1px solid #d9d9d9;border-radius:4px;font-size:13px;");
        builder.AddAttribute(_s++, "onchange",
            EventCallback.Factory.Create<ChangeEventArgs>(this, e => _cloneVoiceName = e.Value?.ToString() ?? ""));
        builder.CloseElement();

        // 克隆按钮
        builder.OpenElement(_s++, "div");
        builder.AddAttribute(_s++, "style", "margin-top:8px;");
        builder.OpenElement(_s++, "button");
        builder.AddAttribute(_s++, "type", "button");
        builder.AddAttribute(_s++, "disabled", _isCloning);
        builder.AddAttribute(_s++, "style",
            "padding:4px 12px;background:#1677ff;color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:13px;" +
            (_isCloning ? "opacity:0.6;" : ""));
        builder.AddAttribute(_s++, "onclick",
            EventCallback.Factory.Create<MouseEventArgs>(this, async _ =>
            {
                try
                {
                    _isCloning = true;
                    _cloneStatus = null;
                    _clonedUri = null;
                    StateHasChanged();
                    _clonedUri = await DoCloneAsync();
                    _cloneStatus = _clonedUri?.StartsWith("speech:") == true
                        ? "✅ 克隆成功！音色 ID 已填入下方文本框，可直接用于合成。"
                        : _clonedUri ?? "克隆失败";
                }
                finally
                {
                    _isCloning = false;
                    StateHasChanged();
                }
            }));
        builder.AddMarkupContent(_s++, _isCloning ? "克隆中..." : "🎤 上传并克隆");
        builder.CloseElement();
        builder.CloseElement();

        // 克隆结果回显
        if (!string.IsNullOrEmpty(_clonedUri))
        {
            // 自动填入选中的音色
            bool success = _clonedUri?.StartsWith("speech:") == true;

            builder.AddMarkupContent(_s++,
                "<div style='margin-top:8px;font-size:12px;font-weight:bold;color:" +
                (success ? "#52c41a" : "#ff4d4f") + ";'>" +
                (success ? "✅ 克隆成功" : "❌ 克隆失败") + "</div>");

            builder.AddMarkupContent(_s++, "<div style='margin:4px 0 2px 0;font-size:12px;color:#666;'>音色 URI（已自动填入音色输入框）</div>");
            builder.OpenElement(_s++, "input");
            builder.AddAttribute(_s++, "type", "text");
            builder.AddAttribute(_s++, "readonly", "readonly");
            builder.AddAttribute(_s++, "value", _clonedUri);
            builder.AddAttribute(_s++, "style",
                "width:100%;padding:4px 8px;background:#f5f5f5;border:1px solid #d9d9d9;" +
                "border-radius:4px;font-size:12px;color:#333;cursor:text;");
            builder.CloseElement();


        }
        else if (_cloneStatus != null && _clonedUri == null)
        {
            builder.AddMarkupContent(_s++,
                "<div style='margin-top:6px;padding:6px 8px;background:#fff2f0;" +
                "border:1px solid #ffccc7;border-radius:4px;font-size:12px;color:#ff4d4f;'>" +
                _cloneStatus + "</div>");
        }

        // ═══════════════════════════════════════════
        //  提示信息
        // ═══════════════════════════════════════════
        builder.AddMarkupContent(_s++, "<div style='margin-top:12px;padding:8px 12px;background:#f6f8fa;" +
            "border:1px solid #d0d7de;border-radius:4px;font-size:12px;color:#666;'>" +
            "💡 <b>预设音色</b>：anna沉稳/bella热情/claire温柔/diana活泼（女声），" +
            "alex沉稳/benjamin低沉/charles磁性/david阳光（男声）。<br/>" +
            "💡 <b>音色克隆</b>：上传&lt;30秒的 WAV 参考音频，<b>必须提供文字内容</b>，点击按钮克隆。" +
            "成功后 URI 自动填入音色框，可直接使用。<br/>" +
            "💡 输入文本自动去除空格，最大支持128k字符。</div>");
    }

    // ── 执行克隆（HTTP 直调，不依赖 Model 实例） ────────────
    async Task<string?> DoCloneAsync()
    {
        var cfg = Configuration;
        if (cfg == null || string.IsNullOrWhiteSpace(cfg.ApiKey))
            return "错误: 请先配置 API Key";
        if (string.IsNullOrWhiteSpace(_cloneAudioPath))
            return "错误: 请填写参考音频路径";
        if (!System.IO.File.Exists(_cloneAudioPath))
            return "错误: 文件不存在";
        if (string.IsNullOrWhiteSpace(_cloneText))
            return "错误: 请填写参考音频的文字内容";

        try
        {
            var http = new System.Net.Http.HttpClient();
            var bytes = await System.IO.File.ReadAllBytesAsync(_cloneAudioPath);
            var b64 = Convert.ToBase64String(bytes);

            var body = new System.Collections.Generic.Dictionary<string, object?>
            {
                ["model"] = cfg.Model,
                ["audio"] = b64,
                ["customName"] = string.IsNullOrWhiteSpace(_cloneVoiceName) ? "cloned_voice" : _cloneVoiceName,
                ["text"] = _cloneText,
            };

            var json = System.Text.Json.JsonSerializer.Serialize(body, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
            });
            var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post,
                $"{cfg.BaseUrl.TrimEnd('/')}/v1/uploads/audio/voice")
            {
                Content = content,
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cfg.ApiKey);

            var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return $"错误: 克隆失败 ({err})";
            }

            var result = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(result);
            var uri = doc.RootElement.GetProperty("uri").GetString();

            if (!string.IsNullOrEmpty(uri))
            {
                // 自动选用克隆音色
                Configuration.Voice = uri;
                return uri;
            }
            return "错误: 未获取到 URI";
        }
        catch (Exception ex)
        {
            return $"错误: {ex.Message}";
        }
    }
}
