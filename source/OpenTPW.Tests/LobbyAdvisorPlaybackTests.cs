using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class LobbyAdvisorPlaybackTests
{
	[TestMethod]
	public void LipBoundariesToggleOncePerUpdateAndDriveTimedMouthChoices()
	{
		byte[] bytes = new[] { 0, 200000, 500000, 600000, -1 }.SelectMany( BitConverter.GetBytes ).ToArray();
		var lips = new LobbyAdvisorLips( bytes );
		int draws = 0;
		int Next() { draws++; return 2; }
		lips.Update( -1, Next );
		Assert.AreEqual( 1, lips.Mouth );
		lips.Update( 1, Next );
		Assert.IsFalse( lips.Speaking );
		Assert.AreEqual( 0, draws );
		lips.Update( 201, Next );
		Assert.IsTrue( lips.Speaking );
		Assert.AreEqual( 3, lips.Mouth );
		lips.Update( 301, Next );
		Assert.AreEqual( 1, draws, "Retail uses a strict greater-than comparison" );
		lips.Update( 302, Next );
		Assert.AreEqual( 2, draws );
		lips.Update( 1000, Next );
		Assert.IsFalse( lips.Finished, "Only one boundary is consumed per update" );
		lips.Update( 1000, Next );
		Assert.IsTrue( lips.Finished );
		Assert.AreEqual( 1, lips.Mouth );
		Assert.ThrowsException<InvalidDataException>( () => new LobbyAdvisorLips( bytes[..^1] ) );
		Assert.ThrowsException<InvalidDataException>( () => new LobbyAdvisorLips( BitConverter.GetBytes( 100 ) ) );
		Assert.IsTrue( new LobbyAdvisorLips( BitConverter.GetBytes( -1 ) ).Finished );
	}

	[TestMethod]
	public void WelcomeQueueBindsNativeResponsesAndCanReplaceActiveSpeech()
	{
		GameData.Require();
		using var audio = new AudioSystem( 1, false );
		var advisor = new LobbyAdvisor( audio, () => 0 );
		advisor.Welcome( false );
		Assert.AreEqual( 2, advisor.PendingCount );
		advisor.Update( 0 );
		Assert.AreEqual( 0x186, advisor.Response );
		Assert.AreEqual( 0, audio.Mixer.ActiveVoices );
		advisor.Update( 0.799 );
		Assert.AreEqual( 0, audio.Mixer.ActiveVoices );
		advisor.Update( 0.8 );
		Assert.AreEqual( 1, audio.Mixer.ActiveVoices );
		advisor.Welcome( true );
		Assert.AreEqual( 0, audio.Mixer.ActiveVoices );
		Assert.AreEqual( 1, advisor.PendingCount );
		advisor.Update( 1 );
		Assert.AreEqual( 0x18e, advisor.Response );
		advisor.Cancel();
		Assert.IsFalse( advisor.Active );
		Assert.AreEqual( 0, advisor.PendingCount );
		foreach ( string name in new[] { "mouth - normal", "mouth - aah", "mouth - eee", "mouth - ooh", "mouth - sss" } )
			Assert.AreEqual( 1, advisor.Geometry.Model.Meshes.Count( mesh => mesh.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) ) );
	}

	[TestMethod]
	public void EveryImplementedLobbyResponseResolvesSpeechAndLipData()
	{
		GameData.Require();
		using var audio = new AudioSystem( 1, false );
		var advisor = new LobbyAdvisor( audio, () => 0 );
		foreach ( int response in new[] { 0x186, 0x187, 0x18e, 0x18a, 0x23a, 0x250, 0x256 } )
		{
			advisor.Say( response, true );
			advisor.Update( 0 );
			Assert.AreEqual( response, advisor.Response );
			Assert.IsTrue( advisor.Active );
		}
	}
}
