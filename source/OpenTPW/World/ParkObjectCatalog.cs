namespace OpenTPW;

/// <summary>
/// Resolves save object type ids against the current theme's object settings.
/// </summary>
public sealed class ParkObjectCatalog
{
	public sealed record Definition( string ModelPath, bool WorldSpace );
	public Dictionary<int, Definition> Objects { get; } = new();

	public ParkObjectCatalog( string levelName )
	{
		var pending = new Stack<string>();
		pending.Push( $"levels/{levelName}" );
		while ( pending.Count > 0 )
		{
			var directory = pending.Pop();
			foreach ( var child in FileSystem.GetDirectories( directory ) )
				pending.Push( child );

			var files = FileSystem.GetFiles( directory );
			foreach ( var path in files.Where( x => x.EndsWith( ".sam", StringComparison.OrdinalIgnoreCase ) ) )
			{
				var settings = new SettingsFile( path );
				if ( !int.TryParse( settings["Info.Id"], out var id ) )
					continue;

				var model = files.FirstOrDefault( x => Path.GetFileName( x ).Equals(
					Path.GetFileNameWithoutExtension( path ) + ".md2", StringComparison.OrdinalIgnoreCase ) );
				if ( model == null )
					continue;

				var definition = new Definition( model, settings["Info.DontApplyOffset"] == "1" );
				if ( !Objects.TryAdd( id, definition ) )
					throw new InvalidDataException( $"Duplicate object type {id} in {levelName}" );
			}
		}
	}
}
