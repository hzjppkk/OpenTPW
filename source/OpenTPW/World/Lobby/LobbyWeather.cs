namespace OpenTPW;

/// <summary>Selected-island sky and the lobby's distance-controlled rain / lightning.</summary>
public sealed class LobbyWeather : ModelEntity
{
	private readonly Sky sky;
	private readonly Func<LobbyIslandConfig> currentIsland;
	private readonly LobbyConfig? config;
	private readonly Vertex[] vertices = new Vertex[(400 + 24) * 6];
	private readonly Vector3[] drops = new Vector3[400];
	private readonly Vector3[] bolt = new Vector3[25];
	private uint random = 0x517CC1B7;
	private float flash;
	private int previousIsland = -1;
	public int RainAmount { get; private set; }

	public LobbyWeather( Sky sky, Func<LobbyIslandConfig> currentIsland, LobbyConfig? config = null )
	{
		this.sky = sky;
		this.currentIsland = currentIsland;
		this.config = config;
		var material = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader", MaterialFlags.DisableDepthWrite );
		material.Set( "Color", new Texture( [170, 200, 230, 180], 1, 1 ) );
		Model = new Model( vertices, material );
		for ( int i = 0; i < drops.Length; i++ ) drops[i] = new( Unit() * 180 - 90, Unit() * 180 - 90, Unit() * 130 - 35 );
		ApplyEnvironment();
	}

	private uint Next()
	{
		uint value = unchecked(random * 0x19660D + 0x3C6EF35F);
		return random = (value >> 13) | (value << 19);
	}
	private float Unit() => (Next() & 0x3FFFFFFF) * (1f / 1073741824f);

	protected override void OnUpdate() => ApplyEnvironment();

	private Vector3 IslandPosition( LobbyIslandConfig island )
	{
		if ( config != null && config.CameraPositions.TryGetValue( island.Id, out var point ) )
			return new( point.X, point.Y, island.Height );
		return Vector3.Zero;
	}

	private void ApplyEnvironment()
	{
		var island = currentIsland();
		if ( island == null ) return;
		if ( previousIsland != island.Id ) { flash = 0; previousIsland = island.Id; }
		flash = Math.Max( 0, flash - RetailTime.FrameSeconds );
		// FUN_005db430: mask the rotated LCG once per frame; 63 gives one chance in 64.
		if ( island.Lightning > 0 && (Next() & (uint)island.Lightning) == 1 )
		{
			var center = IslandPosition( island );
			var top = new Vector3( center.X + Unit() * 100 - 50, center.Y + Unit() * 100 - 50, 500 );
			var bottom = new Vector3( top.X + Unit() * 20 - 10, top.Y + Unit() * 20 - 10, 0 );
			for ( int i = 0; i < bolt.Length; i++ )
			{
				float t = i / (float)(bolt.Length - 1);
				bolt[i] = top * (1 - t) + bottom * t;
				if ( i > 0 && i < bolt.Length - 1 ) bolt[i] += new Vector3( Unit() * 8 - 4, Unit() * 8 - 4, 0 );
			}
			flash = 0.12f;
		}
		sky.SetIslandColour( island.SkyColour );
		float strength = flash > 0 ? flash / 0.12f * 0.65f : 0;
		sky.SetFlash( strength );

		var rainy = config?.Islands.Where( x => x.Rainy ).ToArray() ?? (island.Rainy ? new[] { island } : Array.Empty<LobbyIslandConfig>());
		double meanSquaredDistance = 0;
		foreach ( var wetIsland in rainy )
		{
			var d = Camera.Position - IslandPosition( wetIsland );
			meanSquaredDistance += d.X * d.X + d.Y * d.Y;
		}
		if ( rainy.Length > 0 ) meanSquaredDistance /= rainy.Length;
		double amount = rainy.Length == 0 ? 0 : 1e17 / Math.Pow( Math.Max( 1, meanSquaredDistance ), 3 );
		RainAmount = amount < 20 ? 0 : (int)Math.Min( 400, amount );
		Array.Clear( vertices );
		var right = Camera.Rotation.Right;
		for ( int i = 0; i < RainAmount; i++ )
		{
			drops[i].Z -= RetailTime.FrameSeconds * 110;
			if ( drops[i].Z < -35 ) drops[i].Z += 130;
			var p = Camera.Position + drops[i];
			Ribbon( i * 6, p, p + new Vector3( -0.3f, 0, 2.7f ), right * 0.04f );
		}
		if ( flash > 0 )
			for ( int i = 0; i < bolt.Length - 1; i++ ) Ribbon( (400 + i) * 6, bolt[i], bolt[i + 1], right * 0.6f );
		Model!.UpdateVertices( vertices );
	}

	private void Ribbon( int offset, Vector3 a, Vector3 b, Vector3 side )
	{
		vertices[offset] = new( a - side ); vertices[offset + 1] = new( b - side ); vertices[offset + 2] = new( b + side );
		vertices[offset + 3] = new( b + side ); vertices[offset + 4] = new( a + side ); vertices[offset + 5] = new( a - side );
	}
}
