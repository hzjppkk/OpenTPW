using OpenTPW.UI;
using Veldrid;

namespace OpenTPW;

/// <summary>Login name controls from the table at 0x74fa58; coordinates remain in retail UI units.</summary>
internal sealed class LobbyNamingPanel : Panel
{
	private readonly LobbyPlayerFlow flow;
	private readonly List<Panel> controls = new();
	private readonly LobbyUiText name;
	private readonly LobbyUiText error;
	private readonly Action<byte> confirm;
	private readonly Action cancel;
	private readonly LobbyUiModel instant;
	private readonly LobbyUiModel simulation;
	private readonly LobbyUiModel okay;
	private readonly LobbyUiModel exit;
	private readonly LobbyUiModel backdrop;
	public byte CreationMode { get; private set; } = 1;
	public bool Active { get; set; }

	internal static Rectangle Box( float left, float top, float right, float bottom ) => new( left, top, right - left, bottom - top );
	internal static bool Hit( Rectangle units )
	{
		var point = Input.Mouse.Position;
		float x = point.X * 2048 / Screen.Size.X, y = point.Y * 1536 / Screen.Size.Y;
		return x >= units.X && x < units.X + units.Width && y >= units.Y && y < units.Y + units.Height;
	}

	public LobbyNamingPanel( LobbyPlayerFlow flow, Action<byte> confirm, Action cancel )
	{
		this.flow = flow;
		this.confirm = confirm;
		this.cancel = cancel;
		// 0x47cff0 creates the modal underlay separately from the authored dialog table.
		backdrop = new LobbyUiModel( "LOLIGHT", Box( 0, 0, 2047, 1535 ) );
		// Table 0x74fa58 binds 0xcd93131e; 0x48744c registers w_dialog_wave as "wdialogw".
		controls.Add( new LobbyUiModel( LobbyControlResources.ResolveModel( 0xcd93131e ), Box( 500, 233, 1516, 830 ) ) );
		controls.Add( new LobbyUiModel( "!f_plain", Box( 735, 377, 1235, 597 ) ) );
		controls.Add( new LobbyUiModel( "!frame", Box( 593, 616, 1216, 698 ) ) );
		controls.Add( instant = new LobbyUiModel( "b_ltoggle", Box( 764, 405, 1206, 476 ) ) );
		controls.Add( simulation = new LobbyUiModel( "b_ltoggle", Box( 764, 498, 1206, 569 ) ) );
		controls.Add( exit = new LobbyUiModel( "b_exit", Box( 1387, 613, 1470, 696 ) ) );
		controls.Add( okay = new LobbyUiModel( "b_okay", Box( 1376, 524, 1460, 608 ) ) );
		controls.Add( new LobbyUiText( Localization.Parse( "#EnterName" ), Box( 655, 285, 1315, 365 ), "TITLEMED" ) );
		controls.Add( new LobbyUiText( Localization.Parse( "#InstantAction" ), Box( 764, 405, 1206, 476 ) ) );
		controls.Add( new LobbyUiText( Localization.Parse( "#FullSimulation" ), Box( 764, 498, 1206, 569 ) ) );
		name = new LobbyUiText( "", Box( 623, 632, 1186, 677 ) );
		controls.Add( name );
		error = new LobbyUiText( "", Box( 500, 720, 1516, 830 ) );
		controls.Add( error );
	}
	public void ShowError( string message ) => error.Text = message;
	protected override void OnUpdate()
	{
		if ( !Active ) return;
		if ( Input.Pressed( Key.Escape ) ) { cancel(); return; }
		if ( Input.Pressed( Key.BackSpace ) && flow.Name.Length != 0 ) flow.Name = flow.Name[..^1];
		flow.Name += Input.Text;
		name.Text = flow.Name;
		// 0x662ca0: disabled=1, hover=2, hover+down=3, held=4, selected/down=5.
		int State( Rectangle bounds, bool selected = false, bool disabled = false ) => disabled ? 1 :
			Hit( bounds ) ? (selected || Input.Mouse.Left ? 3 : 2) : selected ? 5 : 0;
		instant.State = State( Box( 764, 405, 1206, 476 ), CreationMode == 1 );
		simulation.State = State( Box( 764, 498, 1206, 569 ), CreationMode == 0 );
		okay.State = State( Box( 1376, 524, 1460, 608 ), disabled: !flow.CanConfirm );
		exit.State = State( Box( 1387, 613, 1470, 696 ) );
		if ( Input.LeftPressed )
		{
			if ( Hit( Box( 764, 405, 1206, 476 ) ) ) CreationMode = 1;
			else if ( Hit( Box( 764, 498, 1206, 569 ) ) ) CreationMode = 0;
			else if ( Hit( Box( 1387, 613, 1470, 696 ) ) ) { cancel(); return; }
		}
		if ( flow.CanConfirm && (Input.Pressed( Key.Enter ) || Input.LeftPressed && Hit( Box( 1376, 524, 1460, 608 ) )) ) confirm( CreationMode );
	}
	protected override void OnRender()
	{
		if ( !Active ) return;
		backdrop.Draw();
		foreach ( var control in controls ) control.Draw();
	}
}
