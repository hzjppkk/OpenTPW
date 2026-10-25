using OpenTPW.UI;
using Veldrid;

namespace OpenTPW;

/// <summary>Two-button message dialog, table 0x74b468 and 0x47d290.</summary>
internal sealed class LobbyConfirmPanel : Panel
{
	private readonly RootPanel hud;
	private readonly LobbyUiModel backdrop, background, cancel, okay;
	private readonly LobbyUiText message;
	private Action? confirm;
	private ulong activatedFrame;
	private static readonly Rectangle CancelBox = LobbyNamingPanel.Box( 1387, 610, 1470, 694 ),
		OkayBox = LobbyNamingPanel.Box( 1379, 522, 1462, 605 );
	public bool Active => confirm != null;
	public LobbyConfirmPanel( RootPanel hud )
	{
		this.hud = hud;
		backdrop = new LobbyUiModel( "LOLIGHT", LobbyNamingPanel.Box( 0, 0, 2047, 1535 ) );
		background = new LobbyUiModel( LobbyControlResources.ResolveModel( 0xd8cca935 ), LobbyNamingPanel.Box( 500, 233, 1516, 830 ) );
		cancel = new LobbyUiModel( "b_exit", CancelBox );
		okay = new LobbyUiModel( "b_okay", OkayBox );
		message = new LobbyUiText( "", LobbyNamingPanel.Box( 610, 276, 1345, 753 ), LobbyFonts.Name( 4 ) );
	}
	public void Show( string text, Action accepted )
	{
		message.Text = text; confirm = accepted; activatedFrame = Input.FrameNumber;
		hud.Modal = this;
	}
	private void Close() { confirm = null; if ( hud.Modal == this ) hud.Modal = null; }
	protected override void OnUpdate()
	{
		if ( !Active || activatedFrame == Input.FrameNumber ) return;
		int State( Rectangle bounds ) => LobbyNamingPanel.Hit( bounds ) ? Input.Mouse.Left ? 3 : 2 : 0;
		cancel.State = State( CancelBox ); okay.State = State( OkayBox );
		if ( Input.Pressed( Key.Escape ) || Input.LeftPressed && LobbyNamingPanel.Hit( CancelBox ) ) { Close(); return; }
		if ( Input.Pressed( Key.Enter ) || Input.LeftPressed && LobbyNamingPanel.Hit( OkayBox ) )
		{
			var accepted = confirm!; Close(); accepted();
		}
	}
	protected override void OnRender()
	{
		if ( !Active ) return;
		backdrop.Draw(); background.Draw(); cancel.Draw(); okay.Draw(); message.Draw();
	}
}
