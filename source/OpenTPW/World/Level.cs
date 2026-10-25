using OpenTPW.UI;

namespace OpenTPW;

public class Level
{
	internal static Level Current { get; set; }

	public RootPanel Hud { get; set; }
	public Sun SunLight { get; set; }
	public LobbyConfig? Lobby { get; private set; }
	public Sky? EnvironmentSky { get; private set; }
	public AudioSystem? Audio { get; }
	public LobbyAudio? LobbySound { get; }
	public LobbyAdvisor? Advisor { get; private set; }
	internal LobbyPlayerPanel? PlayerMenu { get; set; }
	internal LobbyNavigation? Navigation { get; private set; }
	internal LobbyIslandPanel? IslandMenu { get; private set; }
	internal LobbyGlobePanel? GlobeMenu { get; private set; }
	public bool FogEnabled => GlobeMenu?.Active != true;
	public event Action<LobbyPlayerProfile, LobbyIslandConfig>? ParkEntryRequested;
	private LobbyPlayerProfile? selectedPlayer;
	private LobbyKeyParticlePanel? keyEffects;

	public SettingsFile Global { get; private init; }

	public Level( string levelName, AudioSystem? audio = null )
	{
		Audio = audio;
		LobbySound = audio == null ? null : new LobbyAudio( audio );
		Global = new SettingsFile( $"/levels/{levelName}/global.sam" );
		Current = this;

		SetupEntities();
		SetupHud();
	}

	private void SetupEntities()
	{
		SunLight = new Sun() { Position = new( 0, 100, 100 ) };

		// 0x5d34e0 -> 0x44f710 loads the authored lobby Base, including both sea meshes and their UVs.
		_ = new LobbyModel( Vector3.Zero, "lobby/terrain/Base.md2", "lobby/terrain/textures" );
		var sky = new Sky();
		EnvironmentSky = sky;
		Lobby = LobbyConfig.Load();
		var lobbyRandom = new LobbyRandom( unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()) );
		LobbyCameraMode.Configure( Lobby, lobbyRandom );
		_ = new LobbyWeather( sky, () => LobbyCameraMode.CurrentIsland, Lobby, lobbyRandom, Audio );
		var islands = new Dictionary<int, LobbyIsland>();
		foreach ( var island in Lobby.Islands )
		{
			var center = Lobby.CameraPositions[island.Id];
			islands.Add( island.Id, new LobbyIsland( new Vector3( center.X, center.Y, 0 ), island, null, lobbyRandom ) );
		}
		LobbyCameraMode.GateAnimationRequested += ( id, entering ) =>
		{
			if ( entering ) islands[id].Gate.StartEntering();
			else islands[id].Gate.StartLeaving();
		};

		Camera.SetCameraMode<LobbyCameraMode>();
	}

	private void SetupHud()
	{
		Hud = new();

		var layout = new LobbyLayout() { Hud = Hud };
		layout.OnInit();
		Navigation = new LobbyNavigation( Lobby! );
		Navigation.IslandSelected += LobbyCameraMode.SelectIsland;
		Navigation.EntryRequested += () =>
		{
			// 0x5dcd00 -> 0x4b6a30 plays global lobby effect 4 when leaving island selection.
			Audio?.Play( "global/sound", "cat_globallobbysfx", 4 );
			keyEffects?.EnterIsland();
			LobbyCameraMode.BeginEntering();
		};
		Navigation.EntryCancelled += LobbyCameraMode.CancelEntering;
		IslandMenu = Hud.AddChild( new LobbyIslandPanel( Navigation ) );
		GlobeMenu = Hud.AddChild( new LobbyGlobePanel( this ) );
		IslandMenu.GlobeRequested += () => GlobeMenu.Show( LobbyGlobeMode.Visit );
		PlayerMenu!.PlayerSelected += ( player, data ) =>
		{
			selectedPlayer = player;
			Navigation.Bind( data );
			IslandMenu.Activate();
		};
		var confirmation = Hud.AddChild( new LobbyConfirmPanel( Hud ) );
		PlayerMenu.QuitRequested += () => confirmation.Show( Localization.Parse( "#ConfirmQuit" ), () => Window.Current.SdlWindow.Close() );
		LobbyCameraMode.IslandEntered += id =>
		{
			if ( ParkEntryRequested != null ) ParkEntryRequested( selectedPlayer!, Lobby!.Islands.First( island => island.Id == id ) );
			else
			{
				Navigation.CancelEntry();
				IslandMenu.Failure = "Park loading is not available in this build.";
				Log.Warning( $"Park entry has no handler: {selectedPlayer!.Directory}, {Navigation.Island.ThemeName}" );
			}
		};
		if ( Audio != null )
		{
			Advisor = new LobbyAdvisor( Audio );
			Advisor.ResponseCue += response =>
			{
				if ( response == 0x189 )
				{
					Audio.Play( "global/sound", "cat_globallobbysfx", 0xc6 );
					keyEffects?.IntroductionKey();
				}
			};
			Advisor.Welcome( LobbyPlayerProfiles.Read().Any( player => player != null ) );
			Hud.AddChild( new LobbyAdvisorPanel( Advisor ) );
		}

		keyEffects = Hud.AddChild( new LobbyKeyParticlePanel() );
		Hud.AddChild( new LobbyFadePanel() );
		Hud.AddChild( new Cursor() );
	}

	public void Update()
	{
		Audio?.Update();
		// 0x5db430 updates the camera before calculating distance-controlled weather.
		Camera.Update();
		if ( GlobeMenu?.Active != true ) LobbySound?.SetIsland( LobbyCameraMode.CurrentIsland.ThemeName );
		// Flocks can add model entities while replenishing their actor list.
		foreach ( var entity in Entity.All.ToArray() )
			if ( GlobeMenu?.Active != true || entity == Hud || GlobeMenu.Owns( entity ) ) entity.Update();
	}

	public void Render()
	{
		// Retail registers screen-space UI models separately (TP.exe 0x474eb0 ->
		// 0x45a960, list mask 0x20; screen transform at 0x474f10).
		// Keep the OpenTPW HUD out of world creation order as well.
		foreach ( var entity in Entity.InRenderOrder( Entity.All ) )
		{
			if ( GlobeMenu != null && entity != Hud && (GlobeMenu.Active != GlobeMenu.Owns( entity )) ) continue;
			// Material.UI disables depth testing and writes, so world depth cannot hide the HUD.
			entity.Render();
		}
	}
}
