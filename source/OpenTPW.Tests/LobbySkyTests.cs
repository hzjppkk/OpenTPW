using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using StbImageSharp;
using N = System.Numerics;

namespace OpenTPW.Tests;

[TestClass]
public class LobbySkyTests
{
	[TestMethod]
	public void EveryLobbySkyTextureLoadsAndPresetsChooseRetailLayerCounts()
	{
		GameData.Require();
		foreach ( var name in new[] { "sky.tga", "sky_rgb.tga", "sky_cyl.tga" } )
		{
			var image = ImageResult.FromMemory( FileSystem.ReadAllBytes( LobbySkyGeometry.Root + name ), ColorComponents.RedGreenBlueAlpha );
			Assert.AreEqual( 256, image.Width );
			Assert.AreEqual( 256, image.Height );
		}
		CollectionAssert.AreEqual( new[] { 1, 2, 4 }, new[] { "low", "med", "high" }.Select( x => LobbyQuality.Load( x ).SkyLayers ).ToArray() );
	}

	[TestMethod]
	public void SkyTgasKeepRetailBottomOriginForUvsAndColourSampling()
	{
		GameData.Require();
		var image = LobbySkyGeometry.ReadTexture( "sky_cyl.tga" );
		CollectionAssert.AreEqual( new byte[] { 63, 208, 255, 255 }, image.Data.Take( 4 ).ToArray() );
		int lastRow = (image.Height - 1) * image.Width * 4;
		CollectionAssert.AreEqual( new byte[] { 111, 220, 255, 24 }, image.Data.Skip( lastRow ).Take( 4 ).ToArray() );
	}

	[TestMethod]
	public void RetailDomeAndHorizonGeometryKeepTheirDimensionsAndSeams()
	{
		GameData.Require();
		var sky = LobbySkyGeometry.Load();
		var clouds = sky.CloudVertices( 0 );
		Assert.AreEqual( 256, clouds.Length );
		Assert.AreEqual( -1200f, clouds[0].Position.X );
		Assert.AreEqual( -1200f, clouds[0].Position.Y );
		Assert.AreEqual( 180 - MathF.Sqrt( 1200 * 1200 * 2 ) * 0.2f, clouds[0].Position.Z, 0.0001f );
		Assert.IsTrue( sky.CloudIndices.Length > 0 );
		Assert.IsTrue( sky.CloudIndices.All( index => index < 256 && clouds[index].Position.Z > -36 ) );
		var horizon = sky.HorizonVertices();
		Assert.AreEqual( 72, horizon.Length );
		Assert.AreEqual( 288, LobbySkyGeometry.HorizonIndices().Length );
		Assert.AreEqual( new Vector3( 720, 0, 36 ), horizon[0].Position );
		Assert.IsTrue( (horizon[32].Position - horizon[36].Position).Length < 0.001f );
		CollectionAssert.AreEqual( new uint[] { 207, 151, 95, 39 }, Enumerable.Range( 0, 4 ).Select( layer => sky.CloudVertices( layer )[0].MatFlags ).ToArray() );
	}

	[TestMethod]
	public void FixedPointColoursPreserveTheRetailBlueSourceForRedAndFrameSmoothing()
	{
		GameData.Require();
		foreach ( var island in LobbyConfig.Load().Islands )
		{
			var state = new LobbySkyColour();
			state.SetTarget( island.SkyColour );
			state.Step( false );
			Assert.AreEqual( 0, state.Red );
			state.Step( true );
			Assert.AreEqual( (int)island.SkyColour.Z << 12, state.Red );
			Assert.AreEqual( (int)island.SkyColour.Y << 12, state.Green );
			Assert.AreEqual( state.Red, state.Blue );
			for ( int frame = 0; frame < 500; frame++ ) state.Step( true );
			Assert.AreEqual( (int)island.SkyColour.Z, (state.Red + 16) >> 16 );
		}
		var transition = new LobbySkyColour();
		transition.SetTarget( new N.Vector3( 255, 255, 255 ) );
		transition.Step( true );
		int previous = transition.Red;
		transition.SetTarget( N.Vector3.Zero );
		transition.Step( true );
		Assert.AreEqual( previous + ((-previous) >> 4), transition.Red );
	}

	[TestMethod]
	public void IslandColoursOnlyReplaceTheFirstCloudLayer()
	{
		GameData.Require();
		var sky = LobbySkyGeometry.Load();
		var tint = new Vector3( 0.2f, 0.3f, 0.4f );
		Assert.AreEqual( tint, sky.CloudVertices( 0, 0, tint )[17].Normal );
		Assert.AreEqual( sky.CloudVertices( 1 )[17].Normal, sky.CloudVertices( 1, 0, tint )[17].Normal );
		Assert.AreNotEqual( tint, sky.CloudVertices( 1, 0, tint )[17].Normal );
	}

	[TestMethod]
	public void CloudMotionUsesTheRenderSecondsClockAndWrapsWithoutMovingGeometry()
	{
		GameData.Require();
		var sky = LobbySkyGeometry.Load();
		var before = sky.CloudVertices( 0 );
		sky.Advance( 0.5f );
		var after = sky.CloudVertices( 0 );
		Assert.AreEqual( before[0].Position, after[0].Position );
		Assert.AreEqual( -0.01f, after[0].TexCoords.X, 0.000001f );
		Assert.AreEqual( 0.001125f, after[0].TexCoords.Y, 0.000001f );
	}

	[TestMethod]
	public void CloudLayersAboveTheFirstShareOneRandomGreyPerVertex()
	{
		GameData.Require();
		var sky = LobbySkyGeometry.Load();
		var one = sky.CloudVertices( 1 );
		for ( int i = 0; i < one.Length; i++ )
		{
			var grey = one[i].Normal;
			Assert.AreEqual( grey.X, grey.Y ); Assert.AreEqual( grey.X, grey.Z );
			Assert.IsTrue( grey.X >= 0x80 / 255f && grey.X <= 1, $"vertex {i}: {grey.X}" );
			Assert.AreEqual( grey, sky.CloudVertices( 2 )[i].Normal );
			Assert.AreEqual( grey, sky.CloudVertices( 3 )[i].Normal );
		}
	}
	[TestMethod]
	public void RetailSkyReplaySetsOffsetsAndGreysAndRejectsMismatchedTables()
	{
		GameData.Require();
		var sky = LobbySkyGeometry.Load();
		var greys = Enumerable.Range( 0, 4 ).Select( _ => Enumerable.Range( 0, 256 ).Select( i => 0x80 | (i & 0x7f) ).ToArray() ).ToArray();
		var offsets = new[] { new[] { 0.1f, 0.2f }, new[] { 0.3f, 0.4f }, new[] { 0.5f, 0.6f }, new[] { 0.25f, 0.5f } };
		sky.Replay( offsets, greys );
		// Layer 3 keeps both coefficients positive (0x5863c0), so vertex (0,0) carries the raw offsets.
		Assert.AreEqual( new Vector2( 0.25f, 0.5f ), sky.CloudVertices( 3 )[0].TexCoords );
		Assert.AreEqual( new Vector3( (0x80 | 37) / 255f ), sky.CloudVertices( 2 )[37].Normal );
		greys[3][5] ^= 1;
		Assert.ThrowsException<System.IO.InvalidDataException>( () => sky.Replay( offsets, greys ) );
		Assert.ThrowsException<System.IO.InvalidDataException>( () => sky.Replay( offsets.Take( 3 ).ToArray(), greys ) );
	}
}
