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
}
