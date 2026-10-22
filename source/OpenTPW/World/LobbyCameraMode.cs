using System.Text.Json;

namespace OpenTPW;

public sealed class LobbyCameraMode : CameraMode
{
	public static LobbyConfig Config { get; private set; } = null!;
	public static int SelectedIsland { get; private set; }
	private static float angle;
	private Vector3 trackedTarget;
	private bool initialized;
	private static FixedView? fixedView;
	private bool reportedFixedView;
	private sealed record FixedView( int FormatVersion, string CoordinateSystem, int IslandId,
		float[] Position, float[] Target, float VerticalFieldOfViewDegrees );
	public static LobbyIslandConfig CurrentIsland => Config.Islands.First( x => x.Id == SelectedIsland );

	public static void Configure( LobbyConfig config, int island = 0 )
	{
		Config = config;
		angle = 0;
		fixedView = ReadFixedView();
		SelectIsland( fixedView?.IslandId ?? island );
	}

	private static FixedView? ReadFixedView()
	{
		var path = Environment.GetEnvironmentVariable( "OPENTPW_LOBBY_VIEW" );
		if ( string.IsNullOrWhiteSpace( path ) ) return null;
		var view = JsonSerializer.Deserialize<FixedView>( File.ReadAllText( path ),
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true } );
		if ( view == null || view.FormatVersion != 1 || view.CoordinateSystem != "retail-y-up" )
			throw new InvalidDataException( "Fixed lobby view requires formatVersion 1 and coordinateSystem retail-y-up." );
		if ( !ValidVector( view.Position ) || !ValidVector( view.Target ) ||
			!float.IsFinite( view.VerticalFieldOfViewDegrees ) || view.VerticalFieldOfViewDegrees <= 0 || view.VerticalFieldOfViewDegrees >= 180 )
			throw new InvalidDataException( "Fixed lobby view requires finite position/target vectors and a vertical field of view between 0 and 180 degrees." );
		var direction = FromRetail( view.Target ) - FromRetail( view.Position );
		if ( !float.IsFinite( direction.LengthSquared ) || direction.LengthSquared <= 0 ||
			!Config.Islands.Any( item => item.Id == view.IslandId ) )
			throw new InvalidDataException( "Fixed lobby view requires a known island and distinct position/target." );
		return view;
	}

	private static bool ValidVector( float[]? vector ) => vector is { Length: 3 } && vector.All( float.IsFinite );
	// LobbyModel maps retail (x,y,z) to OpenTPW (x,z,y); 0x5d3eb0 uses retail Y as up.
	private static Vector3 FromRetail( float[] vector ) => new( vector[0], vector[2], vector[1] );

	public static void SelectIsland( int id )
	{
		var island = Config.Islands.First( x => x.Id == id );
		SelectedIsland = id;
	}

	public override void Update()
	{
		if ( Config == null ) return;
		if ( fixedView != null )
		{
			Position = FromRetail( fixedView.Position );
			trackedTarget = FromRetail( fixedView.Target );
			Rotation = Rotation.LookAt( trackedTarget - Position );
			FieldOfView = fixedView.VerticalFieldOfViewDegrees;
			if ( !reportedFixedView )
			{
				Log.Info( $"Fixed lobby view active: island {fixedView.IslandId}" );
				reportedFixedView = true;
			}
			return;
		}
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
