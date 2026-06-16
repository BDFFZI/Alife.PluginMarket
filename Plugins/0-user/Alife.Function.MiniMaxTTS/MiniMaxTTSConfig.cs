namespace Alife.Function.Speech;

public class MiniMaxTTSConfig
{
    /// <summary>MiniMax API 基础地址</summary>
    public string BaseUrl { get; set; } = "https://api.minimaxi.com";

    /// <summary>API Key（Bearer Token）</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>模型名</summary>
    public string Model { get; set; } = "speech-2.8-hd";

    /// <summary>音色 ID</summary>
    public string VoiceId { get; set; } = "female_society_magazine";

    /// <summary>语速（0.5~2.0）</summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>音量（1~10）</summary>
    public int Volume { get; set; } = 1;

    /// <summary>音调（-12~12）</summary>
    public int Pitch { get; set; } = 0;

    /// <summary>情感：happy / sad / angry / fearful / surprise / disgust / neutral / auto 或留空</summary>
    public string Emotion { get; set; } = "";

    /// <summary>输出音频格式：mp3 / wav / flac / pcm</summary>
    public string AudioFormat { get; set; } = "mp3";
}
