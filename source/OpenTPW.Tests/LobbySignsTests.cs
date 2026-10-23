using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace OpenTPW.Tests;

[TestClass]
public class LobbySignsTests
{
	[TestMethod]
	public void RetailLobbySignsDecodeBackgrounds()
	{
		GameData.Require();
		foreach ( string name in new[] { "Jun_isle", "Hal_isle", "Fan_gate", "Spa_gate" } )
		{
			var image = LobbySigns.DecodeBackground( FileSystem.ReadAllBytes( $"lobby/terrain/{name}.sgn" ) );
			Assert.IsTrue( image.Width >= 128 && image.Height >= 64, name );
			Assert.AreEqual( image.Width * image.Height * 4, image.Data.Length, name );
			bool varied = false;
			for ( int i = 4; i < image.Data.Length; i += 4 )
				if ( image.Data[i] != image.Data[0] ) { varied = true; break; }
			Assert.IsTrue( varied, name );
		}
	}

	[TestMethod]
	public void InvalidSignIsRejected()
	{
		Assert.ThrowsException<InvalidDataException>( () => LobbySigns.DecodeBackground( new byte[64] ) );
	}
	[TestMethod]
	public void RetailSignLayoutAndLocalizedLines()
	{
		GameData.Require();
		foreach ( string name in new[] { "Jun_isle", "Hal_isle", "Fan_gate", "Spa_gate" } )
		{
			var sign = LobbySigns.Decode( FileSystem.ReadAllBytes( $"lobby/terrain/{name}.sgn" ) );
			Assert.AreEqual( 2, sign.Lines.Length );
			Assert.AreEqual( 256, sign.Background.Width );
			Assert.AreEqual( 128, sign.Background.Height );
			foreach ( var line in sign.Lines )
			{
				Assert.IsTrue( line.Spacing > 0 );
				Assert.IsTrue( line.FontPath.EndsWith( ".ttf", System.StringComparison.OrdinalIgnoreCase ) || line.FontPath.EndsWith( ".ttc", System.StringComparison.OrdinalIgnoreCase ) );
				using var font = FileSystem.OpenRead( $"fonts/{line.FontPath}" );
				Assert.IsNotNull( font );
			}
			var text = LobbySigns.LocalizedLines( name );
			Assert.IsFalse( string.IsNullOrEmpty( text.First ) );
			Assert.IsFalse( string.IsNullOrEmpty( text.Second ) );
		}
	}

	[TestMethod]
	public void SignHalvesPreserveVisualTopFirstRows()
	{
		var bytes = new byte[256 * 128 * 4];
		bytes[0] = 11; bytes[128 * 4] = 22;
		bytes[127 * 256 * 4] = 33; bytes[(127 * 256 + 128) * 4] = 44;
		var image = new TextureData( 256, 128, bytes );
		var left = LobbySigns.Split( image, 0 );
		var right = LobbySigns.Split( image, 1 );
		Assert.AreEqual( (byte)11, left.Data[0] );
		Assert.AreEqual( (byte)22, right.Data[0] );
		Assert.AreEqual( (byte)33, left.Data[127 * 128 * 4] );
		Assert.AreEqual( (byte)44, right.Data[127 * 128 * 4] );
	}

	[TestMethod]
	public void ShippedFontsComposeBothLocalizedLines()
	{
		GameData.Require();
		foreach ( string name in new[] { "Jun_isle", "Hal_isle", "Fan_gate", "Spa_gate" } )
		{
			byte[] data = FileSystem.ReadAllBytes( $"lobby/terrain/{name}.sgn" );
			var text = LobbySigns.LocalizedLines( name );
			var image = LobbySigns.Compose( data, text.First, text.Second );
			var firstOnly = LobbySigns.Compose( data, text.First, "" );
			var secondOnly = LobbySigns.Compose( data, "", text.Second );
			var background = LobbySigns.DecodeBackground( data );
			int changes = 0, firstChanges = 0, secondChanges = 0;
			for ( int i = 0; i < image.Data.Length; i++ )
			{
				if ( image.Data[i] != background.Data[i] ) changes++;
				if ( image.Data[i] != secondOnly.Data[i] ) firstChanges++;
				if ( image.Data[i] != firstOnly.Data[i] ) secondChanges++;
			}
			Assert.IsTrue( changes > 100, name );
			Assert.IsTrue( firstChanges > 100, $"{name}: first localized line is missing" );
			Assert.IsTrue( secondChanges > 100, $"{name}: second localized line is missing" );
		}
	}

}
