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
		bool previewPark = args.Length == 2 && args[0] == "--park";
		if ( args.Length != 0 && !previewPark )
			throw new ArgumentException( "Usage: OpenTPW [--park <level>]" );
		var level = new Level( previewPark ? args[1] : "jungle", previewPark );

		//
		// Run game loop
		//
		Render.OnUpdate += level.Update;
		Render.OnRender += level.Render;
		Render.Run();
	}
}
