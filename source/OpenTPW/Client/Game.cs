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
		// Shell launchers can supply their own working directory. Resolve engine assets once
		// before shaders, icons and the editor cache use relative paths.
		Directory.SetCurrentDirectory( EngineContent.Find( Environment.CurrentDirectory, AppContext.BaseDirectory ) );

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
#if DEBUG
		// Run deterministic scene transitions through production callbacks without injecting OS input.
		LobbyAcceptance? acceptance = args.Length == 2 && args[0] == "--verify-lobby" ? new( level, args[1] ) : null;
#endif

		//
		// Run game loop
		//
		Render.OnUpdate += level.Update;
#if DEBUG
		if ( acceptance != null ) Render.OnUpdate += acceptance.Update;
#endif
		Render.OnRender += level.Render;
		Render.Run();
		Log.Info( "Render loop stopped" );
		// A closed SDL window does not stop the audio callback. Drain it before reporting completion.
		audio.Dispose();
		Log.Info( "Audio output disposed" );
#if DEBUG
		acceptance?.VerifyCompleted();
#endif
	}
}
