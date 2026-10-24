namespace OpenTPW;

/// <summary>Page-scoped island music/SFX handles (TP.exe 0x5dc512/0x5dc536 and resume paths).</summary>
public sealed class LobbyAudio : IDisposable
{
	private readonly AudioSystem audio;
	private string? theme;
	private long music, ambience;
	public LobbyAudio( AudioSystem audio ) => this.audio = audio;
	public void SetIsland( string nextTheme )
	{
		if ( theme == nextTheme ) return;
		Dispose();
		theme = nextTheme;
		music = audio.Play( $"levels/{theme}/Sound", "cat_locallobbymusic", 1 );
		ambience = audio.Play( $"levels/{theme}/Sound", "cat_locallobbysfx", 2 );
	}
	public void Dispose()
	{
		audio.Stop( music ); audio.Stop( ambience );
		music = ambience = 0;
		theme = null;
	}
}
