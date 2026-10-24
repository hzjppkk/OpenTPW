using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class AudioTests
{
	[TestMethod]
	public void ReusedNAudioByteBufferDoesNotReplayOrAccumulatePreviousSamples()
	{
		var mixer = new SoundMixer();
		var output = new NAudio.Wave.SampleProviders.SampleToWaveProvider( mixer );
		var bytes = new byte[16];
		mixer.Play( new[] { 0.1f, 0.2f, 0.3f, 0.4f } );
		Assert.AreEqual( 16, output.Read( bytes, 0, 16 ) );
		Assert.AreEqual( 0.4f, BitConverter.ToSingle( bytes, 12 ), 0.00001f );
		Assert.AreEqual( 16, output.Read( bytes, 0, 16 ) );
		CollectionAssert.AreEqual( new byte[16], bytes, "NAudio reuses its byte-backed float overlay after the voice ends" );
		mixer.Play( new[] { 0.05f, 0.06f, 0.07f, 0.08f }, loop: true );
		for ( int read = 0; read < 10; read++ )
		{
			output.Read( bytes, 0, bytes.Length );
			Assert.AreEqual( 0.08f, BitConverter.ToSingle( bytes, 12 ), 0.00001f, "Previous output must not be added to the next mix" );
		}
	}
	private static SoundCategoryMap Read( string directory, string category ) => SoundCategoryMap.Parse(
		FileSystem.ReadAllBytes( $"{directory}/{category}BANK.map" ), FileSystem.ReadAllBytes( $"{directory}/{category}SFX.map" ) );

	[TestMethod]
	public void LobbyBanksBindEveryCandidateAndDecodeActualMpeg()
	{
		GameData.Require();
		var assets = new SoundAssets();
		foreach ( string theme in new[] { "global", "jungle", "hallow", "fantasy", "space" } )
		{
			string directory = theme == "global" ? "global/sound" : $"levels/{theme}/Sound";
			var map = Read( directory, theme == "global" ? "cat_globallobbysfx" : "cat_locallobbysfx" );
			Assert.AreEqual( 4, map.Sounds.Count );
			foreach ( var sample in map.Sounds.Values.SelectMany( sound => sound.Definitions ).SelectMany( definition => definition.Samples ) )
			{
				var file = assets.Sample( directory, map, sample );
				Assert.IsFalse( string.IsNullOrWhiteSpace( file.Name ) );
				Assert.IsTrue( assets.Pcm( file ).Length >= 2, file.Name );
			}
		}
	}

	[TestMethod]
	public void NativeThresholdGapsAndDefinitionFallbackArePreserved()
	{
		GameData.Require();
		var map = Read( "global/sound", "cat_globallobbysfx" );
		var thunder = map.Sounds[1].Definitions[0];
		CollectionAssert.AreEqual( new uint[] { 2, 3, 4, 1 }, thunder.Samples.Select( sample => sample.Id ).ToArray() );
		Assert.AreEqual( 2u, SoundSelection.Sample( thunder, 16383 )!.Id );
		Assert.AreEqual( 3u, SoundSelection.Sample( thunder, 16384 )!.Id );
		Assert.IsNull( SoundSelection.Sample( thunder, 65535 ) );
		var space = map.Sounds[3];
		Assert.AreSame( space.Definitions[0], SoundSelection.Definition( space, 65535 ) );
		Assert.AreEqual( 80, new SoundSelection( 1 ).Range( thunder.VolumeMax, thunder.VolumeMin ) );
	}

	[TestMethod]
	public void IndependentHandlesMixOverlapStopLoopAndDelay()
	{
		var mixer = new SoundMixer( 1000, 2 );
		long first = mixer.Play( new[] { 0.2f, 0.2f }, 0.5f, true );
		long second = mixer.Play( new[] { 0.3f, 0.3f } );
		var data = new float[4];
		Assert.AreNotEqual( first, second );
		mixer.Read( data, 0, 4 );
		Assert.AreEqual( 0.4f, data[0], 0.00001f );
		Assert.AreEqual( 0.1f, data[2], 0.00001f );
		Assert.IsFalse( mixer.IsPlaying( second ) );
		Assert.IsTrue( mixer.IsPlaying( first ) );
		mixer.Stop( first );
		mixer.Play( new[] { 0.7f, 0.7f }, delayMilliseconds: 1 );
		mixer.Read( data, 0, 4 );
		Assert.AreEqual( 0f, data[0] );
		Assert.AreEqual( 0.7f, data[2] );
		Assert.AreEqual( 0, mixer.ActiveVoices );
	}

	[TestMethod]
	public void MapRejectsInvalidSignaturesCountsAndTruncation()
	{
		GameData.Require();
		byte[] bank = FileSystem.ReadAllBytes( "global/sound/cat_globallobbysfxBANK.map" );
		byte[] sfx = FileSystem.ReadAllBytes( "global/sound/cat_globallobbysfxSFX.map" );
		Assert.ThrowsException<InvalidDataException>( () => SoundCategoryMap.Parse( bank, sfx[..^1] ) );
		var bad = (byte[])sfx.Clone(); bad[0] ^= 0xff;
		Assert.ThrowsException<InvalidDataException>( () => SoundCategoryMap.Parse( bank, bad ) );
		bad = (byte[])sfx.Clone(); Array.Fill( bad, (byte)255, 24, 4 );
		Assert.ThrowsException<InvalidDataException>( () => SoundCategoryMap.Parse( bank, bad ) );
	}

	[TestMethod]
	public void LobbyPersistentHandlesSwitchThemeWithoutLeakingPreviousVoices()
	{
		GameData.Require();
		using var audio = new AudioSystem( 123, openDevice: false );
		using var lobby = new LobbyAudio( audio );
		foreach ( string theme in new[] { "jungle", "hallow", "fantasy", "space" } )
		{
			lobby.SetIsland( theme );
			Assert.AreEqual( 2, audio.Mixer.ActiveVoices, theme );
			lobby.SetIsland( theme );
			Assert.AreEqual( 2, audio.Mixer.ActiveVoices, theme );
		}
		lobby.Dispose();
		Assert.AreEqual( 0, audio.Mixer.ActiveVoices );
	}
}
