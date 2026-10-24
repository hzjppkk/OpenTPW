namespace OpenTPW;

internal sealed class LobbyLayout : Layout
{
	public override void OnInit()
	{
		var players = new LobbyPlayerPanel( Level.Current );
		Level.Current.PlayerMenu = players;
		Hud.AddChild( players );
	}
}
