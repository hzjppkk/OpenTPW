using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class StringAndSaveTests
{
	[TestMethod]
	public void EveryStringTableReads()
	{
		GameData.Require();

		var tables = FileSystem.GetFiles( "Language/English" ).Where( x => x.EndsWith( ".str", StringComparison.OrdinalIgnoreCase ) ).ToList();
		Assert.IsTrue( tables.Count > 0 );

		var failures = new List<string>();
		foreach ( var path in tables )
		{
			try
			{
				var bytes = File.ReadAllBytes( path );
				var file = new StringFile( new MemoryStream( bytes ) );
				if ( file.Entries.Length != BitConverter.ToInt32( bytes, 8 ) )
					failures.Add( $"{path}: {file.Entries.Length} entries" );
			}
			catch ( Exception e )
			{
				failures.Add( $"{path}: {e.Message}" );
			}
		}

		Assert.AreEqual( 0, failures.Count, string.Join( "\n", failures ) );
	}

	[TestMethod]
	public void EverySaveInTheDataDecompresses()
	{
		GameData.Require();

		var saves = GameData.FindFiles( ".tpws" ).Concat( GameData.FindFiles( ".tpwi" ) ).ToList();
		if ( saves.Count == 0 )
			Assert.Inconclusive( "No saves in the game data" );

		foreach ( var path in saves )
			Assert.IsTrue( new SaveReader( FileSystem.OpenRead( path ) ).ReadFile().Length > 0, path );
	}

	[TestMethod]
	public void EverySaveParksObjectsKnownToTheLevels()
	{
		GameData.Require();

		var saves = GameData.FindFiles( ".tpws" ).Concat( GameData.FindFiles( ".tpwi" ) ).ToList();
		if ( saves.Count == 0 )
			Assert.Inconclusive( "No saves in the game data" );

		var typeIds = new HashSet<int>();
		foreach ( var path in GameData.FindFiles( ".sam" ) )
		{
			if ( int.TryParse( new SettingsFile( FileSystem.OpenRead( path ) )["Info.Id"], out var id ) )
				typeIds.Add( id );
		}

		var failures = new List<string>();
		foreach ( var path in saves )
		{
			try
			{
				// Reading checks that the things end exactly at the World module's tag
				var save = new SaveFile( FileSystem.OpenRead( path ) );
				if ( save.WorldVersion != 2 || save.Map.All( x => x.Type == -1 ) )
					failures.Add( $"{path}: version {save.WorldVersion}" );

				foreach ( var unknown in save.Objects.Where( x => !typeIds.Contains( x.TypeId ) ) )
					failures.Add( $"{path}: object type {unknown.TypeId}" );

				if ( !save.Objects.Any() || save.Things.Count( x => x.Model == SaveFile.ThingModel.BankAccount ) != 1 )
					failures.Add( $"{path}: {save.Objects.Count()} objects" );
			}
			catch ( Exception e )
			{
				failures.Add( $"{path}: {e.Message}" );
			}
		}

		Assert.AreEqual( 0, failures.Count, string.Join( "\n", failures ) );
	}
}
