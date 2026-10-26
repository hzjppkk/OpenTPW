using OpenTPW.UI;
using Veldrid;

namespace OpenTPW;

/// <summary>Local actions from the shared Escape menu (0x5dee20, 0x48a6c0, 0x489e00).</summary>
internal sealed class LobbyMenuPanel : Panel
{
	private readonly Level level;
	private readonly RootPanel hud;
	private readonly LobbyConfirmPanel confirmation;
	private List<(UIStrings Action, LobbyUiText Text, Rectangle Bounds)> items = new();
	private readonly Dictionary<bool, List<(UIStrings Action, LobbyUiText Text, Rectangle Bounds)>> layouts = new();
	private ulong activatedFrame;
	private int focused;
	public bool Active { get; private set; }
	internal LobbyMenuPanel( Level level, LobbyConfirmPanel confirmation )
	{
		this.level = level; hud = level.Hud; this.confirmation = confirmation;
	}
	internal void Show()
	{
		if ( Active || hud.Modal != null ) return;
		focused = 0;
		bool selected = level.Navigation!.Active;
		// Retain both layouts so reopening the menu does not allocate new GPU text textures.
		if ( layouts.TryGetValue( selected, out var cached ) ) items = cached;
		else
		{
			items = new();
			float top = 5;
			void Add( UIStrings action )
			{
				// 0x490cd0 centres measured text, starts at y=5 and adds a five-unit row gap.
				var label = new LobbyUiText( Localization.Parse( "#" + action ), new Rectangle( 0, 0, 2048, 1536 ), LobbyFonts.Name( 0 ) );
				float width = label.Measure( label.Text ) * 3.2f, height = (label.LineHeight + 5) * 3.2f;
				var bounds = new Rectangle( (2048 - width) / 2, top, width, height );
				items.Add( (action, new LobbyUiText( label.Text, bounds, LobbyFonts.Name( 0 ) ), bounds) );
				top += height + 5;
			}
			// The online service and engine-wide settings page are separate from these local routes.
			if ( selected ) Add( UIStrings.SelectNewPlayer );
			Add( UIStrings.ResumeGame ); Add( UIStrings.QuitGame );
			layouts.Add( selected, items );
		}
		Active = true; activatedFrame = Input.FrameNumber; hud.Modal = this;
	}
	internal void Close()
	{
		Active = false;
		if ( hud.Modal == this ) hud.Modal = null;
	}
	internal void Choose( UIStrings action )
	{
		if ( !Active || !items.Any( item => item.Action == action ) ) throw new InvalidOperationException( "Inactive lobby menu action" );
		if ( action == UIStrings.ResumeGame ) { Close(); return; }
		// 0x489fff uses ConfirmNewPlayer; 0x48a109 uses ConfirmQuit. Cancellation keeps the menu.
		confirmation.Show( Localization.Parse( action == UIStrings.SelectNewPlayer ? "#ConfirmNewPlayer" : "#ConfirmQuit" ), () =>
		{
			Close();
			if ( action == UIStrings.SelectNewPlayer ) level.ReturnToPlayers();
			else Window.Current.SdlWindow.Close();
		} );
	}
	protected override void OnUpdate()
	{
		if ( !Active || activatedFrame == Input.FrameNumber ) return;
		if ( Input.Pressed( Key.Escape ) ) { Close(); return; }
		if ( Input.Pressed( Key.Up ) ) focused = (focused + items.Count - 1) % items.Count;
		if ( Input.Pressed( Key.Down ) ) focused = (focused + 1) % items.Count;
		for ( int i = 0; i < items.Count; i++ )
			if ( LobbyNamingPanel.Hit( items[i].Bounds ) )
			{
				focused = i;
				if ( Input.LeftPressed ) { Choose( items[i].Action ); return; }
			}
		if ( Input.Pressed( Key.Enter ) ) Choose( items[focused].Action );
	}
	protected override void OnRender()
	{
		if ( !Active ) return;
		for ( int i = 0; i < items.Count; i++ )
		{
			// 0x489390 selects cyan (0,175,190); 0x490cd0 creates the default black text.
			items[i].Text.Color = i == focused ? 0x00afbeffu : 0x000000ffu;
			items[i].Text.Draw();
		}
	}
}
