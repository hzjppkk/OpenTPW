using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class SaveModuleTests
{
	private static readonly string[] Modules =
	{
		"Scripts", "Particles", "MsgCntr", "Clock", "VanillaTime", "GameSystem", "RideSystem", "TrackRides",
		"FlyingRides", "RSSE", "Camera", "Coasters", "Advisor", "Sound", "Cheat", "AdvisorScoring", "UI"
	};

	[TestMethod]
	public void EverySaveInTheSavesPathEndsEveryModuleOnItsTag()
	{
		if ( SampleSaves.Find() is not string directory )
		{
			Assert.Inconclusive( $"No samples/saves folder found from {Environment.CurrentDirectory}" );
			return;
		}

		var saves = Directory.GetFiles( directory )
			.Where( x => new[] { ".tpws", ".tpwi", ".ints" }.Contains( Path.GetExtension( x ).ToLowerInvariant() ) )
			.ToList();
		if ( saves.Count == 0 )
			Assert.Inconclusive( $"No saves in {directory}" );

		var failures = new List<string>();
		foreach ( var path in saves )
		{
			try
			{
				// Reading checks every module's tag and that the data ends exactly after the UI module
				using var stream = File.OpenRead( path );
				var save = new SaveFile( stream );

				var missing = Modules.Where( x => !save.ModuleRanges.ContainsKey( x ) ).ToList();
				if ( missing.Count > 0 )
					failures.Add( $"{path}: modules not read: {string.Join( ", ", missing )}" );

				if ( save.PlacedRides.Select( x => x.Handle ).Distinct().Count() != save.PlacedRides.Count() )
					failures.Add( $"{path}: ride handles repeat" );

				if ( save.ClockTime == 0 || string.IsNullOrEmpty( save.BuildStamp ) || save.GameSystem.Length != 9 )
					failures.Add( $"{path}: clock {save.ClockTime}, build '{save.BuildStamp}'" );

				var dateSet = save.GameMonth is >= 0 and <= 11 && save.GameDay >= 1
					&& save.GameDay <= DateTime.DaysInMonth( 2000, save.GameMonth + 1 );
				var dateNotSetYet = save.GameMonth == 0 && save.GameDay == -1;
				if ( !(dateSet || dateNotSetYet) || save.FunnySecondsPerRealSecond <= 0 )
					failures.Add( $"{path}: date {save.GameMonth}/{save.GameDay}, {save.FunnySecondsPerRealSecond} funny seconds per real second" );

				var objects = save.Objects.Select( x => x.TypeId ).OrderBy( x => x ).ToList();
				var placed = save.PlacedRides.Select( x => x.TypeId ).OrderBy( x => x ).ToList();
				if ( !objects.SequenceEqual( placed ) )
					failures.Add( $"{path}: ride system lists {placed.Count} placed rides, the park has {objects.Count} objects" );
			}
			catch ( Exception e )
			{
				failures.Add( $"{path}: {e.Message}" );
			}
		}

		Assert.AreEqual( 0, failures.Count, string.Join( "\n", failures ) );
	}
}
