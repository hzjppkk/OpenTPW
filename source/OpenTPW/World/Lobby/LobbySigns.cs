using System.Text;

namespace OpenTPW;

/// <summary>Composes the lobby's generated sign materials from the background carried by SGN v101.</summary>
public static class LobbySigns
{
	private static readonly Dictionary<string, Texture?> cache = new( StringComparer.OrdinalIgnoreCase );

	public static Texture? Resolve( string modelPath, string materialName, string title )
	{
		if ( !materialName.Equals( "sign1", StringComparison.OrdinalIgnoreCase ) &&
			!materialName.Equals( "sign2", StringComparison.OrdinalIgnoreCase ) ) return null;
		var path = Path.ChangeExtension( modelPath, ".sgn" );
		if ( cache.TryGetValue( path, out var found ) ) return found;
		using var input = FileSystem.OpenRead( path );
		if ( input == null ) return cache[path] = null;
		using var buffer = new MemoryStream();
		input.CopyTo( buffer );
		var image = DecodeBackground( buffer.ToArray() );
		DrawTitle( image.Data, image.Width, image.Height, title );
		return cache[path] = new Texture( image.Data, image.Width, image.Height );
	}

	/// <summary>SGN keeps float quantization factors rather than the short WCT fields.</summary>
	public static TextureData DecodeBackground( byte[] data )
	{
		if ( data.Length < 4 || BitConverter.ToInt32( data ) != 101 )
			throw new InvalidDataException( "Unsupported SGN version" );
		// Embedded images follow variable font/layout records and two raw edge strips.
		// Validate the complete compressed image descriptor rather than assuming a fixed offset.
		for ( int offset = 40; offset + 28 <= data.Length; offset++ )
		{
			if ( data[offset] != 'B' || data[offset + 1] != 'I' || data[offset + 2] != 'L' || data[offset + 3] != 'Z' ) continue;
			int header = offset - 36;
			int width = BitConverter.ToInt32( data, header ), height = BitConverter.ToInt32( data, header + 4 );
			int colorSize = BitConverter.ToInt32( data, header + 28 ), alphaSize = BitConverter.ToInt32( data, header + 32 );
			if ( width < 1 || width > 4096 || height < 1 || height > 4096 ||
				BitConverter.ToInt32( data, header + 8 ) != 4 || colorSize < 28 || alphaSize < 0 ||
				(long)offset + colorSize + alphaSize > data.Length ) continue;
			using var wct = new MemoryStream();
			using ( var writer = new BinaryWriter( wct, Encoding.ASCII, true ) )
			{
				writer.Write( (byte)0 ); // SGN backgrounds use the half-scale wavelet layout.
				writer.Write( (byte)(alphaSize > 0 ? 1 : 0) );
				writer.Write( (byte)32 ); writer.Write( (byte)1 );
				writer.Write( (short)width ); writer.Write( (short)height );
				for ( int channel = 0; channel < 4; channel++ )
				{
					float factor = BitConverter.ToSingle( data, header + 12 + channel * 4 );
					writer.Write( checked((short)MathF.Round( channel == 0 ? (factor - 1) * 2 : channel == 3 ? factor - 1 : (factor - 1) * 4 )) );
				}
				writer.Write( colorSize ); writer.Write( alphaSize ); writer.Write( 0 );
				writer.Write( data, offset, colorSize + alphaSize );
			}
			wct.Position = 0;
			return new TextureFile( wct ).Data;
		}
		throw new InvalidDataException( "SGN has no supported background image" );
	}

	private static void DrawTitle( byte[] rgba, int width, int height, string title )
	{
		// SGN stores a TrueType face; the portable path uses the shipped BF4 atlas.
		// Exact retail two-line positioning and localization selection remain unverified.
		var languages = FileSystem.GetDirectories( "Language" ).OrderBy( x => x, StringComparer.OrdinalIgnoreCase );
		foreach ( var language in languages )
		{
			var path = FileSystem.GetFiles( language ).FirstOrDefault( x => Path.GetFileName( x ).Equals( "GAME12AA.bf4", StringComparison.OrdinalIgnoreCase ) );
			if ( path == null ) continue;
			var font = new FontFile( path );
			var text = font.RenderString( title, 0xFFF5E7FF );
			float scale = Math.Min( (width * 0.85f) / text.Width, (height * 0.45f) / text.Height );
			int dw = Math.Max( 1, (int)(text.Width * scale) ), dh = Math.Max( 1, (int)(text.Height * scale) );
			for ( int y = 0; y < dh; y++ )
			for ( int x = 0; x < dw; x++ )
			{
				int source = ((int)(y / scale) * text.Width + (int)(x / scale)) * 4;
				int target = (((height - dh) / 2 + y) * width + (width - dw) / 2 + x) * 4;
				int a = text.Data[source + 3];
				for ( int c = 0; c < 3; c++ ) rgba[target + c] = (byte)((text.Data[source + c] * a + rgba[target + c] * (255 - a)) / 255);
			}
			return;
		}
	}
}
