using OpenTPW.UI;

namespace OpenTPW;

public class Level
{
	internal static Level Current { get; set; }

	public RootPanel Hud { get; set; }
	public Sun SunLight { get; set; }
	public LobbyConfig? Lobby { get; private set; }
	public Sky? EnvironmentSky { get; private set; }

	public SettingsFile Global { get; private init; }

	public Level( string levelName )
	{
		Global = new SettingsFile( $"/levels/{levelName}/global.sam" );
		Current = this;

		SetupEntities();
		SetupHud();
	}

	private void SetupEntities()
	{
		SunLight = new Sun() { Position = new( 0, 100, 100 ) };

		_ = new Water() { Scale = new Vector3( 10000f ) };
		var sky = new Sky();
		EnvironmentSky = sky;
		Lobby = LobbyConfig.Load();
		LobbyCameraMode.Configure( Lobby );
		_ = new LobbyWeather( sky, () => LobbyCameraMode.CurrentIsland, Lobby );
		foreach ( var island in Lobby.Islands )
		{
			var center = Lobby.CameraPositions[island.Id];
			_ = new LobbyIsland( new Vector3( center.X, center.Y, 0 ), island );
		}

		Camera.SetCameraMode<LobbyCameraMode>();
	}

	private void SetupHud()
	{
		Hud = new();

		var layout = new LobbyLayout() { Hud = Hud };
		layout.OnInit();

		Hud.AddChild( new Cursor() );
	}

	public void Update()
	{
		// Flocks can add model entities while replenishing their actor list.
		foreach ( var entity in Entity.All.ToArray() ) entity.Update();
		if ( Lobby != null ) LobbyNavigation.Draw( Lobby );
	}

	public void Render()
	{
		Camera.Update();

		Entity.All.ForEach( entity => entity.Render() );
	}
}
