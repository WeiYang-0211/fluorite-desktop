namespace Launchpad.Services;

public sealed record MusicState(string Title, string Artist, string Player, bool Playing, bool CanPlay, bool CanNext, bool CanPrevious, byte[]? Artwork, string? Error = null, double Progress = -1.0)
{
	public static MusicState Empty { get; } = new MusicState("暂无正在播放的音乐", "在播放器中播放音乐后，这里会自动显示", "", Playing: false, CanPlay: false, CanNext: false, CanPrevious: false, null);
}
