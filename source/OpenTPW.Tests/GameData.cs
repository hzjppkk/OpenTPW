using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The repository's content/ folder, found from the test's directory upwards.
/// </summary>
internal static class Content
{
	public static string GetPath( string path )
	{
		for ( var directory = new DirectoryInfo( Environment.CurrentDirectory ); directory != null; directory = directory.Parent )
		{
			var candidate = Path.Combine( directory.FullName, "content", path );
			if ( File.Exists( candidate ) )
				return candidate;
		}

		throw new FileNotFoundException( $"content/{path} not found from {Environment.CurrentDirectory}" );
	}
}

/// <summary>
/// A folder of sample saves, samples/saves, found the way the game folder is found
/// (<see cref="GameDir.Find"/>): in the test's directory and each folder above it, the folder itself
/// and then its direct subfolders.
/// </summary>
internal static class SampleSaves
{
	public static string? Find()
	{
		for ( var directory = new DirectoryInfo( Environment.CurrentDirectory ); directory != null; directory = directory.Parent )
		{
			var candidates = new List<string> { directory.FullName };
			try
			{
				candidates.AddRange( Directory.GetDirectories( directory.FullName ).Order( StringComparer.OrdinalIgnoreCase ) );
			}
			catch ( Exception e ) when ( e is UnauthorizedAccessException or IOException )
			{
			}

			var match = candidates.Select( x => Path.Combine( x, "samples", "saves" ) ).FirstOrDefault( Directory.Exists );
			if ( match != null )
				return match;
		}

		return null;
	}
}

/// <summary>
/// Tests over the whole game data, found the way the game finds it (<see cref="GameDir.Find"/>) from
/// the test's directory: put the game folder (the one with data/) there or in a folder above it.
/// Without it these tests are inconclusive rather than failing.
/// </summary>
internal static class GameData
{
	public static string? GamePath => GameDir.Find( Environment.CurrentDirectory );

	public static void Require()
	{
		if ( GamePath is not string gamePath )
		{
			Assert.Inconclusive( $"No game data found from {Environment.CurrentDirectory}" );
			return;
		}

		var data = Path.Combine( gamePath, "data" );

		Log ??= new();
		FileSystem = new BaseFileSystem( data );
		FileSystem.RegisterArchiveHandler<WadArchive>( ".wad" );
		FileSystem.RegisterArchiveHandler<SdtArchive>( ".sdt" );
	}

	/// <summary>
	/// Every file with the extension, inside archives too.
	/// </summary>
	public static List<string> FindFiles( string extension )
	{
		var found = new List<string>();
		var pending = new Stack<string>();
		pending.Push( "/" );

		while ( pending.Count > 0 )
		{
			var directory = pending.Pop();
			found.AddRange( FileSystem.GetFiles( directory ).Where( x => x.EndsWith( extension, StringComparison.OrdinalIgnoreCase ) ) );

			foreach ( var child in FileSystem.GetDirectories( directory ) )
				pending.Push( child );
		}

		return found;
	}
}
