namespace OpenTPW;

/// <summary>
/// Handles the creation and management of various systems, including the game
/// window.
/// </summary>
internal static class Game
{
	public static void Run( string[] args )
	{
		Log = new();

		//
		// Find the game data directory (throws if there is none)
		//
		var gamePath = GameDir.GamePath;
		Log.Info( $"Game data: {gamePath}" );

		// Register game data directory
		FileSystem = new BaseFileSystem( $"{gamePath}/data/" );
		FileSystem.RegisterArchiveHandler<WadArchive>( ".wad" );
		FileSystem.RegisterArchiveHandler<SdtArchive>( ".sdt" );

		//
		// Check if the save data directory exists (create if not)
		//
		if ( !Path.Exists( $"{gamePath}/save/" ) )
			Directory.CreateDirectory( $"{gamePath}/save/" );

		// Register save data directory
		SaveFileSystem = new BaseFileSystem( $"{gamePath}/save/" );

		//
		// Custom OpenTPW cache directory (mainly for editor-related stuff)
		//
		CacheFileSystem = new BaseFileSystem( $"./.opentpw" );

		//
		// Init renderer
		//
		Render = new();

		//
		// Create level
		//
		using var audio = new AudioSystem( unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()) );
		if ( !audio.Available ) Log.Warning( $"Audio output unavailable: {audio.Failure}" );
		var level = new Level( "jungle", audio );

		//
		// Run game loop
		//
		Render.OnUpdate += level.Update;
		Render.OnRender += level.Render;
		Render.Run();
	}
}
