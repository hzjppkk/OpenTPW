namespace OpenTPW;

/// <summary>0x579900 initializes the cube; 0x57c560 scrolls it and 0x57b5e0 wraps each point.</summary>
internal sealed class LobbyRainSimulation
{
	private readonly Vector3[] drops = new Vector3[1024];
	private float offset;
	internal LobbyRainSimulation( Func<int>? next = null )
	{
		next ??= RetailRandom.Next;
		float Coordinate() => ((next() & 0x3fff) - 8192f) * (1f / 2048);
		for ( int i = 0; i < drops.Length; i++ )
		{
			float x = Coordinate(), z = Coordinate(), y = Coordinate();
			drops[i] = new( x, y, z );
		}
	}
	internal void Advance( float seconds ) => offset = Wrap( offset - seconds * 5 );
	internal Vector3 Point( int index, Vector3 camera )
	{
		var point = drops[index];
		return new( Wrap( point.X - CameraOffset( camera.X ) ), Wrap( point.Y - CameraOffset( camera.Y ) ),
			Wrap( point.Z + CameraOffset( -camera.Z ) + offset ) );
	}
	internal static float CameraOffset( float coordinate ) => (((int)((coordinate * 0.1f + 4) * 16384)) & 0x1ffff) / 16384f - 4;
	internal static float Wrap( float value ) => value > 4 ? value - 8 : value < -4 ? value + 8 : value;
}
