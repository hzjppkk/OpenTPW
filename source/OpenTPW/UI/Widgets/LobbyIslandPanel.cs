using OpenTPW.UI;
using Veldrid;

namespace OpenTPW;

/// <summary>Island controls from 0x753a60, initialized by 0x4b6700.</summary>
internal sealed class LobbyIslandPanel : Panel
{
	private readonly LobbyNavigation navigation;
	private readonly LobbyUiModel background, enter, globe, next, previous, key, cost;
	private readonly LobbyUiText count, title, error;
	public event Action? GlobeRequested;
	public bool Active { get; set; } = true;
	private ulong activatedFrame;
	public void Activate() { Active = true; activatedFrame = Input.FrameNumber; }
	public string Failure { get => error.Text; set => error.Text = value; }
	private static Rectangle Box( float l, float t, float r, float b ) => LobbyNamingPanel.Box( l, t, r, b );
	private static readonly Rectangle EnterBox = Box( 68, 1114, 221, 1267 ), GlobeBox = Box( 68, 946, 221, 1099 ),
		NextBox = Box( 97, 1299, 250, 1453 ), PreviousBox = Box( 260, 1299, 413, 1453 );
	private static LobbyUiModel Model( uint hash, Rectangle box ) => new( LobbyControlResources.ResolveModel( hash ), box );
	public LobbyIslandPanel( LobbyNavigation navigation )
	{
		this.navigation = navigation;
		background = Model( 0xa1f674e6, Box( 28, 855, 476, 1521 ) );
		enter = Model( 0x9044a4cc, EnterBox );
		globe = Model( 0x64b12618, GlobeBox );
		next = Model( 0x795da8e1, NextBox );
		previous = Model( 0xf1d0542a, PreviousBox );
		key = Model( 0x2f575715, Box( 554, 170, 990, 437 ) );
		cost = Model( 0x21aea625, Box( 1045, 191, 1405, 416 ) );
		count = new LobbyUiText( "", Box( 1688, 82, 1829, 172 ), LobbyFonts.Name( 2 ) ) { HorizontalAlignment = 2 };
		title = new LobbyUiText( "", Box( 477, 1303, 1571, 1437 ), LobbyFonts.Name( 0 ), 0xfce807ff );
		error = new LobbyUiText( "", Box( 477, 1438, 2047, 1535 ) );
	}
	protected override void OnUpdate()
	{
		if ( !Active || !navigation.Active || activatedFrame == Input.FrameNumber ) return;
		if ( navigation.Entering )
		{
			if ( Input.Pressed( Key.Escape ) ) navigation.CancelEntry();
			return;
		}
		int State( Rectangle bounds ) => LobbyNamingPanel.Hit( bounds ) ? Input.Mouse.Left ? 3 : 2 : 0;
		enter.State = State( EnterBox ); globe.State = State( GlobeBox );
		next.State = State( NextBox ); previous.State = State( PreviousBox );
		bool Click( Rectangle bounds ) => Input.LeftPressed && LobbyNamingPanel.Hit( bounds );
		// 0x4b65d0 maps the visual left button (0x1e0f0) to vtable +0x34,
		// and the visual right button (0x1e0f1) to +0x30: opposite the keyboard handlers.
		if ( Input.Pressed( Key.Left ) || Click( PreviousBox ) ) { Failure = ""; navigation.Navigate( 1 ); }
		else if ( Input.Pressed( Key.Right ) || Click( NextBox ) ) { Failure = ""; navigation.Navigate( -1 ); }
		else if ( Input.Pressed( Key.Enter ) || Click( EnterBox ) ) { Failure = ""; navigation.Enter(); }
		else if ( Click( GlobeBox ) ) GlobeRequested?.Invoke();
	}
	protected override void OnRender()
	{
		if ( !Active || !navigation.Active || navigation.Entering ) return;
		background.Draw(); enter.Draw(); globe.Draw(); next.Draw(); previous.Draw();
		key.State = navigation.CanEnter ? 0 : 1;
		cost.State = navigation.RequiredKeys + (navigation.CanEnter ? -1 : 4);
		key.Draw(); cost.Draw();
		count.Text = navigation.Keys > 0 ? $"{navigation.Keys} x" : "";
		count.Draw();
		title.Text = navigation.Island.Name;
		title.Draw(); error.Draw();
	}
}
