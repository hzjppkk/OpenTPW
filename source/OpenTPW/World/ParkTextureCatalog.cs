namespace OpenTPW;

/// <summary>
/// Object-local textures take precedence over the theme's shared textures.
/// </summary>
public sealed class ParkTextureCatalog
{
	private readonly Dictionary<string, string> shared = new( StringComparer.OrdinalIgnoreCase );
	private readonly Dictionary<string, string[]> local = new( StringComparer.OrdinalIgnoreCase );

	public ParkTextureCatalog( string levelName )
	{
		foreach ( var directory in FileSystem.GetDirectories( $"levels/{levelName}" )
			.Where( x => Path.GetFileName( x ).Equals( "sharetex", StringComparison.OrdinalIgnoreCase ) ) )
		{
			var pending = new Stack<string>();
			pending.Push( directory );
			while ( pending.Count > 0 )
			{
				var path = pending.Pop();
				foreach ( var child in FileSystem.GetDirectories( path ) )
					pending.Push( child );
				foreach ( var file in FileSystem.GetFiles( path ).Where( x => x.EndsWith( ".wct", StringComparison.OrdinalIgnoreCase ) ) )
					shared.TryAdd( Path.GetFileNameWithoutExtension( file ), file );
			}
		}
	}

	public string? Find( string directory, string? name )
	{
		if ( name == null )
			return null;
		if ( !local.TryGetValue( directory, out var files ) )
			local[directory] = files = FileSystem.GetFiles( Path.Combine( directory, "textures" ) );
		return files.FirstOrDefault( x => Path.GetFileNameWithoutExtension( x ).Equals( name, StringComparison.OrdinalIgnoreCase ) )
			?? shared.GetValueOrDefault( name );
	}
}
