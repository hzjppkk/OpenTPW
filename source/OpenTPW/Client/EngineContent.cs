namespace OpenTPW;

internal static class EngineContent
{
	internal static string Find( string workingDirectory, string executableDirectory )
	{
		// Preserve an explicit development root; shell launchers fall back to packaged
		// assets beside the executable or the source checkout above it.
		bool HasContent( string directory ) => File.Exists( Path.Join( directory, "content", "shaders", "ui.shader" ) );
		if ( HasContent( workingDirectory ) ) return Path.GetFullPath( workingDirectory );
		for ( var directory = new DirectoryInfo( executableDirectory ); directory != null; directory = directory.Parent )
			if ( HasContent( directory.FullName ) ) return directory.FullName;
		throw new DirectoryNotFoundException( $"OpenTPW content/shaders not found from {workingDirectory} or {executableDirectory}" );
	}
}
