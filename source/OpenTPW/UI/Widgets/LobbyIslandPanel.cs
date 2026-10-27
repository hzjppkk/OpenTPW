using OpenTPW.UI;
using Veldrid;

namespace OpenTPW;

/// <summary>Island controls from 0x753a60, initialized by 0x4b6700.</summary>
internal sealed class LobbyIslandPanel : Panel
{
	private readonly LobbyNavigation navigation;
	private readonly LobbyUiModel background, enter, globe, next, previous, key, cost, goldKey;
	private readonly LobbyUiText count, title, error;
	public event Action? GlobeRequested;
	public bool Active { get; set; } = true;
	private ulong activatedFrame;
	/// <summary>Elements whose help has already placed a MessageTag1 since the page was activated.</summary>
	private readonly HashSet<int> tagged = new();
	private bool helpShown;
	// Do not let the click that left the previous page activate a newly visible control.
	public void Activate() { Active = true; activatedFrame = Input.FrameNumber; tagged.Clear(); }
	public string Failure { get => error.Text; set => error.Text = value; }
	private static Rectangle Box( float l, float t, float r, float b ) => LobbyNamingPanel.Box( l, t, r, b );
	private static readonly Rectangle EnterBox = Box( 68, 1114, 221, 1267 ), GlobeBox = Box( 68, 946, 221, 1099 ),
		NextBox = Box( 97, 1299, 250, 1453 ), PreviousBox = Box( 260, 1299, 413, 1453 );
	/// <summary>
	/// UIHELPTEXT numbers from the island table's key 0x11 (US 0x757f60): enter 0x1e0e8 → 345, globe 0x1e0e9 → 346,
	/// left arrow 0x1e0f0 → 343, right arrow 0x1e0f1 → 344.
	/// </summary>
	private static readonly (Rectangle Bounds, int Help, bool Arrow)[] helpElements =
		[(EnterBox, 345, false), (GlobeBox, 346, false), (NextBox, 343, true), (PreviousBox, 344, true)];
	private static LobbyUiModel Model( uint hash, Rectangle box )
	{
		var model = new LobbyUiModel( LobbyControlResources.ResolveModel( hash ), box );
		// Build every authored state with the scene so hover, press and disabled frames never load mid-frame.
		model.PreloadAll();
		return model;
	}
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
		// US 0x4b8ca0: element 0x1e0ed (gkey.MD2) in group 0x1e0ec is set to state 3 and drawn beside the count.
		goldKey = Model( 0x1d50cb14, Box( 1853, 76, 1955, 178 ) );
		goldKey.State = 3;
		count = new LobbyUiText( "", Box( 1688, 82, 1829, 172 ), LobbyFonts.Name( 2 ) ) { HorizontalAlignment = 2 };
		title = new LobbyUiText( "", Box( 477, 1303, 1571, 1437 ), LobbyFonts.Name( 0 ), 0xfce807ff );
		error = new LobbyUiText( "", Box( 477, 1438, 2047, 1535 ) );
	}
	protected override void OnUpdate()
	{
		if ( !Active || !navigation.Active || activatedFrame == Input.FrameNumber || navigation.Entering )
		{
			HideHelp();
			if ( Active && navigation.Active && activatedFrame != Input.FrameNumber && navigation.Entering && Input.Pressed( Key.Escape ) )
				navigation.CancelEntry();
			return;
		}
		// US 0x4b9340/0x4b9840 disable both arrows in GameType 2; a disabled model control draws state 1 (see LobbyNamingPanel).
		bool arrows = !navigation.InstantAction;
		int State( Rectangle bounds, bool enabled = true ) => !enabled ? 1 : LobbyNamingPanel.Hit( bounds ) ? Input.Mouse.Left ? 3 : 2 : 0;
		enter.State = State( EnterBox ); globe.State = State( GlobeBox );
		next.State = State( NextBox, arrows ); previous.State = State( PreviousBox, arrows );
		UpdateHelp( arrows );
		bool Click( Rectangle bounds ) => Input.LeftPressed && LobbyNamingPanel.Hit( bounds );
		// 0x4b65d0 and 0x5dc310 both route left to vtable +0x34 (previous)
		// and right to +0x30 (next); the model resource names do not define direction.
		if ( Input.Pressed( Key.Left ) || arrows && Click( NextBox ) ) { Failure = ""; navigation.Navigate( -1 ); }
		else if ( Input.Pressed( Key.Right ) || arrows && Click( PreviousBox ) ) { Failure = ""; navigation.Navigate( 1 ); }
		else if ( Input.Pressed( Key.Enter ) || Click( EnterBox ) ) { Failure = ""; navigation.Enter(); }
		else if ( Click( GlobeBox ) ) GlobeRequested?.Invoke();
	}
	/// <summary>
	/// Shows the hovered element's help and, on its first help since activation, places MessageTag1 at its centre
	/// (0x48b9f0 event 0x11). A disabled arrow is treated as not hovered (inferred: retail disabled elements are
	/// skipped by hit testing; not yet compared with the original).
	/// </summary>
	private void UpdateHelp( bool arrows )
	{
		if ( Level.Current?.Help is not { } help ) return;
		int index = Array.FindIndex( helpElements, element => (arrows || !element.Arrow) && LobbyNamingPanel.Hit( element.Bounds ) );
		if ( index >= 0 && tagged.Add( index ) ) Level.Current.KeyEffects?.MessageTag( helpElements[index].Bounds );
		help.Show( index >= 0 ? helpElements[index].Help : -1 );
		helpShown = index >= 0;
	}
	private void HideHelp()
	{
		if ( !helpShown ) return;
		helpShown = false;
		Level.Current?.Help?.Show( -1 );
	}
	protected override void OnRender()
	{
		if ( !Active || !navigation.Active || navigation.Entering ) return;
		background.Draw(); enter.Draw(); globe.Draw(); next.Draw(); previous.Draw();
		// US 0x4b9340: GameType 2 hides the key 0x1e0ea (with its cost 0x1e0eb) and the count group 0x1e0ec.
		if ( !navigation.InstantAction )
		{
			// 0x4b6da0 selects separate authored frames for affordable and locked key costs.
			key.State = navigation.CanEnter ? 0 : 1;
			cost.State = navigation.RequiredKeys + (navigation.CanEnter ? -1 : 4);
			key.Draw(); cost.Draw();
			// US 0x4b9340 shows group 0x1e0ec only while the player holds keys; 0x758194 formats L"%d x".
			if ( navigation.Keys > 0 )
			{
				count.Text = $"{navigation.Keys} x";
				count.Draw(); goldKey.Draw();
			}
		}
		title.Text = navigation.IslandTitle;
		title.Draw(); error.Draw();
	}
}
