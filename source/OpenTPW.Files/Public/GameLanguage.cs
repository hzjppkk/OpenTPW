namespace OpenTPW;

/// <summary>
/// The folder under data/Language that strings, bitmap fonts and the multibyte table come from.
/// Retail picks it from the registry Language LCID (default 0x809, English); OpenTPW takes the folder
/// name instead, so an English release and a Chinese language folder are chosen the same way.
/// </summary>
public static class GameLanguage
{
	public const string Default = "English";

	/// <summary>Folder name under data/Language, as spelled on disk. English until <see cref="Select"/>.</summary>
	public static string Name { get; private set; } = Default;

	/// <summary>Path of a file in the selected language folder, relative to data/.</summary>
	public static string Path( string file ) => $"Language/{Name}/{file}";

	public static void Select( string name ) => Name = string.IsNullOrWhiteSpace( name )
		? throw new ArgumentException( "Language folder name is empty", nameof(name) ) : name;

	/// <summary>
	/// The requested folder when it exists (any case); without a request, English if present,
	/// otherwise the only folder. Anything else is ambiguous or missing and throws.
	/// </summary>
	public static string Resolve( IEnumerable<string> available, string? requested )
	{
		var folders = available.ToArray();
		string? Find( string name ) => folders.FirstOrDefault( folder => folder.Equals( name, StringComparison.OrdinalIgnoreCase ) );
		if ( !string.IsNullOrWhiteSpace( requested ) )
			return Find( requested.Trim() ) ?? throw new DirectoryNotFoundException(
				$"Language '{requested}' not found in data/Language (available: {string.Join( ", ", folders )})" );
		if ( Find( Default ) is string english ) return english;
		if ( folders.Length == 1 ) return folders[0];
		throw new DirectoryNotFoundException( folders.Length == 0
			? "data/Language has no language folder"
			: $"data/Language has several languages and no English; set Language to one of: {string.Join( ", ", folders )}" );
	}

	/// <summary>Language folder names present in <paramref name="files"/> (rooted at data/).</summary>
	public static string[] Available( BaseFileSystem files ) => files.GetDirectories( "Language" )
		.Select( directory => System.IO.Path.GetFileName( directory.TrimEnd( '/', '\\' ) ) ).ToArray();
}
