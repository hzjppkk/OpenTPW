namespace OpenTPW;

public sealed class LobbyCameraMode : CameraMode
{
	public static LobbyConfig Config { get; private set; } = null!;
	public static int SelectedIsland { get; private set; }
	private static float angle;
	private Vector3 trackedTarget;
	private bool initialized;
	public static LobbyIslandConfig CurrentIsland => Config.Islands.First( x => x.Id == SelectedIsland );

	public static void Configure( LobbyConfig config, int island = 0 )
	{
		Config = config;
		angle = 0;
		SelectIsland( island );
	}

	public static void SelectIsland( int id )
	{
		var island = Config.Islands.First( x => x.Id == id );
		SelectedIsland = id;
	}

	public override void Update()
	{
		if ( Config == null ) return;
		var island = CurrentIsland;
		var center = Config.CameraPositions[island.Id];
		var target = new Vector3( center.X, center.Y, island.Height );
		angle += Config.SpinSpeed * Math.Clamp( Time.Delta * 10, 0, 5 );
		var desired = target + new Vector3( MathF.Sin( angle ) * Config.SpinRadius,
			MathF.Cos( angle ) * Config.SpinRadius, Config.VerticalOffset );
		float dt = Math.Clamp( Time.Delta * 10, 0, 5 );
		if ( !initialized ) { Position = desired; trackedTarget = target; initialized = true; }
		Position += (desired - Position) * Math.Min( 1, dt * 0.1f );
		trackedTarget += (target - trackedTarget) * Math.Min( 1, dt * 0.2f );
		Rotation = Rotation.LookAt( trackedTarget - Position );
		// Retail's viewport/frustum spans +/-0.5, rather than the modern +/-1 convention.
		FieldOfView = (2 * MathF.Atan( 0.5f * MathF.Tan( Config.IslandFovDegrees.DegreesToRadians() * 0.5f ) )).RadiansToDegrees();
	}
}
