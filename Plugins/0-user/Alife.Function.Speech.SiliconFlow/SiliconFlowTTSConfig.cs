namespace Alife.Function.Speech;

public class SiliconFlowTTSConfig
{
    /// <summary>SiliconFlow API 基础地址</summary>
    public string BaseUrl { get; set; } = "https://api.siliconflow.cn";

    /// <summary>API Key（Bearer Token）</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>模型名</summary>
    public string Model { get; set; } = "FunAudioLLM/CosyVoice2-0.5B";

    /// <summary>
    /// 音色（预设音色格式 model:voice_name，克隆音色格式 speech:xxx）。
    /// 预设可选: anna / bella / claire / diana / alex / benjamin / charles / david
    /// </summary>
    public string Voice { get; set; } = "FunAudioLLM/CosyVoice2-0.5B:anna";

    /// <summary>输出格式: mp3 / opus / wav / pcm</summary>
    public string ResponseFormat { get; set; } = "wav";

    /// <summary>
    /// 采样率:
    /// - mp3: 32000 / 44100
    /// - wav/pcm: 8000 / 16000 / 24000 / 32000 / 44100
    /// - opus: 仅 48000
    /// </summary>
    public int SampleRate { get; set; } = 24000;

    /// <summary>语速（0.25 ~ 4.0，默认 1.0）</summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>音量增益 dB（-10 ~ 10，默认 0.0）</summary>
    public double Gain { get; set; } = 0.0;

    /// <summary>
    /// 动态音色克隆参考音频（JSON 数组，每项含 audio 和 name 字段）。
    /// 例如: [{"audio":"base64_wav_data","name":"ref1"}]
    /// 留空则不使用动态克隆。
    /// </summary>
    public string CloneReferences { get; set; } = "";
}
