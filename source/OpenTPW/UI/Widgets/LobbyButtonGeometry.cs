using System.Numerics;

namespace OpenTPW;

/// <summary>The model-space bounds and screen fit used by the lobby model control.</summary>
public sealed class LobbyButtonGeometry
{
	public ModelFile Model { get; }
	public System.Numerics.Vector3 Minimum { get; }
	public System.Numerics.Vector3 Maximum { get; }

	public LobbyButtonGeometry( byte[] data )
	{
		Model = new ModelFile( new MemoryStream( data ) );
		// TP.exe 0x4751d0 transforms the root's two stored bounds through its ancestors,
		// then 0x474f10 fits their X/Y extent to the control rectangle and flattens Z.
		int rootOffset = checked((int)BitConverter.ToUInt32( data, 0x78 ));
		int meshOffset = checked((int)BitConverter.ToUInt32( data, 0x70 ));
		int meshIndex = (rootOffset - meshOffset) / 160;
		if ( rootOffset < meshOffset || (rootOffset - meshOffset) % 160 != 0 || meshIndex >= Model.Meshes.Count )
			throw new InvalidDataException( "Lobby button root is not a mesh" );
		System.Numerics.Vector3 ReadPoint( int offset ) => new( BitConverter.ToSingle( data, offset ),
			BitConverter.ToSingle( data, offset + 4 ), BitConverter.ToSingle( data, offset + 8 ) );
		var world = Model.Meshes[meshIndex].WorldTransform;
		var a = System.Numerics.Vector3.Transform( ReadPoint( rootOffset + 0x78 ), world );
		var b = System.Numerics.Vector3.Transform( ReadPoint( rootOffset + 0x84 ), world );
		Minimum = System.Numerics.Vector3.Min( a, b );
		Maximum = System.Numerics.Vector3.Max( a, b );
		if ( Maximum.X <= Minimum.X || Maximum.Y <= Minimum.Y )
			throw new InvalidDataException( "Lobby button has empty screen bounds" );
	}

	/// <summary>Maps original model X/Y through the 2048x1536 control space into clip space.</summary>
	public Vector3 Project( System.Numerics.Vector3 point, Rectangle frame )
	{
		float x = frame.X + (point.X - Minimum.X) / (Maximum.X - Minimum.X) * frame.Width;
		float y = frame.Y + (Maximum.Y - point.Y) / (Maximum.Y - Minimum.Y) * frame.Height;
		return new Vector3( x / 320 - 1, 1 - y / 240, 0 );
	}
}
