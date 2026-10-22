namespace OpenTPW;

/// <summary>Flying actors use the retail clock and target-seeking update.</summary>
public sealed class LobbyFlock : Entity
{
	private sealed class Actor
	{
		public LobbyModel Model = null!;
		public Vector3 Direction;
		public Vector3 Target;
	}

	private readonly List<Actor> actors = new();
	private readonly LobbyFlyingMeshConfig config;
	private readonly Vector3 center;
	private readonly Vector3 extents;
	private uint random;
	private readonly LobbyQuality quality;
	public int QualityPercent { get; set; }
	public int ActorCount => actors.Count;

	public LobbyFlock( LobbyFlyingMeshConfig config, Vector3 islandPosition, int qualityPercent, uint seed, LobbyQuality? quality = null )
	{
		this.config = config;
		this.quality = quality ?? LobbyQuality.Load();
		center = islandPosition + new Vector3( 0, 0, 50 );
		extents = new Vector3( config.Extents.X, config.Extents.Z, config.Extents.Y );
		QualityPercent = qualityPercent;
		random = seed;
	}

	private uint Next()
	{
		random = unchecked( random * 0x19660d + 0x3c6ef35f );
		random = (random >> 13) | (random << 19);
		return random;
	}
	private float Unit() => (Next() & 0x3fffffff) * (1f / 1073741824f);
	private Vector3 Point() => center + new Vector3( Unit() - 0.5f, Unit() - 0.5f, Unit() - 0.5f ) * extents;

	protected override void OnUpdate()
	{
		int count = config.CountAtQuality( QualityPercent );
		while ( actors.Count > count )
		{
			var actor = actors[^1];
			actor.Model.Delete();
			All.Remove( actor.Model );
			actors.RemoveAt( actors.Count - 1 );
		}
		while ( actors.Count < count )
		{
			var position = Point();
			var target = Point();
			// FUN_005d4900 -> FUN_005d3430( 0, 1 ): slot M, segment 0, looped.
			var animation = AnimationFiles.Resolve( config.ModelPath, AnimationSlot.M, 0 );
			actors.Add( new Actor
			{
				Model = new LobbyModel( position, AnimationFiles.ResolveModel( config.ModelPath ),
					$"{config.ResourceDirectory}/textures", animation, quality: quality ),
				Direction = (target - position).Normal,
				Target = target
			} );
			if ( (Next() & 7) == 0 ) break;
		}
		float dt = RetailTime.FrameTicks;
		foreach ( var actor in actors )
		{
			var ideal = (actor.Target - actor.Model.Position).Normal;
			actor.Direction = (actor.Direction + (ideal - actor.Direction) * dt * 0.1f).Normal;
			actor.Model.Position += actor.Direction * config.Speed * dt;
			actor.Model.Rotation = Facing( actor.Direction );
			if ( (actor.Target - actor.Model.Position).LengthSquared < 500 ) actor.Target = Point();
		}
	}

	/// <summary>FUN_005d3200: right = normalize(up cross direction), up = direction cross right.</summary>
	public static System.Numerics.Quaternion Facing( Vector3 direction )
	{
		var forward = direction.Normal;
		if ( forward.LengthSquared == 0 ) return System.Numerics.Quaternion.Identity;
		var right = new Vector3( forward.Y, -forward.X, 0 ).Normal;
		if ( right.LengthSquared == 0 ) right = new Vector3( 1, 0, 0 );
		var up = Vector3.Cross( right, forward ).Normal;
		return System.Numerics.Quaternion.CreateFromRotationMatrix( new System.Numerics.Matrix4x4(
			right.X, right.Y, right.Z, 0, forward.X, forward.Y, forward.Z, 0,
			up.X, up.Y, up.Z, 0, 0, 0, 0, 1 ) );
	}
}
